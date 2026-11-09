using System.Diagnostics;
using Nexora.Core.Configuracion;
using Nexora.Core.Modelos;
using Nexora.Core.Utilidades;
using OpenCvSharp;

namespace Nexora.App.Servicios;

/// <summary>
/// Lee la webcam en su propio hilo y hace pasar cada imagen por todos los módulos.
/// </summary>
/// <remarks>
/// <para>
/// La cámara trabaja en un hilo aparte para que la ventana no se congele (si
/// leyéramos la cámara en el hilo de la interfaz, la ventana iría a tirones).
/// </para>
/// <para>Recorrido de cada imagen (igual que en la propuesta):</para>
/// <list type="number">
///   <item><c>VideoCapture.Read</c>: leemos una imagen, como en la primera práctica.</item>
///   <item><c>Flip</c>: la volteamos como un espejo, para que al mover la mano a la
///   derecha el cursor también vaya a la derecha.</item>
///   <item><c>CvtColor</c>: la pasamos a gris, que es como trabaja el detector de caras.</item>
///   <item>Mano → cara y filtro → plan B, según qué módulos estén encendidos.</item>
///   <item>Avisamos a la ventana con el evento <see cref="FotogramaListo"/> con la
///   imagen ya procesada, lista para pintarse.</item>
/// </list>
/// <para>La cámara solo se lee una vez: la cara, la mano y lo que se ve salen de la misma imagen.</para>
/// </remarks>
public sealed class ServicioCamara : IDisposable
{
    private readonly AjustesNexora _ajustes;
    private readonly ServicioCara? _servicioCara;
    private readonly ServicioGestos? _servicioGestos;

    private Thread? _hilo;
    private volatile bool _funcionando;

    /// <summary>Mide FPS y milisegundos por imagen.</summary>
    public MedidorRendimiento Medidor { get; } = new();

    /// <summary>true mientras el hilo de la cámara está leyendo imágenes.</summary>
    public bool EstaFuncionando => _funcionando;

    /// <summary>
    /// Imagen ya procesada. OJO: quien reciba el Mat se hace cargo de él y tiene
    /// que llamar a Dispose cuando termine (es memoria nativa de OpenCV).
    /// </summary>
    public event EventHandler<Mat>? FotogramaListo;

    /// <summary>Avisos para el historial (cámara abierta, errores...).</summary>
    public event EventHandler<EventoNexora>? EventoOcurrido;

    /// <summary>Avisa cuando la cámara se para sola (por ejemplo, si se desconecta).</summary>
    public event EventHandler? CamaraDetenida;

    /// <param name="ajustes">Ajustes compartidos.</param>
    /// <param name="servicioCara">Módulo de cara (puede ser null si no cargó).</param>
    /// <param name="servicioGestos">Módulo de gestos (puede ser null si no cargó).</param>
    public ServicioCamara(AjustesNexora ajustes, ServicioCara? servicioCara, ServicioGestos? servicioGestos)
    {
        _ajustes = ajustes;
        _servicioCara = servicioCara;
        _servicioGestos = servicioGestos;
    }

    /// <summary>Arranca el hilo de la cámara (si no estaba ya arrancado).</summary>
    public void Iniciar()
    {
        if (_funcionando) return;

        _funcionando = true;
        Medidor.Reiniciar();
        _hilo = new Thread(BucleCaptura)
        {
            IsBackground = true, // si se cierra la ventana, el hilo no deja el programa colgado
            Name = "Hilo de la cámara"
        };
        _hilo.Start();
    }

    /// <summary>Para el hilo de la cámara y espera a que termine.</summary>
    public void Detener()
    {
        _funcionando = false;
        if (_hilo is not null && _hilo != Thread.CurrentThread)
        {
            _hilo.Join(TimeSpan.FromSeconds(2));
        }
        _hilo = null;
    }

    /// <summary>El bucle que corre en el hilo de la cámara.</summary>
    private void BucleCaptura()
    {
        using VideoCapture? captura = AbrirCamara();
        if (captura is null)
        {
            Avisar("No se pudo abrir la webcam. ¿Está conectada o la está usando otro programa?");
            _funcionando = false;
            CamaraDetenida?.Invoke(this, EventArgs.Empty);
            return;
        }

        Avisar($"Cámara abierta ({captura.FrameWidth}x{captura.FrameHeight})");

        using var imagen = new Mat();
        using var gris = new Mat();
        int fallosSeguidos = 0;
        string? ultimoError = null;

        while (_funcionando)
        {
            // 1) Leemos una imagen. Si falla muchas veces seguidas, es que la
            //    cámara se ha desconectado.
            if (!captura.Read(imagen) || imagen.Empty())
            {
                if (++fallosSeguidos > 100)
                {
                    Avisar("La cámara ha dejado de mandar imágenes");
                    break;
                }
                Thread.Sleep(10);
                continue;
            }
            fallosSeguidos = 0;

            long inicio = Stopwatch.GetTimestamp();
            try
            {
                ProcesarImagen(imagen, gris);
                ultimoError = null;
            }
            catch (Exception ex)
            {
                // Si algo falla en una imagen, lo apuntamos (una sola vez) y seguimos con la siguiente.
                if (ex.Message != ultimoError)
                {
                    ultimoError = ex.Message;
                    Avisar($"Error procesando la imagen: {ex.Message}");
                }
            }
            Medidor.Registrar(Stopwatch.GetElapsedTime(inicio));

            // 5) Mandamos una copia a la ventana (la "imagen" la reutilizamos en la siguiente vuelta).
            FotogramaListo?.Invoke(this, imagen.Clone());
        }

        _funcionando = false;
        CamaraDetenida?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>El recorrido de cada imagen por los módulos.</summary>
    private void ProcesarImagen(Mat imagen, Mat gris)
    {
        // 2) Espejo
        Cv2.Flip(imagen, imagen, FlipMode.Y);

        // 3) Gris para Haar. Ecualizamos el histograma para que con poca luz
        //    la cara tenga más contraste (esto lo añadimos nosotros).
        Cv2.CvtColor(imagen, gris, ColorConversionCodes.BGR2GRAY);
        Cv2.EqualizeHist(gris, gris);

        bool control = _ajustes.ControlGestualActivo;
        bool conCabeza = _ajustes.ModoCabeza;

        // 4a) La mano va primero, con la imagen todavía "limpia" (sin el filtro encima).
        if (control && !conCabeza)
        {
            _servicioGestos?.ProcesarMano(imagen);
        }

        // 4b) La cara: hace falta para los filtros y también para el plan B.
        ResultadoCara? cara = null;
        if (_servicioCara is not null && (_ajustes.FiltrosActivos || (control && conCabeza)))
        {
            cara = _servicioCara.Procesar(imagen, gris);
        }

        // 4c) Plan B: cursor con la cabeza.
        if (control && conCabeza)
        {
            _servicioGestos?.ProcesarCabeza(imagen, cara);
        }
    }

    /// <summary>
    /// Abre la webcam. En Windows probamos primero DirectShow (abre mucho más
    /// rápido) y, si no va, dejamos que OpenCV elija.
    /// </summary>
    private VideoCapture? AbrirCamara()
    {
        foreach (VideoCaptureAPIs api in new[] { VideoCaptureAPIs.DSHOW, VideoCaptureAPIs.ANY })
        {
            var captura = new VideoCapture(_ajustes.IndiceCamara, api);
            if (captura.IsOpened())
            {
                captura.Set(VideoCaptureProperties.FrameWidth, _ajustes.AnchoCamara);
                captura.Set(VideoCaptureProperties.FrameHeight, _ajustes.AltoCamara);
                return captura;
            }
            captura.Dispose();
        }
        return null;
    }

    private void Avisar(string accion) => EventoOcurrido?.Invoke(this, EventoNexora.Ahora(OrigenEvento.Sistema, accion));

    public void Dispose() => Detener();
}
