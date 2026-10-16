using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Users;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AuthUserRepository(SupportDbContext db)
    : IAuthUserRepository
{
    public Task<SupportUser?> GetByEmailAsync(
        string email,
        CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        return db.Set<SupportUser>()
            .FirstOrDefaultAsync(
                user => user.Email == normalizedEmail,
                ct);
    }

    public async Task AddAsync(
        SupportUser user,
        CancellationToken ct)
    {
        await db.Set<SupportUser>().AddAsync(user, ct);
    }
}