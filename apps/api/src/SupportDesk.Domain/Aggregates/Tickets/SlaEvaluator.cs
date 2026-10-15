
namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Calculates SLA status from a ticket's dates and the current time.
/// SLA status is derived and never stored.
/// </summary>
public static class SlaEvaluator
{
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime slaWindowStartedAtUtc,
        DateTime nowUtc,
        double atRiskThresholdPercent)
    {
        if (dueAtUtc is null)
        {
            return SlaStatus.NotApplicable;
        }

        var due = dueAtUtc.Value;

        if (resolvedAtUtc is not null)
        {
            return resolvedAtUtc.Value <= due
                ? SlaStatus.Met
                : SlaStatus.Breached;
        }

        if (nowUtc > due)
        {
            return SlaStatus.Breached;
        }

        var totalWindow = due - slaWindowStartedAtUtc;

        if (totalWindow <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The SLA window must have a positive duration.",
                nameof(slaWindowStartedAtUtc));
        }

        if (atRiskThresholdPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(atRiskThresholdPercent),
                "The threshold must be between 0 and 100.");
        }

        var remaining = due - nowUtc;

        var remainingPercent =
            remaining.TotalSeconds / totalWindow.TotalSeconds * 100;

        return remainingPercent <= atRiskThresholdPercent
            ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;
    }
}
