namespace SupportDesk.Presentation.Authentication;

public sealed record JwtAuthOptions(
    string Issuer,
    string Audience,
    string SigningKey,
    int ExpirationMinutes);