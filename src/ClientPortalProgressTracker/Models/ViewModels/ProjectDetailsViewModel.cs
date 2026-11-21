using ClientPortalProgressTracker.Models.Entities;

namespace ClientPortalProgressTracker.Models.ViewModels
{
    public class ProjectDetailsViewModel
    {
        public Project Project { get; set; } = null!;
        public List<Milestone> Milestones { get; set; } = new();
        public List<PaymentTransaction> Transactions { get; set; } = new();
        public QuoteSignature? Signature { get; set; }
        public string PortalUrl { get; set; } = string.Empty;
        public decimal BalanceDue => Math.Max(0, Project.TotalAmount - Project.AmountPaid);
    }

}
