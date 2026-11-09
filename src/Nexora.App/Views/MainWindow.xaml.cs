using System.Windows;
using Nexora.App.ViewModels;

namespace Nexora.App.Views;

/// <summary>
/// Código de la ventana. Con MVVM aquí no va lógica: solo creamos el
/// ViewModel, lo arrancamos cuando la ventana ya se ve y lo paramos al cerrar.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        // Arrancamos la cámara y la voz cuando la ventana ya está cargada,
        // así se ve la ventana enseguida aunque la cámara tarde un poco en abrir.
        Loaded += (_, _) => _viewModel.IniciarCommand.Execute(null);

        // Cuando por voz decimos «salir» y luego «sí», el ViewModel nos pide cerrar.
        _viewModel.CerrarSolicitado += (_, _) => Close();

        // Al cerrar paramos el hilo de la cámara y el micrófono.
        Closed += (_, _) => _viewModel.Dispose();
    }
}
