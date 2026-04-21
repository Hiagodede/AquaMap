using AquaMap.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AquaMap.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Reservoir> Reservoirs { get; set; }
        public DbSet<Neighborhood> Neighborhoods { get; set; }
        public DbSet<WaterAnalysis> WaterAnalyses { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasIndex(u => u.TaxId)
                .IsUnique();

            modelBuilder.Entity<Reservoir>()
                .HasMany(r => r.Neighborhoods)
                .WithOne(n => n.Reservoir)
                .HasForeignKey(n => n.ReservoirId);

            modelBuilder.Entity<Reservoir>()
                .HasMany(r => r.WaterAnalyses)
                .WithOne(wa => wa.Reservoir)
                .HasForeignKey(wa => wa.ReservoirId);
        }
    }
}