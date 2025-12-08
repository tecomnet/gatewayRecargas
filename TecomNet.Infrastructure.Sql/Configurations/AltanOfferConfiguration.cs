using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecomNet.Domain.Models;

namespace TecomNet.Infrastructure.Sql.Configurations;

public class AltanOfferConfiguration : IEntityTypeConfiguration<AltanOffer>
{
    public void Configure(EntityTypeBuilder<AltanOffer> builder)
    {
        builder.ToTable("AltanOffers");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("Id")
            .IsRequired();

        builder.Property(a => a.CommercialName)
            .HasColumnName("CommercialName")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.IDOffer)
            .HasColumnName("IDOffer")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Price)
            .HasColumnName("Price")
            .HasColumnType("decimal(10,2)")
            .IsRequired();

        builder.Property(a => a.IsActive)
            .HasColumnName("IsActive")
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime2(7)")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("UpdatedAt")
            .HasColumnType("datetime2(7)")
            .IsRequired(false);

        builder.Property(a => a.MvnoId)
            .HasColumnName("MvnoId")
            .IsRequired(false);

        builder.HasOne(a => a.Mvno)
            .WithMany(m => m.AltanOffers)
            .HasForeignKey(a => a.MvnoId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}


