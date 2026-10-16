namespace SupportDesk.Application.Contracts.Auth;

public sealed record LoginRequest(
    string? Email,
    string? Password);

public sealed record AuthenticatedUserDto(
    int Id,
    string Email,
    string FullName,
    string Role);

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    AuthenticatedUserDto User);