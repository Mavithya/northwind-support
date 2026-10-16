using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Users;

namespace SupportDesk.Infrastructure.Data.Configurations;

public sealed class SupportUserConfiguration
    : IEntityTypeConfiguration<SupportUser>
{
    public void Configure(EntityTypeBuilder<SupportUser> builder)
    {
        builder.ToTable("SupportUsers");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(user => user.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(user => user.Role)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(user => user.IsActive)
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc)
            .HasColumnType("datetime2(3)");

        builder.HasIndex(user => user.Email)
            .IsUnique()
            .HasDatabaseName("UQ_SupportUsers_Email");
    }
}