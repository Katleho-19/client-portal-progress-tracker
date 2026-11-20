using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClientPortalProgressTracker.Models.Entities;

[Table("projects")]
public class Project : BaseModel
{
    [PrimaryKey("id", true)] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("freelancer_id")] public Guid FreelancerId { get; set; }
    [Column("reference_code")] public string ReferenceCode { get; set; } = string.Empty;
    [Column("portal_slug")] public string PortalSlug { get; set; } = string.Empty;
    [Column("is_link_active")] public bool IsLinkActive { get; set; } = true;
    [Column("quote_expires_at")] public DateTime? QuoteExpiresAt { get; set; }
    [Column("client_name")] public string ClientName { get; set; } = string.Empty;
    [Column("client_email")] public string ClientEmail { get; set; } = string.Empty;
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("total_amount")] public decimal TotalAmount { get; set; }
    [Column("deposit_percentage")] public int DepositPercentage { get; set; }
    [Column("amount_paid")] public decimal AmountPaid { get; set; }
    [Column("status")] public string Status { get; set; } = ProjectStatus.Draft;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}