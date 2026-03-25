using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<CustomerEntity>
{
    public void Configure(EntityTypeBuilder<CustomerEntity> builder)
    {
        builder.ToTable("customers", "chronith");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.EmailEncrypted);

        builder.Property(c => c.EmailToken)
            .HasMaxLength(64);

        builder.Property(c => c.MobileEncrypted);

        builder.Property(c => c.PasswordHash);

        builder.Property(c => c.FirstName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.LastName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Mobile)
            .HasMaxLength(50);

        builder.Property(c => c.ExternalId)
            .HasMaxLength(200);

        builder.Property(c => c.AuthProvider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        // Unique email per tenant (soft-delete aware)
        builder.HasIndex(c => new { c.TenantId, c.Email })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("ix_customers_email");

        // Blind index for encrypted email lookup
        builder.HasIndex(c => new { c.TenantId, c.EmailToken })
            .HasFilter("\"EmailToken\" IS NOT NULL AND \"IsDeleted\" = false")
            .HasDatabaseName("ix_customers_email_token");

        // External ID lookup
        builder.HasIndex(c => new { c.TenantId, c.ExternalId })
            .HasFilter("\"ExternalId\" IS NOT NULL AND \"IsDeleted\" = false");

        builder.HasIndex(c => new { c.TenantId, c.IsDeleted });
    }
}
