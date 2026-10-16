using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Login;

public sealed class LoginCommandHandler(
    IAuthUserRepository users,
    IPasswordHasher passwordHasher,
    IAuthTokenService tokenService,
    IClock clock)
{
    public async Task<LoginResponse?> HandleAsync(
        LoginRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrEmpty(request.Password))
        {
            return null;
        }

        var email = request.Email.Trim().ToLowerInvariant();

        var user = await users.GetByEmailAsync(email, ct);

        if (user is null ||
            !user.IsActive ||
            !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            // Use the same result for unknown users and incorrect passwords.
            return null;
        }

        var issuedToken = tokenService.IssueToken(
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            clock.UtcNow);

        return new LoginResponse(
            issuedToken.AccessToken,
            "Bearer",
            issuedToken.ExpiresAtUtc,
            new AuthenticatedUserDto(
                user.Id,
                user.Email,
                user.FullName,
                user.Role));
    }
}