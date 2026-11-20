using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClientPortalProgressTracker.Models.Entities;

[Table("milestones")]
public class Milestone : BaseModel
{
    [PrimaryKey("id", true)] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("project_id")] public Guid ProjectId { get; set; }
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("description")] public string? Description { get; set; }
    [Column("sort_order")] public int SortOrder { get; set; } = 1;
    [Column("amount")] public decimal? Amount { get; set; }
    [Column("status")] public string Status { get; set; } = MilestoneStatus.Pending;
    [Column("completed_at")] public DateTime? CompletedAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}