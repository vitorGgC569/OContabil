using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using OContabil.Data;
using OContabil.Models;

namespace OContabil.Views;

public partial class DashboardPage : UserControl
{
    public DashboardPage()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            using var db = new AppDbContext();
            var docs = db.Documents.Include(d => d.Client).OrderByDescending(d => d.UploadedAt).ToList();
            var clients = db.Clients.Where(c => c.IsActive).ToList();

            int total = docs.Count;
            int validated = docs.Count(d => d.Status == DocumentStatus.Validated);
            int pending = docs.Count(d => d.Status == DocumentStatus.ReadyForReview || d.Status == DocumentStatus.Pending);

            statDocs.Text = total.ToString();
            statValidated.Text = validated.ToString();
            statValPct.Text = total > 0 ? $"{validated * 100 / total}% do total" : "";
            statPending.Text = pending.ToString();
            statClients.Text = clients.Count.ToString();

            gridRecent.ItemsSource = docs.Take(10).Select(d => new
            {
                d.Filename,
                d.DocumentType,
                ClientName = d.Client?.Name ?? "",
                d.StatusDisplay,
                DateStr = d.UploadedAt.ToString("dd/MM/yyyy"),
                SizeStr = d.FileSizeDisplay
            }).ToList();

            gridClients.ItemsSource = clients;
        }
        catch (Exception ex)
        {
            Services.ToastService.ShowError($"Erro ao carregar painel: {ex.Message}");
        }
    }
}
