namespace SupportDesk.Application.Abstractions;

public sealed record IssuedAuthToken(
    string AccessToken,
    DateTime ExpiresAtUtc);

public interface IAuthTokenService
{
    IssuedAuthToken IssueToken(
        int userId,
        string email,
        string fullName,
        string role,
        DateTime nowUtc);
}