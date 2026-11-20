using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClientPortalProgressTracker.Models.Entities;

[Table("quote_signatures")]
public class QuoteSignature : BaseModel
{
    [PrimaryKey("id", true)] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("project_id")] public Guid ProjectId { get; set; }
    [Column("signer_full_name")] public string SignerFullName { get; set; } = string.Empty;
    [Column("agreed_to_terms")] public bool AgreedToTerms { get; set; }
    [Column("ip_address")] public string? IpAddress { get; set; }
    [Column("user_agent")] public string? UserAgent { get; set; }
    [Column("scope_hash")] public string? ScopeHash { get; set; }
    [Column("signed_at")] public DateTime SignedAt { get; set; } = DateTime.UtcNow;
}