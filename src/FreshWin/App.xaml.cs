using System.Windows;
using System.Windows.Threading;

namespace FreshWin;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"FreshWin hit an unexpected error and may not be able to continue:\n\n{e.Exception.Message}",
            "FreshWin",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
