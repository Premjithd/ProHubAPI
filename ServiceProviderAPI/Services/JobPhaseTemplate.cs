using System.Text.Json;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Services;

/// <summary>
/// Seeds a job's project phases from a (category-specific or generic) template and
/// auto-ticks the phases that map to lifecycle events we already track — so a pro no
/// longer has to build the phase list or tick "Payment"/"Job Execution" by hand.
///
/// The phase JSON shape matches <see cref="JobPhaseItem"/> (and the frontend JobPhase):
/// { id, title, description, isCompleted, completedAt }. Phases are copied *into* the
/// job's own JobPhases, so editing a category template later never rewrites live jobs,
/// and the pro can still override via PUT /api/jobs/{id}/phases.
/// </summary>
public static class JobPhaseTemplate
{
    // Phase ids the templates use (generic default below + admin-authored category templates).
    public const string InitialConsultationPhaseId = "initialconsultation";
    public const string PaymentPhaseId = "payment";
    public const string ExecutionPhaseId = "execution";

    // Lifecycle rank — higher means further along. Used to decide which phases to auto-tick.
    private static int StatusRank(string? status) => status switch
    {
        "Open" => 0,
        "Bid Accepted" => 1,
        "Payment Made" => 2,
        "Pro Confirmed" => 3,
        "In Progress" => 4,
        "Completion Submitted" => 5,
        "Completed" => 6,
        _ => 0
    };

    /// <summary>Fallback template used when a category has no DefaultPhasesJson.</summary>
    public static string GenericDefaultJson { get; } = JsonSerializer.Serialize(new[]
    {
        new JobPhaseItem { Id = "initialconsultation", Title = "Initial Consultation", Description = "Discuss requirements, scope, budget, and timeline" },
        new JobPhaseItem { Id = PaymentPhaseId,       Title = "Payment",              Description = "Received payment for the job" },
        new JobPhaseItem { Id = ExecutionPhaseId,     Title = "Job Execution",        Description = "Perform the work according to agreed plan and timeline" },
        new JobPhaseItem { Id = "inspection",         Title = "Quality Inspection",   Description = "Verify work quality and ensure all requirements are met" },
        new JobPhaseItem { Id = "delivery",           Title = "Final Delivery",       Description = "Complete delivery, documentation, and project closure" }
    });

    /// <summary>
    /// Ensures <paramref name="job"/> has phases (seeding from the category template or the
    /// generic default when empty) and auto-ticks the phases implied by the current status.
    /// Mutates <c>job.JobPhases</c> and returns true if anything changed (so the caller can
    /// decide whether to persist).
    /// </summary>
    public static bool ApplyTemplateAndAutoTicks(Job job, ServiceCategory? category)
    {
        var before = job.JobPhases;

        var templateJson = !string.IsNullOrWhiteSpace(category?.DefaultPhasesJson)
            ? category!.DefaultPhasesJson!
            : GenericDefaultJson;

        List<JobPhaseItem> phases;
        if (string.IsNullOrWhiteSpace(job.JobPhases))
        {
            // Only seed once a pro is actually assigned — an unassigned/open job has no
            // owner to track progress against yet.
            if (job.AssignedProId is null or 0)
                return false;

            phases = Deserialize(templateJson);
            if (phases.Count == 0)
                return false;

            // A fresh template starts un-ticked, and every seeded phase is a locked default
            // (the pro can add more phases later but may not remove these).
            foreach (var p in phases)
            {
                p.IsCompleted = false;
                p.CompletedAt = null;
                p.IsDefault = true;
            }
        }
        else
        {
            phases = Deserialize(job.JobPhases);
            if (phases.Count == 0)
                return false;

            // Backfill the locked-default flag for jobs seeded before IsDefault existed:
            // any phase whose id belongs to the (category or generic) template is a default.
            var templateIds = Deserialize(templateJson)
                .Where(p => p.Id != null)
                .Select(p => p.Id!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var p in phases)
            {
                if (!p.IsDefault && p.Id != null && templateIds.Contains(p.Id))
                    p.IsDefault = true;
            }
        }

        ApplyAutoTicks(phases, job.Status);

        var after = JsonSerializer.Serialize(phases);
        if (after == before)
            return false;

        job.JobPhases = after;
        return true;
    }

    private static void ApplyAutoTicks(List<JobPhaseItem> phases, string? status)
    {
        var rank = StatusRank(status);
        // Once payment is in, the job has been consulted on and paid for — complete both the
        // Initial Consultation and Payment phases. Nothing else is auto-ticked (e.g. Job
        // Execution stays manual: "In Progress" means work has started, not finished).
        if (rank >= StatusRank("Payment Made"))
        {
            MarkComplete(phases, InitialConsultationPhaseId);
            MarkComplete(phases, PaymentPhaseId);
        }
    }

    private static void MarkComplete(List<JobPhaseItem> phases, string id)
    {
        var phase = phases.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        if (phase is { IsCompleted: false })
        {
            phase.IsCompleted = true;
            phase.CompletedAt = DateTime.UtcNow.ToString("O");
        }
    }

    private static List<JobPhaseItem> Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<JobPhaseItem>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch
        {
            return new();
        }
    }
}

/// <summary>
/// Serialized shape of a single project phase. Stored (and re-serialized) as PascalCase —
/// matching the existing JobPhases JSON — which the frontend reads case-insensitively.
/// </summary>
public class JobPhaseItem
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public string? CompletedAt { get; set; }
    // True for template-seeded phases the pro may not remove (they can still add more).
    public bool IsDefault { get; set; }
}
