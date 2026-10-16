using System.Globalization;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Presentation.Authentication;

public sealed class JwtAuthTokenService(JwtAuthOptions options)
    : IAuthTokenService
{
    public IssuedAuthToken IssueToken(
        int userId,
        string email,
        string fullName,
        string role,
        DateTime nowUtc)
    {
        var expiresAtUtc = nowUtc.AddMinutes(options.ExpirationMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            NotBefore = nowUtc,
            IssuedAt = nowUtc,
            Expires = expiresAtUtc,
            Subject = new ClaimsIdentity(
            [
                new Claim(
                    JwtRegisteredClaimNames.Sub,
                    userId.ToString(CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Name, fullName),
                new Claim("role", role)
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(options.SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(descriptor);

        return new IssuedAuthToken(token, expiresAtUtc);
    }
}