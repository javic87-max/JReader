using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using lector_de_libros.Services;

namespace lector_de_libros
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly string CrashLogPath = AppPaths.GetDataFilePath("crash.log");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Without these, an unhandled exception on any thread just kills the process silently -
            // which for the user looks like the app "hangs and then closes itself" with no clue why.
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogCrash("UI thread", e.Exception);
            MessageBox.Show(
                $"Ha ocurrido un error inesperado y se ha registrado en:\n{CrashLogPath}\n\n{e.Exception.Message}",
                "Error inesperado",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            // Recoverable: the error is logged and shown, so keep the app running rather than
            // letting WPF tear down the whole process over what's usually a single failed action.
            e.Handled = true;
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Not cancellable (the CLR is already terminating the process here) - this only exists
            // so the crash leaves a trace to diagnose instead of vanishing without one.
            if (e.ExceptionObject is Exception ex)
            {
                LogCrash("background thread (terminating)", ex);
            }
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogCrash("unobserved task", e.Exception);
            e.SetObserved();
        }

        private static void LogCrash(string source, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.DataDirectory);
                File.AppendAllText(CrashLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ({source}) {ex}\n\n");
            }
            catch
            {
                // Logging must never throw on top of the original crash.
            }
        }
    }

}
