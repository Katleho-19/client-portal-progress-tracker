using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ClientPortalProgressTracker.Models.Entities;
using ClientPortalProgressTracker.Models.ViewModels;
using ClientPortalProgressTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static Supabase.Postgrest.Constants;

namespace ClientPortalProgressTracker.Controllers;

[Authorize]
public class ProjectsController : Controller
{
    private readonly SupabaseAdmin _admin;
    private readonly EmailService _email;

    public ProjectsController(SupabaseAdmin admin, EmailService email)
    {
        _admin = admin;
        _email = email;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var (nextRef, _) = await GenerateReferencePreviewAsync();
        ViewBag.PreviewRef = nextRef;
        ViewBag.HostUrl = $"{Request.Scheme}://{Request.Host}";
        return View(new CreateProjectViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProjectViewModel model, string submitAction)
    {
        var validMilestones = model.Milestones
            .Where(m => !string.IsNullOrWhiteSpace(m.Title))
            .ToList();

        if (validMilestones.Count == 0)
            ModelState.AddModelError(string.Empty, "Add at least one project milestone.");

        if (!ModelState.IsValid)
        {
            var (nextRef, _) = await GenerateReferencePreviewAsync();
            ViewBag.PreviewRef = nextRef;
            ViewBag.HostUrl = $"{Request.Scheme}://{Request.Host}";
            return View(model);
        }

        var db = await _admin.GetAsync();
        var (referenceCode, _) = await GenerateReferencePreviewAsync();

        var portalSlug = BuildSlug(model.Title);

        var isDraft = string.Equals(submitAction, "draft", StringComparison.OrdinalIgnoreCase);
        var calculatedTotal = validMilestones.Sum(m => m.Amount);

        var project = new Project
        {
            FreelancerId = CurrentUserId,
            ReferenceCode = referenceCode,
            PortalSlug = portalSlug,
            IsLinkActive = true,
            QuoteExpiresAt = model.QuoteExpiresAt.HasValue
                ? DateTime.SpecifyKind(model.QuoteExpiresAt.Value, DateTimeKind.Utc)
                : null,
            MessageToClient = string.IsNullOrWhiteSpace(model.MessageToClient) ? null : model.MessageToClient.Trim(),
            ClientName = model.ClientName.Trim(),
            ClientEmail = model.ClientEmail.Trim(),
            Title = model.Title.Trim(),
            TotalAmount = calculatedTotal,
            DepositPercentage = model.DepositPercentage,
            AmountPaid = 0m,
            Status = ProjectStatus.Draft
        };

        await db.From<Project>().Insert(project);

        var milestoneEntities = validMilestones.Select((m, idx) => new Milestone
        {
            ProjectId = project.Id,
            Title = m.Title.Trim(),
            Description = m.Description?.Trim() ?? string.Empty,
            Amount = m.Amount,
            SortOrder = idx + 1,
            Status = MilestoneStatus.Pending
        }).ToList();

        await db.From<Milestone>().Insert(milestoneEntities);

        TempData["SuccessMessage"] = isDraft
            ? $"Draft saved ({project.ReferenceCode})."
            : $"Quote created ({project.ReferenceCode})! Share the portal link so your client can review and sign.";

        return RedirectToAction(nameof(Details), new { id = project.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var db = await _admin.GetAsync();
        var project = await db.From<Project>().Where(p => p.Id == id && p.FreelancerId == CurrentUserId).Single();
        if (project == null) return NotFound();

        var sig = await db.From<QuoteSignature>().Where(s => s.ProjectId == project.Id).Single();
        if (sig != null)
        {
            TempData["ErrorMessage"] = "This quote has already been signed by the client and can no longer be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var milestonesRes = await db.From<Milestone>()
            .Where(m => m.ProjectId == project.Id)
            .Order("sort_order", Ordering.Ascending)
            .Get();

        var vm = new EditProjectViewModel
        {
            Id = project.Id,
            ReferenceCode = project.ReferenceCode,
            ClientName = project.ClientName,
            ClientEmail = project.ClientEmail,
            Title = project.Title,
            DepositPercentage = project.DepositPercentage,
            QuoteExpiresAt = project.QuoteExpiresAt,
            MessageToClient = project.MessageToClient,
            Milestones = milestonesRes.Models.Select(m => new MilestoneInputItem
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description ?? string.Empty,
                Amount = m.Amount ?? 0m
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProjectViewModel model)
    {
        var validMilestones = model.Milestones
            .Where(m => !string.IsNullOrWhiteSpace(m.Title))
            .ToList();

        if (validMilestones.Count == 0)
            ModelState.AddModelError(string.Empty, "Add at least one project milestone.");

        if (!ModelState.IsValid) return View(model);

        var db = await _admin.GetAsync();
        var project = await db.From<Project>().Where(p => p.Id == model.Id && p.FreelancerId == CurrentUserId).Single();
        if (project == null) return NotFound();

        var sig = await db.From<QuoteSignature>().Where(s => s.ProjectId == project.Id).Single();
        if (sig != null)
        {
            TempData["ErrorMessage"] = "Signed quotes cannot be modified.";
            return RedirectToAction(nameof(Details), new { id = project.Id });
        }

        project.ClientName = model.ClientName.Trim();
        project.ClientEmail = model.ClientEmail.Trim();
        project.Title = model.Title.Trim();
        project.DepositPercentage = model.DepositPercentage;
        project.QuoteExpiresAt = model.QuoteExpiresAt.HasValue
            ? DateTime.SpecifyKind(model.QuoteExpiresAt.Value, DateTimeKind.Utc)
            : null;
        project.MessageToClient = string.IsNullOrWhiteSpace(model.MessageToClient) ? null : model.MessageToClient.Trim();
        project.TotalAmount = validMilestones.Sum(m => m.Amount);

        await db.From<Project>().Update(project);

        await db.From<Milestone>().Where(m => m.ProjectId == project.Id).Delete();
        var milestoneEntities = validMilestones.Select((m, idx) => new Milestone
        {
            ProjectId = project.Id,
            Title = m.Title.Trim(),
            Description = m.Description?.Trim() ?? string.Empty,
            Amount = m.Amount,
            SortOrder = idx + 1,
            Status = MilestoneStatus.Pending
        }).ToList();
        await db.From<Milestone>().Insert(milestoneEntities);

        TempData["SuccessMessage"] = "Quote and milestone pricing updated.";
        return RedirectToAction(nameof(Details), new { id = project.Id });
    }
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var db = await _admin.GetAsync();
        var project = await db.From<Project>()
            .Where(p => p.Id == id && p.FreelancerId == CurrentUserId)
            .Single();

        if (project == null) return NotFound();

        var milestonesRes = await db.From<Milestone>()
            .Where(m => m.ProjectId == project.Id)
            .Order("sort_order", Ordering.Ascending)
            .Get();

        var txRes = await db.From<PaymentTransaction>()
            .Where(t => t.ProjectId == project.Id)
            .Order("created_at", Ordering.Ascending)
            .Get();

        var sig = await db.From<QuoteSignature>()
            .Where(s => s.ProjectId == project.Id)
            .Single();

        var vm = new ProjectDetailsViewModel
        {
            Project = project,
            Milestones = milestonesRes.Models,
            Transactions = txRes.Models,
            Signature = sig,
            PortalUrl = $"{Request.Scheme}://{Request.Host}/p/{project.PortalSlug}"
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendQuoteEmail(Guid projectId)
    {
        var db = await _admin.GetAsync();
        var project = await db.From<Project>()
            .Where(p => p.Id == projectId && p.FreelancerId == CurrentUserId)
            .Single();

        if (project == null) return NotFound();

        var freelancer = await db.From<Freelancer>().Where(f => f.Id == CurrentUserId).Single();
        var portalUrl = $"{Request.Scheme}://{Request.Host}/p/{project.PortalSlug}";

        try
        {
            await _email.SendQuoteEmailAsync(
                project.ClientEmail,
                project.ClientName,
                freelancer?.FullName ?? "Freelancer",
                freelancer?.Email ?? User.FindFirstValue(ClaimTypes.Email)!,
                freelancer?.BusinessName ?? "Studio",
                project.Title,
                project.ReferenceCode,
                project.TotalAmount,
                project.DepositPercentage,
                project.MessageToClient,
                portalUrl);

            project.IsLinkActive = true;
            project.QuoteSentAt = DateTime.UtcNow;
            await db.From<Project>().Update(project);

            TempData["SuccessMessage"] = $"Quote emailed to {project.ClientEmail}.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Could not send email: {ex.Message}";
        }

        return RedirectToAction(nameof(Details), new { id = projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLink(Guid projectId)
    {
        var db = await _admin.GetAsync();
        var project = await db.From<Project>()
            .Where(p => p.Id == projectId && p.FreelancerId == CurrentUserId)
            .Single();

        if (project == null) return NotFound();

        project.IsLinkActive = !project.IsLinkActive;
        await db.From<Project>().Update(project);

        TempData["SuccessMessage"] = project.IsLinkActive
            ? "Client portal link turned ON."
            : "Client portal link turned OFF.";
        return RedirectToAction(nameof(Details), new { id = projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMilestoneStatus(Guid projectId, Guid milestoneId, string status)
    {
        var db = await _admin.GetAsync();
        var project = await db.From<Project>()
            .Where(p => p.Id == projectId && p.FreelancerId == CurrentUserId)
            .Single();

        if (project == null) return NotFound();

        var milestone = await db.From<Milestone>()
            .Where(m => m.Id == milestoneId && m.ProjectId == project.Id)
            .Single();

        if (milestone != null)
        {
            milestone.Status = status;
            milestone.CompletedAt = status == MilestoneStatus.Completed ? DateTime.UtcNow : null;
            await db.From<Milestone>().Update(milestone);

            var allMilestones = await db.From<Milestone>().Where(m => m.ProjectId == project.Id).Get();
            if (allMilestones.Models.Count > 0 && allMilestones.Models.All(m => m.Status == MilestoneStatus.Completed))
            {
                if (project.AmountPaid < project.TotalAmount)
                {
                    project.Status = ProjectStatus.CompletedUnpaid;
                    await db.From<Project>().Update(project);
                }
            }
        }

        TempData["SuccessMessage"] = "Milestone progress updated.";
        return RedirectToAction(nameof(Details), new { id = projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegenerateLink(Guid projectId)
    {
        var db = await _admin.GetAsync();
        var project = await db.From<Project>()
            .Where(p => p.Id == projectId && p.FreelancerId == CurrentUserId)
            .Single();

        if (project == null) return NotFound();

        project.PortalSlug = BuildSlug(project.Title);
        project.IsLinkActive = true;

        await db.From<Project>().Update(project);

        TempData["SuccessMessage"] = "New client portal link generated. The previous link is now inactive.";
        return RedirectToAction(nameof(Details), new { id = projectId });
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyEft(Guid projectId, Guid transactionId)
    {
        var db = await _admin.GetAsync();
        var project = await db.From<Project>()
            .Where(p => p.Id == projectId && p.FreelancerId == CurrentUserId)
            .Single();

        if (project == null) return NotFound();

        var tx = await db.From<PaymentTransaction>()
            .Where(t => t.Id == transactionId && t.ProjectId == project.Id)
            .Single();

        if (tx != null && tx.Status == TransactionStatus.PendingVerification)
        {
            tx.Status = TransactionStatus.Success;
            tx.VerifiedAt = DateTime.UtcNow;
            tx.PaidAt ??= DateTime.UtcNow;
            await db.From<PaymentTransaction>().Update(tx);

            project.AmountPaid += tx.Amount;
            project.Status = project.AmountPaid >= project.TotalAmount
                ? ProjectStatus.PaidInFull
                : ProjectStatus.Active;

            await db.From<Project>().Update(project);
            TempData["SuccessMessage"] = $"Payment of R {tx.Amount:N2} confirmed.";
        }

        return RedirectToAction(nameof(Details), new { id = projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectEft(Guid projectId, Guid transactionId)
    {
        var db = await _admin.GetAsync();
        var project = await db.From<Project>()
            .Where(p => p.Id == projectId && p.FreelancerId == CurrentUserId)
            .Single();

        if (project == null) return NotFound();

        var tx = await db.From<PaymentTransaction>()
            .Where(t => t.Id == transactionId && t.ProjectId == project.Id)
            .Single();

        if (tx != null && tx.Status == TransactionStatus.PendingVerification)
        {
            tx.Status = TransactionStatus.Rejected;
            await db.From<PaymentTransaction>().Update(tx);
            TempData["SuccessMessage"] = "Proof of payment rejected. The client can upload a valid POP from their portal.";
        }

        return RedirectToAction(nameof(Details), new { id = projectId });
    }

    [HttpGet]
    public async Task<IActionResult> ViewPop(Guid projectId, Guid transactionId)
    {
        var db = await _admin.GetAsync();
        var project = await db.From<Project>()
            .Where(p => p.Id == projectId && p.FreelancerId == CurrentUserId)
            .Single();

        if (project == null) return NotFound();

        var tx = await db.From<PaymentTransaction>()
            .Where(t => t.Id == transactionId && t.ProjectId == project.Id)
            .Single();

        if (tx == null || string.IsNullOrWhiteSpace(tx.PopFilePath)) return NotFound();

        var signedUrl = await db.Storage
            .From("proof-of-payments")
            .CreateSignedUrl(tx.PopFilePath, 300);

        return Redirect(signedUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid projectId)
    {
        var db = await _admin.GetAsync();
        var project = await db.From<Project>()
            .Where(p => p.Id == projectId && p.FreelancerId == CurrentUserId)
            .Single();

        if (project == null) return NotFound();

        if (project.Status != ProjectStatus.PaidInFull)
        {
            TempData["ErrorMessage"] = "Only projects that are Paid in full can be archived.";
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        project.IsArchived = true;
        project.ArchivedAt = DateTime.UtcNow;
        await db.From<Project>().Update(project);

        TempData["SuccessMessage"] = $"\"{project.Title}\" moved to Archive for safekeeping.";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unarchive(Guid projectId)
    {
        var db = await _admin.GetAsync();
        var project = await db.From<Project>()
            .Where(p => p.Id == projectId && p.FreelancerId == CurrentUserId)
            .Single();

        if (project == null) return NotFound();

        project.IsArchived = false;
        project.ArchivedAt = null;
        await db.From<Project>().Update(project);

        TempData["SuccessMessage"] = $"\"{project.Title}\" restored to your active projects list.";
        return RedirectToAction(nameof(Details), new { id = projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid projectId)
    {
        var db = await _admin.GetAsync();
        var project = await db.From<Project>()
            .Where(p => p.Id == projectId && p.FreelancerId == CurrentUserId)
            .Single();

        if (project == null) return NotFound();

        var sig = await db.From<QuoteSignature>().Where(s => s.ProjectId == project.Id).Single();
        bool canDelete = (project.IsArchived && project.Status == ProjectStatus.PaidInFull)
                         || (project.Status == ProjectStatus.Draft && sig == null && project.AmountPaid == 0);

        if (!canDelete)
        {
            TempData["ErrorMessage"] = "Archive completed projects first before permanently deleting, or delete only unsigned drafts.";
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        await db.From<Milestone>().Where(m => m.ProjectId == project.Id).Delete();
        await db.From<PaymentTransaction>().Where(t => t.ProjectId == project.Id).Delete();
        if (sig != null)
        {
            await db.From<QuoteSignature>().Where(s => s.Id == sig.Id).Delete();
        }
        await db.From<Project>().Where(p => p.Id == project.Id).Delete();

        TempData["SuccessMessage"] = $"Project \"{project.Title}\" ({project.ReferenceCode}) permanently deleted.";
        return RedirectToAction("Index", "Home");
    }

    private static string BuildSlug(string title)
    {
        var slugBase = Regex.Replace(title.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrEmpty(slugBase)) slugBase = "project";
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(10)).ToLowerInvariant();
        return $"{slugBase}-{token}";
    }

    private async Task<(string ReferenceCode, string Prefix)> GenerateReferencePreviewAsync()
    {
        var db = await _admin.GetAsync();
        var freelancer = await db.From<Freelancer>().Where(f => f.Id == CurrentUserId).Single();
        var existing = await db.From<Project>().Where(p => p.FreelancerId == CurrentUserId).Get();

        var biz = string.IsNullOrWhiteSpace(freelancer?.BusinessName) ? "KM" : freelancer.BusinessName.Trim();
        var parts = biz.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var prefix = parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpper()
            : biz[..Math.Min(2, biz.Length)].ToUpper();

        var nextNumber = existing.Models.Count + 1;
        return ($"{prefix}-{DateTime.UtcNow.Year}-{nextNumber:D3}", prefix);
    }
}
