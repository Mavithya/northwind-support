
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private SupportDbContext _db = null!;
    private TicketQueries _queries = null!;

    private Customer _contoso = null!;
    private Customer _fabrikam = null!;
    private Category _general = null!;
    private Category _billing = null!;
    private Agent _alex = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new SupportDbContext(options);

        await _db.Database.EnsureCreatedAsync();

        var now = FixedClock.DefaultNow;

        _contoso = new Customer(
            "Contoso",
            "support@contoso.example",
            null,
            CustomerTier.Premium,
            now);

        _fabrikam = new Customer(
            "Fabrikam",
            "support@fabrikam.example",
            null,
            CustomerTier.Standard,
            now);

        _general = new Category("General", false, false);
        _billing = new Category("Billing", true, false);

        _alex = new Agent(
            "Alex Turner",
            "alex@example.com",
            10,
            now);

        _db.Set<Customer>().AddRange(_contoso, _fabrikam);
        _db.Set<Category>().AddRange(_general, _billing);
        _db.Set<Agent>().Add(_alex);

        await _db.SaveChangesAsync();

        var highInProgress = Ticket.Raise(
            "TCK-0001",
            "Cannot access dashboard",
            "The dashboard does not load correctly.",
            _contoso.Id,
            _general.Id,
            TicketPriority.High,
            now.AddMinutes(-60));

        highInProgress.AssignTo(_alex.Id, now.AddMinutes(-55));
        highInProgress.ChangeStatus(
            TicketStatus.InProgress,
            now.AddMinutes(-50));

        var highNew = Ticket.Raise(
            "TCK-0002",
            "Invoice shows incorrect amount",
            "The invoice total is incorrect.",
            _fabrikam.Id,
            _billing.Id,
            TicketPriority.High,
            now.AddMinutes(-45));

        var mediumInProgress = Ticket.Raise(
            "TCK-0003",
            "Cannot export report",
            "Report export fails.",
            _contoso.Id,
            _general.Id,
            TicketPriority.Medium,
            now.AddMinutes(-30));

        mediumInProgress.ChangeStatus(
            TicketStatus.InProgress,
            now.AddMinutes(-25));

        var criticalNew = Ticket.Raise(
            "TCK-0004",
            "Urgent security review",
            "Please review this urgent issue.",
            _fabrikam.Id,
            _general.Id,
            TicketPriority.Critical,
            now.AddMinutes(-15));

        _db.Set<Ticket>().AddRange(
            highInProgress,
            highNew,
            mediumInProgress,
            criticalNew);

        await _db.SaveChangesAsync();

        _queries = new TicketQueries(_db, new FixedClock(now));
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Priority_filter_returns_exact_matches()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                Priority = TicketPriority.High,
                PageSize = 100
            },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);

        Assert.All(result.Items, ticket =>
            Assert.Equal(TicketPriority.High, ticket.Priority));
    }

    [Fact]
    public async Task Status_and_priority_filters_combine_with_and()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                Status = TicketStatus.InProgress,
                Priority = TicketPriority.High,
                PageSize = 100
            },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);

        Assert.Equal(
            new[] { "TCK-0001" },
            result.Items.Select(ticket => ticket.Reference));
    }

    [Fact]
    public async Task Search_matches_customer_name_and_trims_whitespace()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                Search = " Contoso ",
                PageSize = 100
            },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);

        Assert.All(result.Items, ticket =>
            Assert.Equal("Contoso", ticket.Customer.Name));
    }

    [Fact]
    public async Task Status_filter_returns_only_matching_status()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                Status = TicketStatus.InProgress,
                PageSize = 100
            },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);

        Assert.All(result.Items, ticket =>
            Assert.Equal(TicketStatus.InProgress, ticket.Status));
    }

    [Fact]
    public async Task Category_filter_returns_exact_matches()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                CategoryId = _billing.Id,
                PageSize = 100
            },
            CancellationToken.None);

        Assert.Equal(
            new[] { "TCK-0002" },
            result.Items.Select(ticket => ticket.Reference));
    }

    [Fact]
    public async Task Customer_filter_returns_exact_matches()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                CustomerId = _contoso.Id,
                PageSize = 100
            },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);

        Assert.All(result.Items, ticket =>
            Assert.Equal("Contoso", ticket.Customer.Name));
    }

    [Fact]
    public async Task Assigned_agent_filter_returns_exact_matches()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                AssignedAgentId = _alex.Id,
                PageSize = 100
            },
            CancellationToken.None);

        Assert.Equal(
            new[] { "TCK-0001" },
            result.Items.Select(ticket => ticket.Reference));
    }

    [Fact]
    public async Task Unassigned_only_returns_tickets_without_an_agent()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                UnassignedOnly = true,
                PageSize = 100
            },
            CancellationToken.None);

        Assert.Equal(3, result.TotalCount);

        Assert.All(result.Items, ticket =>
            Assert.Null(ticket.AssignedAgent));
    }

    [Theory]
    [InlineData("Invoice")]
    [InlineData("TCK-0002")]
    public async Task Search_matches_title_or_reference(string search)
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                Search = search,
                PageSize = 100
            },
            CancellationToken.None);

        Assert.Equal(
            new[] { "TCK-0002" },
            result.Items.Select(ticket => ticket.Reference));
    }

    [Fact]
    public async Task Blank_search_is_ignored_and_paging_counts_filtered_rows()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                Search = "   ",
                Page = 1,
                PageSize = 2
            },
            CancellationToken.None);

        Assert.Equal(4, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalPages);
    }
}
