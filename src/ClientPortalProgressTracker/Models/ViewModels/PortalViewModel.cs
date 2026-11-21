using ClientPortalProgressTracker.Models.Entities;

namespace ClientPortalProgressTracker.Models.ViewModels;

public class PortalViewModel
{
    public Project Project { get; set; } = null!;
    public Freelancer Freelancer { get; set; } = null!;
    public List<Milestone> Milestones { get; set; } = new();
    public QuoteSignature? Signature { get; set; }
    public List<PaymentTransaction> Transactions { get; set; } = new();

    // An unsigned quote past its expiry date can no longer be signed
    public bool IsExpired =>
        Project.Status == ProjectStatus.Draft
        && Project.QuoteExpiresAt.HasValue
        && Project.QuoteExpiresAt.Value.Date < DateTime.UtcNow.Date;

    public bool HasPrices => Milestones.Any(m => m.Amount.GetValueOrDefault() > 0);

    public decimal DepositAmount =>
        Math.Round(Project.TotalAmount * Project.DepositPercentage / 100m, 2);

    public decimal BalanceDue => Math.Max(0, Project.TotalAmount - Project.AmountPaid);

    // What the client has to pay right now
    public decimal AmountDueNow =>
        Project.Status == ProjectStatus.CompletedUnpaid
            ? BalanceDue
            : Math.Max(0, DepositAmount - Project.AmountPaid);

    public string PaymentTypeDue =>
        Project.Status == ProjectStatus.CompletedUnpaid
            ? PaymentType.FinalBalance
            : (Project.DepositPercentage >= 100 ? PaymentType.FullPayment : PaymentType.Deposit);

    public string PaymentLabelDue =>
        PaymentTypeDue == PaymentType.FinalBalance ? "Final balance due"
        : PaymentTypeDue == PaymentType.FullPayment ? "Full payment due"
        : $"Deposit due now ({Project.DepositPercentage}%)";

    public PaymentTransaction? PendingEft =>
        Transactions.FirstOrDefault(t =>
            t.Status == TransactionStatus.PendingVerification && t.PaymentMethod == PaymentMethod.ManualEft);

    // A rejected proof that has not been replaced yet
    public PaymentTransaction? RejectedProof =>
        PendingEft != null ? null : Transactions.LastOrDefault(t => t.Status == TransactionStatus.Rejected);

    public int CompletedCount => Milestones.Count(m => m.Status == MilestoneStatus.Completed);

    public int ProgressPercent =>
        Milestones.Count == 0 ? 0 : (int)Math.Round(100.0 * CompletedCount / Milestones.Count);

    public string Initials
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(Freelancer.BusinessName) ? Freelancer.FullName : Freelancer.BusinessName;
            var parts = (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "KM";
            return parts.Length == 1
                ? parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant()
                : $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
        }
    }
}