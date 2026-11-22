using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

public class ServiceCategory
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; }
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    [StringLength(50)]
    public string? Icon { get; set; }

    // JSON array of default project phases seeded into a job's JobPhases when a bid is
    // accepted (and the job has no phases yet). Admin-editable; empty/null falls back to
    // JobPhaseTemplate.GenericDefaultJson. See Services/JobPhaseTemplate.cs.
    [StringLength(4000)]
    public string? DefaultPhasesJson { get; set; }

    public int ServiceCount { get; set; } = 0;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
