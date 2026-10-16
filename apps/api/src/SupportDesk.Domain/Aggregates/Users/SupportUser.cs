using SupportDesk.Domain.Common;

namespace SupportDesk.Domain.Aggregates.Users;

/// <summary>
/// An application account that can authenticate to the support desk.
/// </summary>
public sealed class SupportUser : AggregateRoot
{
    private SupportUser()
    {
        // For EF Core.
    }

    public SupportUser(
        string email,
        string fullName,
        string passwordHash,
        string role,
        DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        if (role is not ("Admin" or "SupportAgent"))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        Email = email.Trim().ToLowerInvariant();
        FullName = fullName.Trim();
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public string Email { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string Role { get; private set; } = "SupportAgent";

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public void Deactivate() => IsActive = false;
}