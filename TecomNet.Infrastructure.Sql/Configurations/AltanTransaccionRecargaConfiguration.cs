using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecomNet.Domain.Models;

namespace TecomNet.Infrastructure.Sql.Configurations;

public class AltanTransaccionRecargaConfiguration : IEntityTypeConfiguration<AltanTransaccionRecarga>
{
    public void Configure(EntityTypeBuilder<AltanTransaccionRecarga> builder)
    {
        builder.ToTable("AltanTransaccionesRecargas");

        // Clave primaria
        builder.HasKey(t => t.IdTransaccion);

        // Configuración de IdTransaccion
        builder.Property(t => t.IdTransaccion)
            .HasColumnName("IdTransaccion")
            .IsRequired()
            .ValueGeneratedOnAdd(); // IDENTITY column

        // Campos de timestamps de transacción principal
        builder.Property(t => t.InicioTransaccionCanalDeVenta)
            .HasColumnName("InicioTransaccionCanalDeVenta")
            .HasColumnType("datetime2(0)")
            .IsRequired();

        builder.Property(t => t.InicioTransaccionAltan)
            .HasColumnName("InicioTransaccionAltan")
            .HasColumnType("datetime2(0)")
            .IsRequired();

        builder.Property(t => t.FinTransaccionAltan)
            .HasColumnName("FinTransaccionAltan")
            .HasColumnType("datetime2(0)")
            .IsRequired(false);

        builder.Property(t => t.FinTransaccionCanalDeVenta)
            .HasColumnName("FinTransaccionCanalDeVenta")
            .HasColumnType("datetime2(0)")
            .IsRequired(false);

        // Campos principales de la transacción
        builder.Property(t => t.BE)
            .HasColumnName("BE")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(t => t.MSISDN)
            .HasColumnName("MSISDN")
            .HasMaxLength(15)
            .IsRequired();

        builder.Property(t => t.MontoRecarga)
            .HasColumnName("MontoRecarga")
            .HasColumnType("decimal(10,2)")
            .IsRequired();

        builder.Property(t => t.OfferId)
            .HasColumnName("OfferId")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.CanalDeVenta)
            .HasColumnName("CanalDeVenta")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.Medio)
            .HasColumnName("Medio")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.IdPOS)
            .HasColumnName("IdPOS")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.OrderId)
            .HasColumnName("OrderId")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.ResultadoTransaccion)
            .HasColumnName("ResultadoTransaccion")
            .HasMaxLength(20)
            .IsRequired();

        // Campos de reversa
        builder.Property(t => t.AplicaReverso)
            .HasColumnName("AplicaReverso")
            .IsRequired(false);

        builder.Property(t => t.InicioTransaccionReversa)
            .HasColumnName("InicioTransaccionReversa")
            .HasColumnType("datetime2(0)")
            .IsRequired(false);

        builder.Property(t => t.InicioTransaccionReversaAltan)
            .HasColumnName("InicioTransaccionReversaAltan")
            .HasColumnType("datetime2(0)")
            .IsRequired(false);

        builder.Property(t => t.FinTransaccionReversaAltan)
            .HasColumnName("FinTransaccionReversaAltan")
            .HasColumnType("datetime2(0)")
            .IsRequired(false);

        builder.Property(t => t.FinTransaccionReversa)
            .HasColumnName("FinTransaccionReversa")
            .HasColumnType("datetime2(0)")
            .IsRequired(false);

        builder.Property(t => t.OrderIdReversa)
            .HasColumnName("OrderIdReversa")
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(t => t.ResultadoTransaccionReversa)
            .HasColumnName("ResultadoTransaccionReversa")
            .HasMaxLength(20)
            .IsRequired(false);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime2(0)")
            .IsRequired();

        // Índices para mejorar el rendimiento de búsquedas comunes
        builder.HasIndex(t => t.MSISDN)
            .HasDatabaseName("IX_AltanTransaccionesRecargas_MSISDN");

        builder.HasIndex(t => t.OrderId)
            .HasDatabaseName("IX_AltanTransaccionesRecargas_OrderId");

        builder.HasIndex(t => t.CreatedAt)
            .HasDatabaseName("IX_AltanTransaccionesRecargas_CreatedAt");
    }
}
