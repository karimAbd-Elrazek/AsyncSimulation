using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace AsyncSimulation;

public partial class App : Application
{
    private static readonly string ErrorLogPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "error-log.txt");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try { File.Delete(ErrorLogPath); } catch { }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void WriteError(string source, Exception ex)
    {
        try
        {
            File.AppendAllText(ErrorLogPath,
                $"[{DateTime.Now:HH:mm:ss.fff}] {source}\n" +
                $"  Type: {ex.GetType().FullName}\n" +
                $"  Message: {ex.Message}\n" +
                $"  Stack:\n{ex.StackTrace}\n" +
                $"----------------------------------------\n");
        }
        catch { }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteError("UI Thread", e.Exception);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            WriteError("Background Thread", ex);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
    }
}
