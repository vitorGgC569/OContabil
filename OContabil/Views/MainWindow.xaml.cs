using System.Windows;
using System.Windows.Controls;
using OContabil.Data;
using OContabil.Services;

namespace OContabil.Views;

public partial class MainWindow : Window
{
    private readonly AuthService _auth;
    private string _currentPage = "Dashboard";

    public MainWindow(AuthService auth)
    {
        InitializeComponent();
        _auth = auth;

        txtUserName.Text = auth.CurrentUser!.FullName;
        txtUserRole.Text = auth.CurrentUser.RoleDisplay;
        txtUserInitials.Text = string.Join("",
            auth.CurrentUser.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2).Select(w => char.ToUpper(w[0])));

        // Hide admin nav if not admin
        if (!auth.CanManageUsers)
            navUsers.Visibility = Visibility.Collapsed;
            
        ToastService.Initialize(this);

        Navigate("Dashboard");
    }

    protected override void OnClosed(EventArgs e)
    {
        ToastService.Dispose();
        base.OnClosed(e);
    }

    private void OnNav(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string page)
            Navigate(page);
    }

    private void Navigate(string page)
    {
        _currentPage = page;
        
        var (title, sub) = page switch
        {
            "Dashboard" => ("Painel", "Visao geral do processamento"),
            "Documents" => ("Documentos", "Gerenciar documentos fiscais"),
            "Clients" => ("Clientes", "Cadastro de empresas atendidas"),
            "Users" => ("Usuarios", "Gestao de acessos e permissoes"),
            "Settings" => ("Configuracoes", "Preferencias do sistema"),
            _ => ("Painel", "")
        };

        txtPageTitle.Text = title;
        txtPageSub.Text = sub;

        pageHost.Content = page switch
        {
            "Dashboard" => new DashboardPage(),
            "Documents" => new DocumentsPage(_auth),
            "Clients" => new ClientsPage(_auth),
            "Users" => new UsersPage(_auth),
            "Settings" => new SettingsPage(),
            _ => new DashboardPage()
        };
    }

    private void OnToggleTheme(object sender, RoutedEventArgs e)
    {
        ThemeManager.ToggleTheme();
        // BUG-05 FIX: Always refresh the current page to apply new theme resources
        Navigate(_currentPage);
    }

    private void OnLogout(object sender, RoutedEventArgs e)
    {
        _auth.Logout();
        var login = new LoginView();
        login.Show();
        Close();
    }
}
