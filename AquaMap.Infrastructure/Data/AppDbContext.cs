using AquaMap.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AquaMap.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        // Aqui dizemos quais tabelas queremos criar no banco
        public DbSet<User> Users { get; set; }
        public DbSet<CollectionPoint> CollectionPoints { get; set; }
        public DbSet<LabAnalysis> LabAnalyses { get; set; }

        // O construtor recebe as opções de configuração (como o caminho do arquivo do banco)
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            // Garante que o banco seja criado se não existir
            // Nota: Em apps reais grandes usamos "Migrations", mas para começar isso resolve
            Database.EnsureCreated();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configurações extras se necessário

            // Exemplo: Garantir que o CPF do usuário seja único
            modelBuilder.Entity<User>()
                .HasIndex(u => u.TaxId)
                .IsUnique();

            // Configura o relacionamento 1 para Muitos (CollectionPoint -> LabAnalyses)
            modelBuilder.Entity<CollectionPoint>()
                .HasMany(p => p.AnalysisHistory)
                .WithOne()
                .HasForeignKey(a => a.CollectionPointId);
        }
    }
}