using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OContabil.Models;

namespace OContabil.Data;

public static class DbInitializer
{
    public static void Initialize(AppDbContext db)
    {
        db.Database.EnsureCreated();
        
        // Enable Write-Ahead Logging for extremely robust multi-threading in SQLite
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");


        // Fallback for existing databases that don't have the new Phase 6/7 tables & columns
        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""Accounts"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Accounts"" PRIMARY KEY AUTOINCREMENT,
                    ""Code"" TEXT NOT NULL,
                    ""Description"" TEXT NOT NULL,
                    ""Type"" TEXT NOT NULL,
                    ""AutoClassificationRegex"" TEXT NULL
                );
            ");
        }
        catch { }

        try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Documents\" ADD COLUMN \"ChartOfAccountId\" INTEGER NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Documents\" ADD COLUMN \"FilePath\" TEXT NULL;"); } catch { }

        // Seed default Chart of Accounts
        if (!db.Accounts.Any())
        {
            db.Accounts.AddRange(
                new ChartOfAccount { Code = "3.1.1.01", Description = "Energia Elétrica", Type = "Despesa", AutoClassificationRegex = @"(?i)\b(enel|cemig|elektro|cpfl|light|copel|energia)\b" },
                new ChartOfAccount { Code = "3.1.1.02", Description = "Água e Esgoto", Type = "Despesa", AutoClassificationRegex = @"(?i)\b(sabesp|copasa|sanepar|cagece|saneago|dmae|agua|esgoto)\b" },
                new ChartOfAccount { Code = "3.1.1.03", Description = "Telefone e Internet", Type = "Despesa", AutoClassificationRegex = @"(?i)\b(vivo|claro|tim|oi|algar|telecom|fibra|internet)\b" },
                new ChartOfAccount { Code = "3.1.1.04", Description = "Material de Escritório", Type = "Despesa", AutoClassificationRegex = @"(?i)\b(kalunga|papelaria|materiais)\b" },
                new ChartOfAccount { Code = "3.1.1.05", Description = "Impostos e Taxas", Type = "Despesa", AutoClassificationRegex = @"(?i)\b(das|darf|gps|inss|fgts|iss|icms|tributos)\b" },
                new ChartOfAccount { Code = "4.1.1.01", Description = "Receita de Serviços Prestados", Type = "Receita", AutoClassificationRegex = @"(?i)\b(prestacao\s*de\s*servicos|honorarios)\b" },
                new ChartOfAccount { Code = "3.2.1.01", Description = "Outras Despesas", Type = "Despesa", AutoClassificationRegex = null }
            );
            db.SaveChanges();
        }

        // Only seed admin user on first run
        if (!db.Users.Any())
        {
            db.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = HashPassword("admin"),
                FullName = "Administrador",
                Role = UserRole.Admin,
                IsActive = true
            });
            db.SaveChanges();
        }
    }

    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
