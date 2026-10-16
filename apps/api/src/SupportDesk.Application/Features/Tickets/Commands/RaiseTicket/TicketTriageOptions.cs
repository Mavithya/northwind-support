
namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Configurable rules used when triaging a ticket.
/// Values are loaded from the Sla section of appsettings.
/// </summary>
public sealed class TicketTriageOptions
{
    public Dictionary<string, double> BaseWindowHours { get; set; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Low"] = 72,
            ["Medium"] = 24,
            ["High"] = 8,
            ["Critical"] = 4
        };

    public double PremiumCustomerMultiplier { get; set; } = 0.5;

    public double MinimumWindowHours { get; set; } = 1;

    public double AtRiskThresholdPercent { get; set; } = 25;
}
