
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Start = FixedClock.DefaultNow;

    private const double ThresholdPercent = 25;

    [Fact]
    public void Evaluate_WithoutDueDate_IsNotApplicable() =>
        Assert.Equal(
            SlaStatus.NotApplicable,
            SlaEvaluator.Evaluate(
                null, null, Start, Start, ThresholdPercent));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(
            SlaStatus.Met,
            SlaEvaluator.Evaluate(
                Start.AddHours(4),
                Start.AddHours(3),
                Start,
                Start.AddHours(3),
                ThresholdPercent));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(
            SlaStatus.Breached,
            SlaEvaluator.Evaluate(
                Start.AddHours(4),
                Start.AddHours(4).AddSeconds(1),
                Start,
                Start.AddHours(5),
                ThresholdPercent));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(
            SlaStatus.Breached,
            SlaEvaluator.Evaluate(
                Start.AddHours(4),
                null,
                Start,
                Start.AddHours(4).AddSeconds(1),
                ThresholdPercent));

    [Fact]
    public void Evaluate_OpenAndComfortablyBeforeDue_IsWithinSla() =>
        Assert.Equal(
            SlaStatus.WithinSla,
            SlaEvaluator.Evaluate(
                Start.AddHours(4),
                null,
                Start,
                Start.AddHours(2),
                ThresholdPercent));

    [Fact]
    public void Evaluate_Exactly25PercentRemaining_IsAtRisk() =>
        Assert.Equal(
            SlaStatus.AtRisk,
            SlaEvaluator.Evaluate(
                Start.AddHours(4),
                null,
                Start,
                Start.AddHours(3),
                ThresholdPercent));

    [Fact]
    public void Evaluate_MoreThan25PercentRemaining_IsWithinSla() =>
        Assert.Equal(
            SlaStatus.WithinSla,
            SlaEvaluator.Evaluate(
                Start.AddHours(4),
                null,
                Start,
                Start.AddHours(3).AddSeconds(-1),
                ThresholdPercent));

    [Fact]
    public void Evaluate_ExactlyAtDueTime_IsAtRiskNotBreached() =>
        Assert.Equal(
            SlaStatus.AtRisk,
            SlaEvaluator.Evaluate(
                Start.AddHours(4),
                null,
                Start,
                Start.AddHours(4),
                ThresholdPercent));
}
