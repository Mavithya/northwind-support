using SupportDesk.Domain.Aggregates.Users;

namespace SupportDesk.Domain.Repositories;

public interface IAuthUserRepository
{
    Task<SupportUser?> GetByEmailAsync(
        string email,
        CancellationToken ct);

    Task AddAsync(
        SupportUser user,
        CancellationToken ct);
}