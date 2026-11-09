using System.Globalization;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using Nexora.Core.Configuracion;
using Nexora.Core.Modelos;
using Nexora.Core.Voz;

namespace Nexora.App.Servicios;

/// <summary>
/// Datos de una orden de voz reconocida, para la ventana.
/// </summary>
/// <param name="Comando">Qué orden es.</param>
/// <param name="Texto">Lo que ha oído el reconocedor.</param>
/// <param name="Confianza">Lo seguro que está (0..1).</param>
/// <param name="Aceptada">true si pasa del mínimo (60 %) y la vamos a ejecutar.</param>
/// <param name="MsRespuesta">Milisegundos desde que terminamos de hablar hasta que llega el evento (o null si no se sabe).</param>
public sealed record OrdenVoz(ComandoVoz Comando, string Texto, float Confianza, bool Aceptada, double? MsRespuesta);

/// <summary>
/// Módulo de voz: reconoce unas pocas órdenes en español y contesta en voz alta.
/// </summary>
/// <remarks>
/// <para>Usamos System.Speech, lo mismo que en las prácticas:</para>
/// <list type="bullet">
///   <item><c>SpeechRecognitionEngine</c> escucha todo el rato el micrófono.</item>
///   <item>Le damos una lista cerrada de frases con <c>Choices</c> y <c>GrammarBuilder</c>.</item>
///   <item>Cada vez que reconoce una, salta el evento <c>SpeechRecognized</c> con el
///   texto y lo seguro que está. Si pasa del 60 %, la ejecutamos.</item>
///   <item><c>SpeechSynthesizer</c> contesta en voz alta.</item>
/// </list>
/// <para>
/// Antes de salir, el programa pregunta y en ese momento SOLO escucha «sí» o
/// «no», una vez, como en el ejercicio de guardar los cambios. Para eso tenemos
/// dos gramáticas y encendemos una u otra.
/// </para>
/// <para>
/// Importante: System.Speech usa el reconocedor de Windows, así que el equipo
/// tiene que tener el idioma español instalado (con su paquete de voz).
/// </para>
/// </remarks>
public sealed class ServicioVoz : IDisposable
{
    private readonly AjustesNexora _ajustes;
    private readonly SpeechSynthesizer _sintetizador = new();
    private SpeechRecognitionEngine? _reconocedor;
    private Grammar? _gramaticaOrdenes;
    private Grammar? _gramaticaConfirmacion;
    private volatile bool _hablando;

    /// <summary>Salta cada vez que se reconoce una frase (aceptada o no).</summary>
    public event EventHandler<OrdenVoz>? OrdenReconocida;

    /// <summary>Avisos para el historial (micrófono listo, errores...).</summary>
    public event EventHandler<EventoNexora>? EventoOcurrido;

    /// <summary>true mientras el micrófono está escuchando.</summary>
    public bool Escuchando { get; private set; }

    /// <summary>true cuando hemos preguntado «¿seguro que quieres salir?» y esperamos un sí o un no.</summary>
    public bool EsperandoConfirmacion { get; private set; }

    public ServicioVoz(AjustesNexora ajustes)
    {
        _ajustes = ajustes;

        // Mientras el programa habla, no hacemos caso al micrófono: si no, se
        // podría "oír a sí mismo" y ejecutar órdenes sin querer.
        _sintetizador.SpeakStarted += (_, _) => _hablando = true;
        _sintetizador.SpeakCompleted += (_, _) => _hablando = false;

        try
        {
            _sintetizador.SetOutputToDefaultAudioDevice();
            ElegirVozEnEspanol();
        }
        catch (Exception ex)
        {
            // Sin altavoces no pasa nada grave: el programa sigue, solo que callado.
            Avisar($"No se pudo preparar la voz del programa: {ex.Message}");
        }
    }

    /// <summary>
    /// Empieza a escuchar el micrófono.
    /// </summary>
    /// <returns>true si todo fue bien; false si no hay reconocedor en español o no hay micrófono.</returns>
    public bool Iniciar()
    {
        if (Escuchando) return true;

        try
        {
            if (_reconocedor is null && !CrearReconocedor()) return false;

            // Multiple = que siga escuchando después de reconocer una frase.
            _reconocedor!.RecognizeAsync(RecognizeMode.Multiple);
            Escuchando = true;
            Avisar($"Micrófono escuchando ({_reconocedor.RecognizerInfo.Culture.Name})");
            return true;
        }
        catch (Exception ex)
        {
            Avisar($"No se pudo usar el micrófono: {ex.Message}");
            return false;
        }
    }

    /// <summary>Deja de escuchar el micrófono.</summary>
    public void Detener()
    {
        if (!Escuchando || _reconocedor is null) return;
        _reconocedor.RecognizeAsyncCancel();
        Escuchando = false;
        Avisar("Micrófono apagado");
    }

    /// <summary>El programa dice algo en voz alta (sin bloquear la ventana).</summary>
    public void Hablar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return;
        try
        {
            _sintetizador.SpeakAsyncCancelAll(); // si estaba diciendo otra cosa, la cortamos
            _sintetizador.SpeakAsync(texto);
        }
        catch
        {
            // Si no hay audio, seguimos sin voz.
        }
    }

    /// <summary>
    /// Pasa al modo pregunta: a partir de ahora solo se escucha «sí» o «no».
    /// </summary>
    public void PedirConfirmacion()
    {
        if (_gramaticaOrdenes is null || _gramaticaConfirmacion is null) return;
        EsperandoConfirmacion = true;
        _gramaticaOrdenes.Enabled = false;
        _gramaticaConfirmacion.Enabled = true;
    }

    /// <summary>Vuelve a las órdenes normales después de la pregunta.</summary>
    private void TerminarConfirmacion()
    {
        if (_gramaticaOrdenes is null || _gramaticaConfirmacion is null) return;
        EsperandoConfirmacion = false;
        _gramaticaConfirmacion.Enabled = false;
        _gramaticaOrdenes.Enabled = true;
    }

    /// <summary>Crea el reconocedor con sus dos gramáticas.</summary>
    private bool CrearReconocedor()
    {
        RecognizerInfo? info = BuscarReconocedorEspanol();
        if (info is null)
        {
            Avisar("Windows no tiene el reconocedor de voz en español. Instálalo en Configuración > Hora e idioma > Voz.");
            return false;
        }

        _reconocedor = new SpeechRecognitionEngine(info);

        // Gramática 1: las órdenes normales.
        _gramaticaOrdenes = CrearGramatica(InterpreteOrdenes.OrdenesPrincipales.Keys, info.Culture, "Ordenes");
        // Gramática 2: «sí» / «no». Empieza apagada.
        _gramaticaConfirmacion = CrearGramatica(InterpreteOrdenes.OrdenesConfirmacion.Keys, info.Culture, "Confirmacion");
        _gramaticaConfirmacion.Enabled = false;

        _reconocedor.LoadGrammar(_gramaticaOrdenes);
        _reconocedor.LoadGrammar(_gramaticaConfirmacion);
        _reconocedor.SetInputToDefaultAudioDevice();
        _reconocedor.SpeechRecognized += AlReconocer;
        return true;
    }

    /// <summary>Lista cerrada de frases con Choices + GrammarBuilder (como en clase).</summary>
    private static Grammar CrearGramatica(IEnumerable<string> frases, CultureInfo cultura, string nombre)
    {
        var constructor = new GrammarBuilder { Culture = cultura };
        constructor.Append(new Choices(frases.ToArray()));
        return new Grammar(constructor) { Name = nombre };
    }

    /// <summary>Lo que pasa cada vez que el reconocedor entiende una de las frases.</summary>
    private void AlReconocer(object? sender, SpeechRecognizedEventArgs e)
    {
        if (_hablando) return; // es el propio programa hablando

        string texto = e.Result.Text;
        float confianza = e.Result.Confidence;
        ComandoVoz comando = InterpreteOrdenes.Interpretar(texto);

        // Solo hacemos caso si está seguro al menos un 60 %.
        bool aceptada = comando != ComandoVoz.Desconocido && InterpreteOrdenes.EsFiable(confianza, _ajustes.ConfianzaMinimaVoz);

        // Si estábamos esperando el sí/no y ya ha llegado, volvemos a lo normal (solo se escucha una vez).
        if (aceptada && EsperandoConfirmacion && comando is ComandoVoz.Si or ComandoVoz.No)
        {
            TerminarConfirmacion();
        }

        OrdenReconocida?.Invoke(this, new OrdenVoz(comando, texto, confianza, aceptada, CalcularRetraso(e.Result)));
    }

    /// <summary>
    /// Tiempo de respuesta de la voz: desde que acabamos de hablar (fin del
    /// audio reconocido) hasta ahora.
    /// </summary>
    private static double? CalcularRetraso(RecognitionResult resultado)
    {
        try
        {
            RecognizedAudio? audio = resultado.Audio;
            if (audio is null) return null;
            DateTime finDeLaFrase = audio.StartTime + audio.Duration;
            double ms = (DateTime.Now - finDeLaFrase).TotalMilliseconds;
            return ms >= 0 ? ms : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Buscamos un reconocedor en español (preferimos es-ES, pero sirve cualquier "es-").</summary>
    private static RecognizerInfo? BuscarReconocedorEspanol()
    {
        var instalados = SpeechRecognitionEngine.InstalledRecognizers();
        return instalados.FirstOrDefault(r => r.Culture.Name.Equals("es-ES", StringComparison.OrdinalIgnoreCase))
               ?? instalados.FirstOrDefault(r => r.Culture.TwoLetterISOLanguageName == "es");
    }

    /// <summary>Si hay alguna voz en español instalada, la usamos para contestar.</summary>
    private void ElegirVozEnEspanol()
    {
        InstalledVoice? voz = _sintetizador.GetInstalledVoices()
            .FirstOrDefault(v => v.Enabled && v.VoiceInfo.Culture.TwoLetterISOLanguageName == "es");
        if (voz is not null) _sintetizador.SelectVoice(voz.VoiceInfo.Name);
    }

    private void Avisar(string accion) => EventoOcurrido?.Invoke(this, EventoNexora.Ahora(OrigenEvento.Voz, accion));

    public void Dispose()
    {
        try
        {
            if (_reconocedor is not null)
            {
                _reconocedor.SpeechRecognized -= AlReconocer;
                _reconocedor.RecognizeAsyncCancel();
                _reconocedor.Dispose();
            }
            _sintetizador.SpeakAsyncCancelAll();
            _sintetizador.Dispose();
        }
        catch
        {
            // Al cerrar el programa nos da igual si algo de esto falla.
        }
    }
}
