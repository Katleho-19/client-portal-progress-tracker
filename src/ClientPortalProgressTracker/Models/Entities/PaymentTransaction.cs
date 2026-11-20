using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClientPortalProgressTracker.Models.Entities;

[Table("transactions")]
public class PaymentTransaction : BaseModel
{
    [PrimaryKey("id", true)] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("project_id")] public Guid ProjectId { get; set; }
    [Column("payment_method")] public string PaymentMethod { get; set; } = string.Empty;
    [Column("external_reference")] public string? ExternalReference { get; set; }
    [Column("pop_file_path")] public string? PopFilePath { get; set; }
    [Column("amount")] public decimal Amount { get; set; }
    [Column("payment_type")] public string PaymentType { get; set; } = string.Empty;
    [Column("status")] public string Status { get; set; } = TransactionStatus.PendingVerification;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("paid_at")] public DateTime? PaidAt { get; set; }
    [Column("verified_at")] public DateTime? VerifiedAt { get; set; }
}