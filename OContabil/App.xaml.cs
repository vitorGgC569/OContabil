using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Threading.Tasks;
using OContabil.Services;

namespace OContabil;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Catch UI thread exceptions
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;

        // Catch background thread exceptions
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        // Catch unobserved Task exceptions
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DocumentProcessingQueue.Instance.Shutdown();
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrashAndShowAlert(e.Exception, "UI Thread");
        e.Handled = true; // DO NOT CRASH
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogCrashAndShowAlert(ex, "Background Thread");
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogCrashAndShowAlert(e.Exception, "Async Task");
        e.SetObserved(); // DO NOT CRASH
    }

    private void LogCrashAndShowAlert(Exception ex, string source)
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string crashFile = Path.Combine(desktopPath, "CRASH_OCONTABIL.txt");
        
        string errorDetails = $"--- OCONTABIL CRASH REPORT ---\n" +
                              $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                              $"Source: {source}\n" +
                              $"Message: {ex.Message}\n" +
                              $"Inner Exception: {ex.InnerException?.Message ?? "None"}\n" +
                              $"Stack Trace:\n{ex.StackTrace}\n" +
                              $"-------------------------------\n\n";

        try
        {
            File.AppendAllText(crashFile, errorDetails);
        }
        catch { /* If we can't write, just skip */ }

        MessageBox.Show($"Ocorreu um erro fatal que foi interceptado pelo Sistema Anti-Crash.\n\nUm log foi salvo na sua Área de Trabalho (CRASH_OCONTABIL.txt).\n\nDetalhe: {ex.Message}",
            "Sistema Anti-Crash Ativado", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
