using System.IO;
using Microsoft.EntityFrameworkCore;
using OContabil.Models;

namespace OContabil.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Client> Clients { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<ChartOfAccount> Accounts { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OContabil", "ocontabil.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        options.UseSqlite($"Data Source={dbPath};Cache=Shared");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<Client>()
            .HasIndex(c => c.Cnpj)
            .IsUnique();

        modelBuilder.Entity<Document>()
            .HasOne(d => d.Client)
            .WithMany()
            .HasForeignKey(d => d.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Document>()
            .HasOne(d => d.UploadedBy)
            .WithMany()
            .HasForeignKey(d => d.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Document>()
            .HasOne(d => d.ValidatedBy)
            .WithMany()
            .HasForeignKey(d => d.ValidatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
