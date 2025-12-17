using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecomNet.Domain.Models;

namespace TecomNet.Infrastructure.Sql.Configurations;

public class MvnoConfiguration : IEntityTypeConfiguration<Mvno>
{
    public void Configure(EntityTypeBuilder<Mvno> builder)
    {
        // Nombre de la tabla
        builder.ToTable("Mvnos");

        // Clave primaria
        builder.HasKey(m => m.Id);

        // Configuración de columnas
        builder.Property(m => m.Id)
            .HasColumnName("Id")
            .IsRequired();

        builder.Property(m => m.BeId)
            .HasColumnName("BeId")
            .HasMaxLength(10)
            .IsRequired();

        // Índice para mejorar el rendimiento de búsquedas por BeId
        builder.HasIndex(m => m.BeId)
            .HasDatabaseName("IX_Mvnos_BeId");
    }
}















