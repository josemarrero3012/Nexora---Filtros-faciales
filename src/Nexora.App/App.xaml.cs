using System.Windows;
using System.Windows.Threading;

namespace Nexora.App;

/// <summary>
/// Clase de la aplicación. La ventana que se abre al arrancar se indica en
/// App.xaml (StartupUri), así que aquí casi no hay nada.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Si se nos escapa alguna excepción en el hilo de la ventana, la
        // enseñamos en un mensaje en vez de que el programa se cierre de golpe.
        DispatcherUnhandledException += AlFallarAlgo;
    }

    private static void AlFallarAlgo(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Ha pasado algo que no esperábamos:\n\n{e.Exception.Message}",
            "Nexora",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }
}
