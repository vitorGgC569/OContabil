using System.Windows;
using System.Windows.Input;
using OContabil.Data;
using OContabil.Services;

namespace OContabil.Views;

public partial class LoginView : Window
{
    private readonly AuthService _auth;

    public LoginView()
    {
        InitializeComponent();

        // Initialize DB on startup using a scoped context
        using (var db = new AppDbContext())
        {
            DbInitializer.Initialize(db);
        }

        _auth = new AuthService();

        txtPass.KeyDown += (_, e) => { if (e.Key == Key.Enter) OnLogin(this, new RoutedEventArgs()); };
        txtUser.KeyDown += (_, e) => { if (e.Key == Key.Enter) txtPass.Focus(); };
    }

    private void OnLogin(object sender, RoutedEventArgs e)
    {
        var user = txtUser.Text.Trim();
        var pass = txtPass.Password;

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            ShowError("Preencha usuario e senha.");
            return;
        }

        if (_auth.Login(user, pass))
        {
            var main = new MainWindow(_auth);
            main.Show();
            Close();
        }
        else
        {
            ShowError("Usuario ou senha incorretos.");
            txtPass.Password = "";
            txtPass.Focus();
        }
    }

    private void ShowError(string msg)
    {
        txtError.Text = msg;
        txtError.Visibility = Visibility.Visible;
    }
}
