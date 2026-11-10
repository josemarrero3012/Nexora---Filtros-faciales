using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.App.Servicios;
using Nexora.Core.Configuracion;
using Nexora.Core.Modelos;
using Nexora.Core.Vision;
using Nexora.Core.Voz;
using OpenCvSharp.WpfExtensions;
using Mat = OpenCvSharp.Mat;

namespace Nexora.App.ViewModels;

/// <summary>Una opción de la lista de filtros de la ventana.</summary>
/// <param name="Tipo">El filtro.</param>
/// <param name="Nombre">El texto que se ve en el botón.</param>
public sealed record OpcionFiltro(TipoFiltro Tipo, string Nombre);

/// <summary>
/// ViewModel de la ventana principal (MVVM con CommunityToolkit.Mvvm, como en clase).
/// </summary>
/// <remarks>
/// <para>
/// Es el "pegamento" entre la ventana y los servicios. La ventana solo hace
/// Binding a estas propiedades; los servicios avisan con eventos y aquí
/// actualizamos las propiedades, que se refrescan solas en la ventana gracias a
/// <c>[ObservableProperty]</c>.
/// </para>
/// <para>
/// MUY IMPORTANTE: los eventos de la cámara y de la voz llegan desde OTROS
/// hilos. En WPF solo el hilo de la interfaz puede tocar la ventana, así que
/// todo lo que viene de fuera lo pasamos con <c>Dispatcher.BeginInvoke</c>.
/// </para>
/// </remarks>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    /// <summary>Máximo de filas del historial (para que no crezca sin parar).</summary>
    private const int MaximoHistorial = 300;

    private readonly AjustesNexora _ajustes = new();
    private readonly ServicioRaton _raton = new();
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _temporizadorRendimiento;

    private ServicioCamara? _camara;
    private ServicioCara? _cara;
    private ServicioGestos? _gestos;
    private ServicioVoz? _voz;

    private WriteableBitmap? _bitmap;
    private int _pintandoImagen; // 0 = libre, 1 = la ventana está pintando una imagen
    private bool _iniciado;

    /// <summary>La ventana se suscribe a esto para cerrarse cuando decimos «salir» y «sí».</summary>
    public event EventHandler? CerrarSolicitado;

    public MainViewModel()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        // Valores iniciales sacados de los ajustes (así hay un único sitio con los números).
        FiltrosActivos = _ajustes.FiltrosActivos;
        ControlGestualActivo = _ajustes.ControlGestualActivo;
        ModoCabeza = _ajustes.ModoCabeza;
        MostrarAyudas = _ajustes.MostrarAyudas;
        FiltroSeleccionado = _ajustes.FiltroActual;
        FiltroRealista = _ajustes.FiltroRealista; // [IA]
        FactorEscalaCara = _ajustes.FactorEscalaCara;
        VecinosMinimosCara = _ajustes.VecinosMinimosCara;
        ConfianzaMinimaVoz = _ajustes.ConfianzaMinimaVoz;
        ZonaMuertaPx = _ajustes.ZonaMuertaPx;
        SuavizadoCursor = _ajustes.SuavizadoCursor;

        // Cada medio segundo refrescamos los números de rendimiento (FPS y ms).
        _temporizadorRendimiento = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _temporizadorRendimiento.Tick += (_, _) => ActualizarRendimiento();
    }

    // =====================================================================
    //  Propiedades enlazadas con la ventana
    // =====================================================================

    /// <summary>Lo último que ha llegado de la cámara, ya procesado.</summary>
    [ObservableProperty] public partial ImageSource? ImagenCamara { get; set; }

    /// <summary>Texto de estado de la cámara.</summary>
    [ObservableProperty] public partial string EstadoCamara { get; set; } = "Abriendo cámara...";

    /// <summary>true si la cámara está mandando imágenes.</summary>
    [ObservableProperty] public partial bool CamaraFuncionando { get; set; }

    // --- Interruptores de los módulos ---

    [ObservableProperty] public partial bool FiltrosActivos { get; set; }
    [ObservableProperty] public partial bool ControlGestualActivo { get; set; }
    [ObservableProperty] public partial bool VozActiva { get; set; } = true;
    [ObservableProperty] public partial bool ModoCabeza { get; set; }
    [ObservableProperty] public partial bool MostrarAyudas { get; set; }

    /// <summary>false si el modelo de la mano no cargó (entonces solo queda el plan B).</summary>
    [ObservableProperty] public partial bool ManoDisponible { get; set; } = true;

    // --- Filtro ---

    /// <summary>Opciones para la lista de filtros (incluye «Sin filtro»).</summary>
    public IReadOnlyList<OpcionFiltro> Filtros { get; } =
        [new OpcionFiltro(TipoFiltro.Ninguno, "Sin filtro"), .. CatalogoFiltros.Todos.Select(f => new OpcionFiltro(f.Tipo, f.NombreVisible))];

    [ObservableProperty] public partial TipoFiltro FiltroSeleccionado { get; set; }

    /// <summary>
    /// [Ampliación con uso de IA] Sombra, ajuste de luz y aparecer suave del
    /// filtro. Se puede apagar para comparar en las pruebas.
    /// </summary>
    [ObservableProperty] public partial bool FiltroRealista { get; set; }

    // --- Voz ---

    [ObservableProperty] public partial string UltimaOrdenVoz { get; set; } = "Todavía nada";
    [ObservableProperty] public partial string DetalleUltimaOrden { get; set; } = "Prueba a decir «siguiente filtro»";
    [ObservableProperty] public partial bool EsperandoConfirmacion { get; set; }

    /// <summary>Las frases que entiende el programa, para enseñarlas en la ventana.</summary>
    public string FrasesDisponibles { get; } =
        string.Join("  ·  ", InterpreteOrdenes.OrdenesPrincipales.Keys.Select(f => $"«{f}»"));

    // --- Rendimiento (lo que pide la evaluación) ---

    [ObservableProperty] public partial string Fps { get; set; } = "—";
    [ObservableProperty] public partial string MsPorImagen { get; set; } = "—";
    [ObservableProperty] public partial string RespuestaGesto { get; set; } = "—";
    [ObservableProperty] public partial string RespuestaVoz { get; set; } = "—";
    [ObservableProperty] public partial string GestoActual { get; set; } = "Ninguno";

    // --- Ajustes del detector (para elegir los valores con datos) ---

    [ObservableProperty] public partial double FactorEscalaCara { get; set; }
    [ObservableProperty] public partial int VecinosMinimosCara { get; set; }
    [ObservableProperty] public partial double ConfianzaMinimaVoz { get; set; }
    [ObservableProperty] public partial double ZonaMuertaPx { get; set; }
    [ObservableProperty] public partial double SuavizadoCursor { get; set; }

    /// <summary>Historial de eventos: hora, origen y acción.</summary>
    public ObservableCollection<EventoNexora> Historial { get; } = [];

    // =====================================================================
    //  Comandos (botones)
    // =====================================================================

    /// <summary>
    /// Arranca todos los servicios. Lo llama la ventana cuando ya se ha cargado.
    /// Si un módulo falla, seguimos con los demás (por ejemplo, sin micrófono
    /// los filtros siguen funcionando).
    /// </summary>
    [RelayCommand]
    private void Iniciar()
    {
        if (_iniciado) return;
        _iniciado = true;
        Registrar(OrigenEvento.Sistema, "Nexora arrancando");

        // --- Cara y filtros ---
        try
        {
            _cara = new ServicioCara(_ajustes);
            _cara.EventoOcurrido += AlOcurrirEvento;
        }
        catch (Exception ex)
        {
            Registrar(OrigenEvento.Cara, $"No se pudo cargar el detector de caras: {ex.Message}");
        }

        // --- Mano y gestos ---
        try
        {
            _gestos = new ServicioGestos(_ajustes, _raton);
            _gestos.EventoOcurrido += AlOcurrirEvento;
            _gestos.GestoCambiado += AlCambiarGesto;
            if (!_gestos.ModeloDisponible)
            {
                ManoDisponible = false;
                ModoCabeza = true; // pasamos directamente al plan B
                Registrar(OrigenEvento.Mano, $"Sin modelo de la mano, usamos el plan B (cabeza): {_gestos.ErrorModelo}");
            }
        }
        catch (Exception ex)
        {
            ManoDisponible = false;
            Registrar(OrigenEvento.Mano, $"No se pudo preparar el control gestual: {ex.Message}");
        }

        // --- Cámara ---
        _camara = new ServicioCamara(_ajustes, _cara, _gestos);
        _camara.FotogramaListo += AlLlegarFotograma;
        _camara.EventoOcurrido += AlOcurrirEvento;
        _camara.CamaraDetenida += (_, _) => EnHiloVentana(() =>
        {
            CamaraFuncionando = false;
            EstadoCamara = "Cámara parada";
        });
        _camara.Iniciar();
        _temporizadorRendimiento.Start();

        // --- Voz ---
        try
        {
            _voz = new ServicioVoz(_ajustes);
            _voz.EventoOcurrido += AlOcurrirEvento;
            _voz.OrdenReconocida += AlReconocerOrden;
            if (VozActiva && !_voz.Iniciar()) VozActiva = false;
        }
        catch (Exception ex)
        {
            VozActiva = false;
            Registrar(OrigenEvento.Voz, $"No se pudo preparar la voz: {ex.Message}");
        }
    }

    /// <summary>Vuelve a abrir la cámara (por si se desconectó).</summary>
    [RelayCommand]
    private void ReiniciarCamara()
    {
        if (_camara is null) return;
        _camara.Detener();
        EstadoCamara = "Abriendo cámara...";
        _camara.Iniciar();
    }

    /// <summary>Pasa al siguiente filtro (lo mismo que la orden «siguiente filtro»).</summary>
    [RelayCommand]
    private void SiguienteFiltro()
    {
        FiltrosActivos = true;
        FiltroSeleccionado = CatalogoFiltros.Siguiente(FiltroSeleccionado);
    }

    /// <summary>Borra el historial de la ventana.</summary>
    [RelayCommand]
    private void LimpiarHistorial() => Historial.Clear();

    /// <summary>
    /// Guarda el historial en un CSV en Documentos. Nos viene bien para la
    /// memoria: ahí están los tiempos de respuesta y las órdenes con su confianza.
    /// </summary>
    [RelayCommand]
    private void ExportarHistorial()
    {
        try
        {
            string carpeta = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string ruta = Path.Combine(carpeta, $"Nexora_historial_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            var sb = new StringBuilder();
            sb.AppendLine("Hora;Origen;Accion");
            // El historial está con lo más nuevo arriba; en el CSV lo ponemos en orden de tiempo.
            foreach (EventoNexora evento in Historial.Reverse()) sb.AppendLine(evento.ACsv());

            // UTF-8 con BOM para que Excel lea bien las tildes.
            File.WriteAllText(ruta, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            Registrar(OrigenEvento.Sistema, $"Historial guardado en {ruta}");
        }
        catch (Exception ex)
        {
            Registrar(OrigenEvento.Sistema, $"No se pudo guardar el historial: {ex.Message}");
        }
    }

    // =====================================================================
    //  Cambios en las propiedades → los pasamos a los ajustes compartidos
    //  (estos métodos "On...Changed" los llama solo el CommunityToolkit)
    // =====================================================================

    partial void OnFiltrosActivosChanged(bool value)
    {
        _ajustes.FiltrosActivos = value;
        Registrar(OrigenEvento.Cara, value ? "Filtros encendidos" : "Filtros apagados");
    }

    partial void OnControlGestualActivoChanged(bool value)
    {
        _ajustes.ControlGestualActivo = value;
        if (!value)
        {
            _gestos?.Reiniciar();
            GestoActual = "Ninguno";
        }
        Registrar(OrigenEvento.Mano, value ? "Control gestual activado" : "Control gestual en pausa");
    }

    partial void OnModoCabezaChanged(bool value)
    {
        _ajustes.ModoCabeza = value;
        _gestos?.Reiniciar();
        Registrar(OrigenEvento.Cara, value ? "Plan B: cursor con la cabeza (clic diciendo «clic»)" : "Cursor con la mano");
    }

    partial void OnMostrarAyudasChanged(bool value) => _ajustes.MostrarAyudas = value;

    partial void OnVozActivaChanged(bool value)
    {
        if (_voz is null) return;
        if (value)
        {
            // Si no se puede encender (sin micrófono, sin español...), lo dejamos apagado.
            if (!_voz.Iniciar()) VozActiva = false;
        }
        else
        {
            _voz.Detener();
        }
    }

    partial void OnFiltroSeleccionadoChanged(TipoFiltro value)
    {
        _ajustes.FiltroActual = value;
        Registrar(OrigenEvento.Cara, $"Filtro: {CatalogoFiltros.Nombre(value)}");
    }

    // [IA] Interruptor del filtro realista.
    partial void OnFiltroRealistaChanged(bool value)
    {
        _ajustes.FiltroRealista = value;
        Registrar(OrigenEvento.Cara, value ? "Filtro realista (sombra y luz)" : "Filtro tal cual");
    }

    partial void OnFactorEscalaCaraChanged(double value) => _ajustes.FactorEscalaCara = Math.Round(value, 2);
    partial void OnVecinosMinimosCaraChanged(int value) => _ajustes.VecinosMinimosCara = value;
    partial void OnConfianzaMinimaVozChanged(double value) => _ajustes.ConfianzaMinimaVoz = Math.Round(value, 2);
    partial void OnZonaMuertaPxChanged(double value) => _ajustes.ZonaMuertaPx = value;
    partial void OnSuavizadoCursorChanged(double value) => _ajustes.SuavizadoCursor = value;

    // =====================================================================
    //  Eventos de los servicios
    // =====================================================================

    /// <summary>
    /// Llega una imagen nueva del hilo de la cámara. Si la ventana todavía está
    /// pintando la anterior, tiramos esta (mejor perder una imagen que acumular
    /// retraso).
    /// </summary>
    private void AlLlegarFotograma(object? sender, Mat imagen)
    {
        if (Interlocked.Exchange(ref _pintandoImagen, 1) == 1)
        {
            imagen.Dispose();
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            try
            {
                PintarImagen(imagen);
            }
            finally
            {
                imagen.Dispose();
                Interlocked.Exchange(ref _pintandoImagen, 0);
            }
        }, DispatcherPriority.Render);
    }

    /// <summary>
    /// Pasa el Mat de OpenCV a la imagen de WPF (ToWriteableBitmap). Reutilizamos
    /// el mismo WriteableBitmap mientras no cambie el tamaño, que es más rápido
    /// que crear uno nuevo cada vez.
    /// </summary>
    private void PintarImagen(Mat imagen)
    {
        if (_bitmap is null || _bitmap.PixelWidth != imagen.Width || _bitmap.PixelHeight != imagen.Height)
        {
            _bitmap = imagen.ToWriteableBitmap();
            ImagenCamara = _bitmap;
        }
        else
        {
            WriteableBitmapConverter.ToWriteableBitmap(imagen, _bitmap);
        }

        if (!CamaraFuncionando)
        {
            CamaraFuncionando = true;
            EstadoCamara = "Cámara funcionando";
        }
    }

    /// <summary>Cualquier servicio que quiera apuntar algo en el historial.</summary>
    private void AlOcurrirEvento(object? sender, EventoNexora evento) => EnHiloVentana(() => AnadirAlHistorial(evento));

    /// <summary>Ha cambiado el gesto de la mano.</summary>
    private void AlCambiarGesto(object? sender, ResultadoGesto resultado) => EnHiloVentana(() =>
    {
        GestoActual = resultado.Gesto.ToString();
        if (resultado.Gesto != Gesto.Ninguno) RespuestaGesto = $"{resultado.MsRespuesta:0} ms";
    });

    /// <summary>
    /// Ha llegado una orden de voz. Si es fiable (60 % o más), la ejecutamos y
    /// el programa contesta en voz alta.
    /// </summary>
    private void AlReconocerOrden(object? sender, OrdenVoz orden) => EnHiloVentana(() =>
    {
        UltimaOrdenVoz = $"«{orden.Texto}»";
        DetalleUltimaOrden = $"Confianza {orden.Confianza:P0}" + (orden.Aceptada ? "" : " · ignorada (por debajo del mínimo)");
        if (orden.MsRespuesta is { } ms) RespuestaVoz = $"{ms:0} ms";

        if (!orden.Aceptada)
        {
            Registrar(OrigenEvento.Voz, $"Ignorada «{orden.Texto}» ({orden.Confianza:P0})");
            return;
        }

        Registrar(OrigenEvento.Voz, $"«{orden.Texto}» ({orden.Confianza:P0})");
        EjecutarOrden(orden.Comando);
    });

    /// <summary>Qué hace cada orden de voz.</summary>
    private void EjecutarOrden(ComandoVoz comando)
    {
        switch (comando)
        {
            case ComandoVoz.ActivarControlGestual:
                ControlGestualActivo = true;
                break;

            case ComandoVoz.PausarControl:
                ControlGestualActivo = false;
                break;

            case ComandoVoz.SiguienteFiltro:
                SiguienteFiltro();
                _voz?.Hablar(CatalogoFiltros.Nombre(FiltroSeleccionado));
                return; // ya hemos contestado con el nombre del filtro

            case ComandoVoz.QuitarFiltro:
                FiltroSeleccionado = TipoFiltro.Ninguno;
                break;

            case ComandoVoz.Clic:
                // Del plan B: el clic con la voz.
                _raton.Clic();
                break;

            case ComandoVoz.Salir:
                // Antes de salir preguntamos, y solo escuchamos «sí» o «no».
                _voz?.PedirConfirmacion();
                EsperandoConfirmacion = true;
                break;

            case ComandoVoz.Si:
                EsperandoConfirmacion = false;
                _voz?.Hablar(InterpreteOrdenes.Respuesta(comando));
                CerrarConRetraso();
                return;

            case ComandoVoz.No:
                EsperandoConfirmacion = false;
                break;
        }

        _voz?.Hablar(InterpreteOrdenes.Respuesta(comando));
    }

    /// <summary>Esperamos un poco antes de cerrar para que dé tiempo a decir «hasta luego».</summary>
    private async void CerrarConRetraso()
    {
        await Task.Delay(1200);
        CerrarSolicitado?.Invoke(this, EventArgs.Empty);
    }

    // =====================================================================
    //  Ayudas
    // =====================================================================

    /// <summary>Refresca los números de rendimiento (lo llama el temporizador).</summary>
    private void ActualizarRendimiento()
    {
        if (_camara is null || !_camara.EstaFuncionando) return;
        Fps = $"{_camara.Medidor.Fps:0.0}";
        MsPorImagen = $"{_camara.Medidor.MsPorFotograma:0.0} ms";
    }

    /// <summary>
    /// Apunta algo en el historial (desde el hilo de la ventana). Mientras el
    /// constructor pone los valores iniciales no apuntamos nada, para que el
    /// historial no empiece lleno de «Filtros encendidos» y cosas así.
    /// </summary>
    private void Registrar(OrigenEvento origen, string accion)
    {
        if (!_iniciado) return;
        AnadirAlHistorial(EventoNexora.Ahora(origen, accion));
    }

    private void AnadirAlHistorial(EventoNexora evento)
    {
        Historial.Insert(0, evento); // lo más nuevo arriba
        while (Historial.Count > MaximoHistorial) Historial.RemoveAt(Historial.Count - 1);
    }

    /// <summary>Ejecuta algo en el hilo de la ventana (si ya estamos en él, directamente).</summary>
    private void EnHiloVentana(Action accion)
    {
        if (_dispatcher.CheckAccess()) accion();
        else _dispatcher.BeginInvoke(accion);
    }

    /// <summary>Paramos todo al cerrar: primero la cámara (su hilo) y luego lo demás.</summary>
    public void Dispose()
    {
        _temporizadorRendimiento.Stop();

        if (_camara is not null)
        {
            _camara.FotogramaListo -= AlLlegarFotograma;
            _camara.Dispose();
        }

        _voz?.Dispose();
        _gestos?.Dispose();
        _cara?.Dispose();
    }
}
