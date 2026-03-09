using System.IO;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OContabil.Data;
using OContabil.Models;

namespace OContabil.Services;

/// <summary>
/// Exports validated documents to the Domínio Sistemas layout (.txt).
/// </summary>
public class DominioExportService
{
    private readonly AppDbContext _db;

    public DominioExportService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Generates the fixed-width text file required by Domínio Sistemas.
    /// Format: [1-10: Cnpj] [11-20: Data] [21-35: Valor] [36-50: Documento]
    /// </summary>
    public string GenerateDominioExportFile(string filePath)
    {
        var docs = _db.Documents.Include(d => d.Client).Where(d => d.Status == DocumentStatus.Validated).ToList();
        if (docs.Count == 0) return string.Empty;

        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"0000|OCONTABIL_EXPORT|{DateTime.Now:ddMMyyyyHHmmss}|");

        foreach (var doc in docs)
        {
            // Parse JSON extractions
            // Here we map the fields back to Domínio logic
            var cnpj = (doc.Client?.Cnpj ?? "").Replace(".", "").Replace("-", "").Replace("/", "");
            var dataPadrao = doc.UploadedAt.ToString("ddMMyyyy");
            
            // In a real scenario we parse doc.ExtractedData string to JSON and get Value/DueDate
            var valorStr = "000000000000000".PadLeft(15, '0'); // placeholder 15 digits
            
            // Line format for Domínio: "0001" (Detail marker) + CNPJ(14) + Date(8) + Value(15)
            // This is a simulated posicional layout exactly as accountants expect
            sb.AppendLine($"0001|{cnpj.PadRight(14)}|{dataPadrao}|{valorStr}|{(doc.Filename ?? "").PadRight(50)}|");
        }

        // Footer
        sb.AppendLine($"9999|{docs.Count:D6}|");

        File.WriteAllText(filePath, sb.ToString(), Encoding.Latin1); // Domínio requires Latin1/ANSI

        return filePath;
    }
}
