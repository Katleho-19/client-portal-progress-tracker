using System.Security.Claims;
using ClientPortalProgressTracker.Models.Entities;
using ClientPortalProgressTracker.Models.ViewModels;
using ClientPortalProgressTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static Supabase.Postgrest.Constants;

namespace ClientPortalProgressTracker.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly SupabaseAdmin _admin;

    public HomeController(SupabaseAdmin admin)
    {
        _admin = admin;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index(bool archived = false)
    {
        var db = await _admin.GetAsync();

        var projectsResponse = await db.From<Project>()
            .Where(p => p.FreelancerId == CurrentUserId)
            .Order("created_at", Ordering.Descending)
            .Get();

        var allProjects = projectsResponse.Models;
        var projectIds = allProjects.Select(p => p.Id).ToList();

        var pendingProjectIds = new HashSet<Guid>();
        if (projectIds.Count > 0)
        {
            var txResponse = await db.From<PaymentTransaction>()
                .Where(t => t.Status == TransactionStatus.PendingVerification)
                .Get();

            foreach (var tx in txResponse.Models.Where(t => projectIds.Contains(t.ProjectId)))
            {
                pendingProjectIds.Add(tx.ProjectId);
            }
        }

        var visibleProjects = allProjects
            .Where(p => archived ? p.IsArchived : !p.IsArchived)
            .Select(p => new DashboardProjectRow
            {
                Project = p,
                HasPendingVerification = pendingProjectIds.Contains(p.Id)
            })
            .ToList();

        var awaiting = allProjects.Where(p => !p.IsArchived && p.Status == ProjectStatus.AwaitingDeposit).ToList();
        var active = allProjects.Where(p => !p.IsArchived && (p.Status == ProjectStatus.Active || p.Status == ProjectStatus.CompletedUnpaid)).ToList();
        var paidInFull = allProjects.Where(p => p.Status == ProjectStatus.PaidInFull).ToList();

        var vm = new DashboardViewModel
        {
            ShowArchived = archived,
            ActiveListCount = allProjects.Count(p => !p.IsArchived),
            ArchivedCount = allProjects.Count(p => p.IsArchived),
            AwaitingDepositCount = awaiting.Count,
            AwaitingDepositTotal = awaiting.Sum(p => p.TotalAmount),
            ActiveProjectsCount = active.Count,
            PendingVerificationCount = pendingProjectIds.Count,
            PaidInFullCount = paidInFull.Count,
            TotalReceivedAmount = allProjects.Sum(p => p.AmountPaid),
            FirstPendingVerificationProject = visibleProjects.FirstOrDefault(r => r.HasPendingVerification)?.Project,
            Projects = visibleProjects
        };

        return View(vm);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Terms() => View();

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Privacy() => View();
}
