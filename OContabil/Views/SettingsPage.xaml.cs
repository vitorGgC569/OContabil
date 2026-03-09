using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OContabil.Services;

namespace OContabil.Views;

public partial class SettingsPage : UserControl
{
    private readonly CertificateService _certService = new();
    private readonly GlinerService _glinerService = new();
    private bool _isInitialized;

    public SettingsPage()
    {
        InitializeComponent();
        UpdatePythonStatus();

        // Seed UI from persisted settings
        var savedModel = AppSettings.GlinerModelName;
        foreach (var i in cmbModel.Items.OfType<ComboBoxItem>())
        {
            if ((i.Tag as string) == savedModel)
            {
                cmbModel.SelectedItem = i;
                break;
            }
        }
        txtThreshold.Text = AppSettings.GlinerThreshold.ToString("F2");
        _isInitialized = true;
    }

    private void UpdatePythonStatus()
    {
        if (_glinerService.IsPythonAvailable)
        {
            txtPythonStatus.Text = "Python encontrado ✓";
            txtPythonStatus.Foreground = (System.Windows.Media.Brush)FindResource("Success");
        }
        else
        {
            txtPythonStatus.Text = "Python nao encontrado";
            txtPythonStatus.Foreground = (System.Windows.Media.Brush)FindResource("Error");
        }
    }

    private void OnModelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        if (cmbModel.SelectedItem is ComboBoxItem item && item.Tag is string modelTag)
        {
            AppSettings.GlinerModelName = modelTag;
            ToastService.ShowInfo($"Modelo alterado para: {item.Content}");
        }
    }

    private void OnThresholdChanged(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(txtThreshold.Text.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out double val))
        {
            AppSettings.GlinerThreshold = Math.Clamp(val, 0.0, 1.0);
        }
    }

    private void OnLoadCert(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Certificado Digital (*.pfx;*.p12)|*.pfx;*.p12",
            Title = "Selecionar Certificado Digital A1"
        };

        if (dlg.ShowDialog() != true) return;

        var pwdDialog = new PasswordDialog { Owner = Window.GetWindow(this) };
        if (pwdDialog.ShowDialog() != true || string.IsNullOrEmpty(pwdDialog.Password))
            return;

        var result = _certService.LoadCertificate(dlg.FileName, pwdDialog.Password);

        if (result.Success)
        {
            txtCertStatus.Text = "Certificado carregado ✓";
            txtCertStatus.Foreground = (System.Windows.Media.Brush)FindResource("Success");
            txtCertInfo.Text = $"Titular: {result.Subject}\n" +
                              $"Valido ate: {result.ValidUntil:dd/MM/yyyy}\n" +
                              $"Emissor: {result.Issuer}\n" +
                              $"Serial: {result.SerialNumber}";
        }
        else
        {
            txtCertStatus.Text = "Erro ao carregar certificado";
            txtCertStatus.Foreground = (System.Windows.Media.Brush)FindResource("Error");
            txtCertInfo.Text = result.ErrorMessage;
        }
    }
}
