using Microsoft.EntityFrameworkCore;
using TecomNet.Domain.Models;

namespace TecomNet.Infrastructure.Sql;

public class TecomNetDbContext : DbContext
{
    public TecomNetDbContext(DbContextOptions<TecomNetDbContext> options) : base(options)
    {
    }

    public DbSet<AltanOffer> AltanOffers { get; set; }
    public DbSet<AltanTransaccionRecarga> AltanTransaccionesRecargas { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Aplicar configuraciones desde el assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TecomNetDbContext).Assembly);
    }
}

