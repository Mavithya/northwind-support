
using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Common;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public sealed class TicketTriageServiceTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public async Task MissingRequestedPriority_DefaultsToMedium()
    {
        var category = NewCategory("General");
        var (service, _) = CreateService(
            CustomerTier.Standard, category);

        var result = await service.DecideAsync(
            1, category.Id, null, CancellationToken.None);

        Assert.Equal(TicketPriority.Medium, result.Priority);
        Assert.Equal(Now.AddHours(24), result.DueAtUtc);
        Assert.Null(result.AssignedAgentId);
    }

    [Fact]
    public async Task ForcedCriticalCategory_OverridesRequestedPriority()
    {
        var category = NewCategory(
            "Security",
            requiresSpecialist: true,
            forcesCriticalPriority: true);

        var (service, _) = CreateService(
            CustomerTier.Standard, category);

        var result = await service.DecideAsync(
            1, category.Id, TicketPriority.Low, CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, result.Priority);
        Assert.Equal(Now.AddHours(4), result.DueAtUtc);
        Assert.Contains("Critical", result.PriorityReason);
    }

    [Fact]
    public async Task PremiumCustomer_GetsHalfSlaWindow()
    {
        var category = NewCategory("General");

        var (service, _) = CreateService(
            CustomerTier.Premium, category);

        var result = await service.DecideAsync(
            1, category.Id, TicketPriority.High, CancellationToken.None);

        Assert.Equal(4, result.SlaWindowHours);
        Assert.Equal(Now.AddHours(4), result.DueAtUtc);
        Assert.Contains("Premium", result.SlaReason);
    }

    [Fact]
    public async Task Assignment_SelectsEligibleAgentWithFewestOpenTickets()
    {
        var category = NewCategory(
            "Billing", requiresSpecialist: true);

        var alex = NewAgent(101, "Alex", 8, category.Id);
        var priya = NewAgent(102, "Priya", 8, category.Id);

        var agents = new[] { alex, priya };

        var counts = new Dictionary<int, int>
        {
            [101] = 3,
            [102] = 1
        };

        var (service, _) = CreateService(
            CustomerTier.Standard,
            category,
            agents,
            counts);

        var result = await service.DecideAsync(
            1, category.Id, TicketPriority.High, CancellationToken.None);

        Assert.Equal(102, result.AssignedAgentId);
        Assert.Contains("Priya", result.AssignmentReason);
    }

    [Fact]
    public async Task NoEligibleAgent_LeavesTicketUnassignedWithReason()
    {
        var category = NewCategory(
            "Security",
            requiresSpecialist: true,
            forcesCriticalPriority: true);

        var inactive = NewAgent(101, "Inactive Agent", 10, category.Id);
        inactive.Deactivate();

        var full = NewAgent(102, "Full Agent", 1, category.Id);

        var wrongSpecialization = NewAgent(
            103, "General Agent", 10, category.Id + 1);

        var agents = new[] { inactive, full, wrongSpecialization };

        var counts = new Dictionary<int, int>
        {
            [102] = 1
        };

        var (service, _) = CreateService(
            CustomerTier.Standard, category, agents, counts);

        var result = await service.DecideAsync(
            1, category.Id, TicketPriority.High, CancellationToken.None);

        Assert.Null(result.AssignedAgentId);
        Assert.Contains("unassigned", result.AssignmentReason);
    }

    private static Category NewCategory(
        string name,
        bool requiresSpecialist = false,
        bool forcesCriticalPriority = false)
    {
        var category = new Category(
            name, requiresSpecialist, forcesCriticalPriority);

        SetId(category, 10);

        return category;
    }

    private static Agent NewAgent(
        int id,
        string name,
        int maxOpenTickets,
        int categoryId)
    {
        var agent = new Agent(
            name, $"{name.ToLowerInvariant()}@example.com",
            maxOpenTickets, Now);

        SetId(agent, id);
        agent.AddSpecialization(categoryId);

        return agent;
    }

    private static void SetId(Entity entity, int id) =>
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(entity, id);

    private static (
        TicketTriageService Service,
        Mock<ITicketRepository> Tickets)
        CreateService(
            CustomerTier tier,
            Category category,
            IReadOnlyList<Agent>? availableAgents = null,
            IReadOnlyDictionary<int, int>? openCounts = null)
    {
        var customer = new Customer(
            "Test Customer",
            "customer@example.com",
            null,
            tier,
            Now);

        SetId(customer, 1);

        var customers = new Mock<ICustomerRepository>();
        customers
            .Setup(x => x.GetByIdAsync(
                1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        var categories = new Mock<ICategoryRepository>();
        categories
            .Setup(x => x.GetByIdAsync(
                category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        var agents = new Mock<IAgentRepository>();
        agents
            .Setup(x => x.GetAllForAssignmentAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                availableAgents ?? Array.Empty<Agent>());

        var tickets = new Mock<ITicketRepository>();
        tickets
            .Setup(x => x.GetOpenTicketCountsByAgentAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                openCounts ?? new Dictionary<int, int>());

        IClock clock = new FixedClock(Now);

        var service = new TicketTriageService(
            tickets.Object,
            customers.Object,
            categories.Object,
            agents.Object,
            clock,
            new TicketTriageOptions());

        return (service, tickets);
    }
}
