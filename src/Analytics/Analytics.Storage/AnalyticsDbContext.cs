using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Analytics.Storage.Entities;

namespace Analytics.Storage
{
    public class AnalyticsDbContext : DbContext
    {
        private readonly IConfiguration _configuration;

        public DbSet<Click> Clicks { get; set; }

        public AnalyticsDbContext(IConfiguration configuration) : base()
        {
            _configuration = configuration;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var connectionString = _configuration.GetConnectionString("Analytics") 
                ?? @"server=(localdb)\MSSQLLocalDB;database=analytics-dev;trusted_connection=true;TrustServerCertificate=True;";
            
            options.UseSqlServer(
                connectionString,
                x => x.MigrationsHistoryTable("_EFMigrationsHistory", "Analytics"));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Indeksy dla wydajności
            modelBuilder.Entity<Click>()
                .HasIndex(c => c.LinkId);

            modelBuilder.Entity<Click>()
                .HasIndex(c => c.Timestamp);
        }
    }
}