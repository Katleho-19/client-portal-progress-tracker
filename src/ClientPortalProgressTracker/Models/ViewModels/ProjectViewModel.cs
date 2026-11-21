using System.ComponentModel.DataAnnotations;
using ClientPortalProgressTracker.Models.Entities;

namespace ClientPortalProgressTracker.Models.ViewModels;

public class DashboardViewModel
{
    public bool ShowArchived { get; set; }
    public int ActiveListCount { get; set; }
    public int ArchivedCount { get; set; }

    public int AwaitingDepositCount { get; set; }
    public decimal AwaitingDepositTotal { get; set; }
    public int ActiveProjectsCount { get; set; }
    public int PendingVerificationCount { get; set; }
    public int PaidInFullCount { get; set; }
    public decimal TotalReceivedAmount { get; set; }

    public Project? FirstPendingVerificationProject { get; set; }
    public List<DashboardProjectRow> Projects { get; set; } = new();
}

public class DashboardProjectRow
{
    public Project Project { get; set; } = null!;
    public bool HasPendingVerification { get; set; }
}

public class CreateProjectViewModel
{
    [Required(ErrorMessage = "Client name is required.")]
    [Display(Name = "Client name")]
    public string ClientName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Client email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid client email.")]
    [Display(Name = "Client email")]
    public string ClientEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Project title is required.")]
    [Display(Name = "Project title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Range(0, 100, ErrorMessage = "Deposit must be between 0% and 100%.")]
    [Display(Name = "Deposit %")]
    public int DepositPercentage { get; set; } = 50;

    [Display(Name = "Quote expiry (optional)")]
    [DataType(DataType.Date)]
    public DateTime? QuoteExpiresAt { get; set; }

    [MaxLength(1000, ErrorMessage = "Message cannot exceed 1,000 characters.")]
    [Display(Name = "Message to client (optional)")]
    public string? MessageToClient { get; set; }

    public List<MilestoneInputItem> Milestones { get; set; } = new()
    {
        new() { Title = "Discovery", Description = "Brief & wireframes", Amount = 1500m },
        new() { Title = "Design", Description = "UI mockups", Amount = 3500m },
        new() { Title = "Development", Description = "Build & QA", Amount = 5500m },
        new() { Title = "Launch", Description = "Go live", Amount = 1500m }
    };
}

public class EditProjectViewModel : CreateProjectViewModel
{
    public Guid Id { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
}

public class MilestoneInputItem
{
    public Guid? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    [Range(0, 10000000)]
    public decimal Amount { get; set; }
}
