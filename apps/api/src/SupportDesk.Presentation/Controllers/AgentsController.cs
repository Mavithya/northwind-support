using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Features.Agents.Queries.GetAgents;
using Microsoft.AspNetCore.Authorization;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Authorize]
[Route("api/agents")]
[Produces("application/json")]
public sealed class AgentsController(GetAgentsQueryHandler getAgents) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<AgentDto>> GetAll(CancellationToken ct) => getAgents.HandleAsync(ct);
}
