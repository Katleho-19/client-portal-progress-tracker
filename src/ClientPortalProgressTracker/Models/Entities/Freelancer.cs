using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClientPortalProgressTracker.Models.Entities;

[Table("freelancers")]
public class Freelancer : BaseModel
{
    [PrimaryKey("id", true)] public Guid Id { get; set; }
    [Column("full_name")] public string FullName { get; set; } = string.Empty;
    [Column("business_name")] public string BusinessName { get; set; } = string.Empty;
    [Column("logo_url")] public string? LogoUrl { get; set; }
    [Column("phone_number")] public string PhoneNumber { get; set; } = string.Empty;
    [Column("email")] public string Email { get; set; } = string.Empty;
    [Column("bank_name")] public string? BankName { get; set; }
    [Column("account_holder")] public string? AccountHolder { get; set; }
    [Column("account_number")] public string? AccountNumber { get; set; }
    [Column("branch_code")] public string? BranchCode { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}