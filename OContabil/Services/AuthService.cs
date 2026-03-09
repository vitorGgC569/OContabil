using OContabil.Data;
using OContabil.Models;

namespace OContabil.Services;

public class AuthService
{
    public User? CurrentUser { get; private set; }
    public bool IsLoggedIn => CurrentUser != null;

    public AuthService() { }

    public bool Login(string username, string password)
    {
        using var db = new AppDbContext();
        var hash = DbInitializer.HashPassword(password);
        var user = db.Users.FirstOrDefault(u =>
            u.Username == username && u.PasswordHash == hash && u.IsActive);

        if (user != null)
        {
            CurrentUser = user;
            return true;
        }
        return false;
    }

    public void Logout() => CurrentUser = null;

    public bool CanManageUsers => CurrentUser?.Role == UserRole.Admin;
    public bool CanEdit => CurrentUser?.Role != UserRole.Visualizador;
    public bool CanValidate => CurrentUser?.Role == UserRole.Admin || CurrentUser?.Role == UserRole.Operador;
}
