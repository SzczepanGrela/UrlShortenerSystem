using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UrlShortener.Storage.Entities;

namespace UrlShortener.Storage
{
    public class UrlShortenerDbContext : DbContext
    {
        private readonly IConfiguration _configuration;

        public DbSet<Link> Links { get; set; }

        public UrlShortenerDbContext(IConfiguration configuration) : base()
        {
            _configuration = configuration;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var connectionString = _configuration.GetConnectionString("UrlShortener") 
                ?? @"server=(localdb)\MSSQLLocalDB;database=urlshortener-dev;trusted_connection=true;TrustServerCertificate=True;";
            
            options.UseSqlServer(
                connectionString,
                x => x.MigrationsHistoryTable("_EFMigrationsHistory", "UrlShortener"));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Dodatkowe konfiguracje jeśli potrzeba
            modelBuilder.Entity<Link>()
                .HasIndex(l => l.ShortCode)
                .IsUnique();
        }
    }
}