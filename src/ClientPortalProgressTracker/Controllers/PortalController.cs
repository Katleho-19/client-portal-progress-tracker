using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClientPortalProgressTracker.Models.Entities;
using ClientPortalProgressTracker.Models.ViewModels;
using ClientPortalProgressTracker.Services;
using Microsoft.AspNetCore.Mvc;
using static Supabase.Postgrest.Constants;

namespace ClientPortalProgressTracker.Controllers;

/// <summary>
/// Client portal: no login. Access is by the private link /p/{slug} only.
/// The server uses the service-role client, so every action first loads the
/// project by slug and checks that the link is active.
/// </summary>
[Route("p/{slug}")]
public class PortalController : Controller
{
    private const long MaxProofBytes = 10 * 1024 * 1024; // 10 MB

    private readonly SupabaseAdmin _admin;
    private readonly ILogger<PortalController> _logger;

    public PortalController(SupabaseAdmin admin, ILogger<PortalController> logger)
    {
        _admin = admin;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // One URL, the right screen for the project's status
    // ------------------------------------------------------------------
    [HttpGet("")]
    public async Task<IActionResult> Index(string slug)
    {
        var vm = await LoadAsync(slug);
        if (vm == null) return View("Unavailable");

        switch (vm.Project.Status)
        {
            case ProjectStatus.Draft:
                return vm.IsExpired ? View("Expired", vm) : View("Review", vm);

            case ProjectStatus.AwaitingDeposit:
                return View("Pay", vm);

            default: // Active, Completed_Unpaid, Paid_In_Full
                return View("Progress", vm);
        }
    }

    // ------------------------------------------------------------------
    // FR-08 / FR-09: typed e-signature, audit trail, scope lock
    // ------------------------------------------------------------------
    [HttpPost("sign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sign(string slug, string? signerFullName, bool agree)
    {
        var vm = await LoadAsync(slug);
        if (vm == null) return View("Unavailable");

        var project = vm.Project;

        // Already signed (or past the signing stage): just show the current screen
        if (project.Status != ProjectStatus.Draft || vm.Signature != null)
            return RedirectToAction(nameof(Index), new { slug });

        if (vm.IsExpired) return View("Expired", vm);

        var name = (signerFullName ?? string.Empty).Trim();
        if (!agree || name.Length < 3)
        {
            TempData["PortalError"] = "Please tick the agreement box and type your full legal name to sign.";
            return RedirectToAction(nameof(Index), new { slug });
        }

        if (name.Length > 150) name = name[..150];

        var db = await _admin.GetAsync();

        var signature = new QuoteSignature
        {
            ProjectId = project.Id,
            SignerFullName = name,
            AgreedToTerms = true,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Truncate(Request.Headers.UserAgent.ToString(), 500),
            ScopeHash = ComputeScopeHash(project, vm.Milestones),
            SignedAt = DateTime.UtcNow
        };

        try
        {
            // quote_signatures.project_id is UNIQUE, so a double submit cannot create two signatures
            await db.From<QuoteSignature>().Insert(signature);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Signature insert failed for project {ProjectId}", project.Id);
            return RedirectToAction(nameof(Index), new { slug });
        }

        // FR-09: lock the scope and move Draft -> Awaiting_Deposit
        // (with a 0% deposit there is nothing to wait for, so work can start)
        project.Status = vm.DepositAmount <= 0 ? ProjectStatus.Active : ProjectStatus.AwaitingDeposit;
        await db.From<Project>().Update(project);

        TempData["PortalSuccess"] = "Thank you. Your signature has been recorded and the project scope is now locked.";
        return RedirectToAction(nameof(Index), new { slug });
    }

    // ------------------------------------------------------------------
    // Payment choice
    // ------------------------------------------------------------------
    [HttpGet("pay")]
    public async Task<IActionResult> Pay(string slug)
    {
        var vm = await LoadAsync(slug);
        if (vm == null) return View("Unavailable");
        if (!CanPay(vm)) return RedirectToAction(nameof(Index), new { slug });
        return View(vm);
    }

    // ------------------------------------------------------------------
    // FR-13: manual EFT details + FR-14: proof of payment upload
    // ------------------------------------------------------------------
    [HttpGet("eft")]
    public async Task<IActionResult> Eft(string slug)
    {
        var vm = await LoadAsync(slug);
        if (vm == null) return View("Unavailable");
        if (!CanPay(vm) || vm.PendingEft != null) return RedirectToAction(nameof(Pay), new { slug });
        return View(vm);
    }

    [HttpPost("eft")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> EftUpload(string slug, IFormFile? proof)
    {
        var vm = await LoadAsync(slug);
        if (vm == null) return View("Unavailable");
        if (!CanPay(vm) || vm.PendingEft != null) return RedirectToAction(nameof(Pay), new { slug });

        if (proof == null || proof.Length == 0)
        {
            TempData["PortalError"] = "Please choose your proof of payment (PDF) first.";
            return RedirectToAction(nameof(Eft), new { slug });
        }

        if (proof.Length > MaxProofBytes)
        {
            TempData["PortalError"] = "That file is too large. The maximum size is 10 MB.";
            return RedirectToAction(nameof(Eft), new { slug });
        }

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await proof.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        // PDF only: check the extension and the file signature ("%PDF")
        var isPdf = Path.GetExtension(proof.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                    && bytes.Length > 4
                    && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46;
        if (!isPdf)
        {
            TempData["PortalError"] = "Please upload your proof of payment as a PDF file.";
            return RedirectToAction(nameof(Eft), new { slug });
        }

        var db = await _admin.GetAsync();
        var tx = new PaymentTransaction
        {
            ProjectId = vm.Project.Id,
            PaymentMethod = PaymentMethod.ManualEft,
            Amount = vm.AmountDueNow,
            PaymentType = vm.PaymentTypeDue,
            Status = TransactionStatus.PendingVerification
        };

        // Files are stored as <project_id>/<transaction_id>.pdf (matches the storage policy)
        var path = $"{vm.Project.Id}/{tx.Id}.pdf";

        try
        {
            await db.Storage.From("proof-of-payments").Upload(
                bytes, path,
                new Supabase.Storage.FileOptions { ContentType = "application/pdf", Upsert = false });

            tx.PopFilePath = path;
            await db.From<PaymentTransaction>().Insert(tx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Proof of payment upload failed for project {ProjectId}", vm.Project.Id);
            TempData["PortalError"] = "We could not upload your file. Please try again.";
            return RedirectToAction(nameof(Eft), new { slug });
        }

        TempData["PortalSuccess"] = "Proof of payment received. Your freelancer will confirm it once the funds clear.";
        return RedirectToAction(nameof(Pay), new { slug });
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    private static bool CanPay(PortalViewModel vm) =>
        (vm.Project.Status == ProjectStatus.AwaitingDeposit || vm.Project.Status == ProjectStatus.CompletedUnpaid)
        && vm.AmountDueNow > 0;

    private async Task<PortalViewModel?> LoadAsync(string slug)
    {
        var db = await _admin.GetAsync();

        var project = await db.From<Project>().Where(p => p.PortalSlug == slug).Single();
        if (project == null || !project.IsLinkActive) return null;

        var freelancer = await db.From<Freelancer>().Where(f => f.Id == project.FreelancerId).Single();
        if (freelancer == null) return null;

        var milestones = await db.From<Milestone>()
            .Where(m => m.ProjectId == project.Id)
            .Order("sort_order", Ordering.Ascending)
            .Get();

        var transactions = await db.From<PaymentTransaction>()
            .Where(t => t.ProjectId == project.Id)
            .Order("created_at", Ordering.Ascending)
            .Get();

        var signature = await db.From<QuoteSignature>().Where(s => s.ProjectId == project.Id).Single();

        // Recovery: signature saved but the status update did not complete
        if (project.Status == ProjectStatus.Draft && signature != null)
        {
            project.Status = ProjectStatus.AwaitingDeposit;
            await db.From<Project>().Update(project);
        }

        return new PortalViewModel
        {
            Project = project,
            Freelancer = freelancer,
            Milestones = milestones.Models,
            Transactions = transactions.Models,
            Signature = signature
        };
    }

    /// <summary>Fingerprint of exactly what the client agreed to (stored in quote_signatures.scope_hash).</summary>
    private static string ComputeScopeHash(Project p, IEnumerable<Milestone> milestones)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append(p.Id).Append('|').Append(p.Title).Append('|')
          .Append(p.TotalAmount.ToString("0.00", inv)).Append('|').Append(p.DepositPercentage);

        foreach (var m in milestones.OrderBy(m => m.SortOrder))
        {
            sb.Append('|').Append(m.SortOrder).Append(':').Append(m.Title).Append(':')
              .Append(m.Description ?? string.Empty).Append(':')
              .Append((m.Amount ?? 0m).ToString("0.00", inv));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}