using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using OContabil.Data;
using OContabil.Models;
using OContabil.Services;

namespace OContabil.Views
{
    public partial class DocumentReviewDialog : Window
    {
        private readonly int _docId;
        private Document _doc = null!;

        public DocumentReviewDialog(int docId)
        {
            InitializeComponent();
            _docId = docId;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                using var db = new AppDbContext();
                _doc = db.Documents.FirstOrDefault(d => d.Id == _docId)!;
                
                if (_doc == null)
                {
                    MessageBox.Show("Documento não encontrado.", "OContabil", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                txtFilename.Text = _doc.Filename;
                txtType.Text = _doc.DocumentType;
                txtStatus.Text = _doc.StatusDisplay;
                txtConfidence.Text = _doc.ConfidenceScore.HasValue 
                    ? $"Confiança: {_doc.ConfidenceScore.Value:P0}" 
                    : "Confiança: N/A";

                txtOcr.Text = _doc.OcrText ?? "Nenhum texto extraído.";
                txtJson.Text = _doc.ExtractedJson ?? "{}";

                // Set badge color based on Status (using correct resource names)
                badgeStatus.Background = _doc.Status switch
                {
                    DocumentStatus.Validated => (Brush)FindResource("SuccessBg"),
                    DocumentStatus.Error => (Brush)FindResource("ErrorBg"),
                    DocumentStatus.ReadyForReview => (Brush)FindResource("WarningBg"),
                    _ => (Brush)FindResource("InfoBg")
                };
                
                txtStatus.Foreground = _doc.Status switch
                {
                    DocumentStatus.Validated => (Brush)FindResource("Success"),
                    DocumentStatus.Error => (Brush)FindResource("Error"),
                    DocumentStatus.ReadyForReview => (Brush)FindResource("Warning"),
                    _ => (Brush)FindResource("Info")
                };

                // Set ComboBox to match Status
                foreach (System.Windows.Controls.ComboBoxItem item in cmbStatus.Items)
                {
                    if (item.Tag?.ToString() == ((int)_doc.Status).ToString())
                    {
                        cmbStatus.SelectedItem = item;
                        break;
                    }
                }

                _ = InitializeWebViewAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar documento: {ex.Message}", "OContabil", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task InitializeWebViewAsync()
        {
            try
            {
                await docViewer.EnsureCoreWebView2Async(null);
                
                if (!string.IsNullOrEmpty(_doc.FilePath) && System.IO.File.Exists(_doc.FilePath))
                {
                    docViewer.Source = new Uri(_doc.FilePath);
                }
                else
                {
                    docViewer.NavigateToString("<html><body style='font-family:Segoe UI; padding: 20px; color:#666;'><h2>Arquivo não encontrado</h2><p>O arquivo original não existe no caminho local: <br/>" + (_doc.FilePath ?? "Desconhecido") + "</p></body></html>");
                }
            }
            catch (Exception ex)
            {
                // Most likely WebView2RuntimeNotFoundException
                MessageBox.Show($"O visualizador de PDF/Imagem requer o Microsoft Edge WebView2, que não está instalado ou falhou.\n\nDetalhe: {ex.Message}", 
                    "Componente Ausente", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            try
            {
                using var db = new AppDbContext();
                var docToUpdate = db.Documents.FirstOrDefault(d => d.Id == _docId);
                if (docToUpdate == null) return;

                docToUpdate.ExtractedJson = txtJson.Text;

                if (cmbStatus.SelectedItem is System.Windows.Controls.ComboBoxItem item && 
                    int.TryParse(item.Tag?.ToString(), out int statusId))
                {
                    docToUpdate.Status = (DocumentStatus)statusId;
                    if (docToUpdate.Status == DocumentStatus.Validated)
                    {
                        docToUpdate.ConfidenceScore = 1.0;
                    }
                }

                db.SaveChanges();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ToastService.ShowError($"Erro ao salvar: {ex.Message}");
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
