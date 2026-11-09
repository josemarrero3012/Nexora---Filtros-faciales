using System.Diagnostics;
using Nexora.Core.Configuracion;
using Nexora.Core.Cursor;
using Nexora.Core.Gestos;
using Nexora.Core.Modelos;
using Nexora.Core.Vision;
using OpenCvSharp;

namespace Nexora.App.Servicios;

/// <summary>
/// Módulo de control por gestos: la mano hace de ratón. También lleva el plan B
/// (mover el cursor con la cabeza), porque los dos hacen lo mismo: mover el cursor.
/// </summary>
/// <remarks>
/// <para>Recorrido de cada imagen con la mano:</para>
/// <list type="number">
///   <item><see cref="DetectorMano"/> saca los 21 puntos dentro de la zona de control.</item>
///   <item><see cref="ClasificadorGestos"/> decide el gesto (mover, clic, desplazar o pausa).</item>
///   <item><see cref="ControlCursor"/> calcula a dónde va el cursor (suavizado + zona muerta).</item>
///   <item><see cref="ServicioRaton"/> lo mueve de verdad con SetCursorPos / SendInput.</item>
/// </list>
/// Se llama desde el hilo de la cámara.
/// </remarks>
public sealed class ServicioGestos : IDisposable
{
    private readonly AjustesNexora _ajustes;
    private readonly ServicioRaton _raton;
    private readonly DetectorMano? _detector;
    private readonly ClasificadorGestos _clasificador;
    private readonly ControlCursor _cursor;
    private readonly Stopwatch _reloj = Stopwatch.StartNew();
    private bool _habiaMano;

    /// <summary>Avisa de cosas para el historial (gesto nuevo, clic...).</summary>
    public event EventHandler<EventoNexora>? EventoOcurrido;

    /// <summary>Avisa cuando cambia el gesto (para enseñarlo en la ventana).</summary>
    public event EventHandler<ResultadoGesto>? GestoCambiado;

    /// <summary>false si no se pudo cargar el modelo de la mano (entonces solo queda el plan B).</summary>
    public bool ModeloDisponible => _detector is not null;

    /// <summary>Si el modelo no cargó, aquí está el motivo.</summary>
    public string? ErrorModelo { get; }

    public ServicioGestos(AjustesNexora ajustes, ServicioRaton raton)
    {
        _ajustes = ajustes;
        _raton = raton;
        _clasificador = new ClasificadorGestos(ajustes);
        _cursor = new ControlCursor(ajustes);

        // Si el modelo falla no tiramos todo el programa: seguimos sin mano y
        // se puede usar el plan B con la cabeza.
        try
        {
            _detector = new DetectorMano(RutasRecursos.ModeloMano);
        }
        catch (Exception ex)
        {
            ErrorModelo = ex.Message;
        }
    }

    /// <summary>
    /// Procesa una imagen buscando la mano y ejecuta el gesto.
    /// </summary>
    /// <param name="imagen">Imagen de la cámara (ya volteada). Se dibuja encima.</param>
    public void ProcesarMano(Mat imagen)
    {
        if (_detector is null) return;

        Rect zona = ZonaControl.Calcular(imagen.Size(), _ajustes.LadoZonaControl);
        Rect zonaUtil = ZonaControl.ZonaUtil(zona, _ajustes.MargenZonaControl);

        ResultadoMano? mano = _detector.Detectar(imagen, zona, _ajustes.ConfianzaMinimaMano);
        double ahora = _reloj.Elapsed.TotalMilliseconds;

        // Avisamos solo cuando la mano entra o sale de la zona (no en cada imagen).
        if ((mano is not null) != _habiaMano)
        {
            _habiaMano = mano is not null;
            Avisar(_habiaMano ? "Mano en la zona de control" : "Mano fuera de la zona");
        }

        ResultadoGesto resultado = mano is null
            ? _clasificador.SinMano(ahora)
            : _clasificador.Actualizar(mano.Puntos, ahora);

        if (resultado.HaCambiado) AlCambiarGesto(resultado);

        if (mano is not null) EjecutarGesto(resultado, mano, zonaUtil);

        if (_ajustes.MostrarAyudas)
        {
            Dibujante.DibujarZonaControl(imagen, zona, zonaUtil, mano is not null);
            if (mano is not null) Dibujante.DibujarMano(imagen, mano);
            Dibujante.EscribirTexto(imagen, $"Gesto: {resultado.Gesto}", new Point(zona.X + 4, zona.Bottom + 22));
        }
    }

    /// <summary>
    /// Plan B: mueve el cursor con la cabeza usando la zona muerta, como el
    /// mando de clase. El clic se hace diciendo «clic».
    /// </summary>
    /// <param name="imagen">Imagen de la cámara (se dibuja encima).</param>
    /// <param name="cara">Cara encontrada en esta imagen (o null).</param>
    public void ProcesarCabeza(Mat imagen, ResultadoCara? cara)
    {
        if (cara is null) return;

        Point? nueva = _cursor.PosicionConCabeza(cara.Centro, imagen.Size(), _raton.PosicionActual, _raton.TamanoPantalla);
        if (nueva is { } posicion) _raton.MoverA(posicion);

        if (_ajustes.MostrarAyudas)
        {
            Dibujante.DibujarZonaMuertaCabeza(imagen, _ajustes.ZonaMuertaCabezaPx, cara.Centro);
            Dibujante.EscribirTexto(imagen, "Plan B: cursor con la cabeza", new Point(10, imagen.Height - 14));
        }
    }

    /// <summary>Lo deja todo a cero (por ejemplo, al apagar el control gestual).</summary>
    public void Reiniciar()
    {
        _clasificador.Reiniciar();
        _cursor.Reiniciar();
        _habiaMano = false;
    }

    /// <summary>Hace lo que toca según el gesto confirmado.</summary>
    private void EjecutarGesto(ResultadoGesto resultado, ResultadoMano mano, Rect zonaUtil)
    {
        switch (resultado.Gesto)
        {
            case Gesto.Mover:
                // La punta del índice es el cursor.
                Point? nueva = _cursor.PosicionConMano(mano.Puntos[PuntosMano.IndicePunta], zonaUtil, _raton.TamanoPantalla);
                if (nueva is { } posicion) _raton.MoverA(posicion);
                break;

            case Gesto.Clic:
                // Solo hacemos clic en la imagen en la que se cierra la pinza.
                if (resultado.ClicNuevo)
                {
                    _raton.Clic();
                    Avisar("Clic (pinza)");
                }
                break;

            case Gesto.Desplazar:
                // Usamos la altura media de las puntas del índice y el corazón.
                float y = (mano.Puntos[PuntosMano.IndicePunta].Y + mano.Puntos[PuntosMano.CorazonPunta].Y) / 2f;
                int rueda = _cursor.Desplazamiento(y);
                if (rueda != 0) _raton.Rueda(rueda);
                break;

            case Gesto.Pausa:
            case Gesto.Ninguno:
                // El cursor se queda quieto: podemos recolocar la mano sin arrastrarlo.
                break;
        }
    }

    /// <summary>Lo que hay que hacer justo cuando cambia el gesto.</summary>
    private void AlCambiarGesto(ResultadoGesto resultado)
    {
        // Al dejar de desplazar, olvidamos la altura de la mano.
        if (resultado.Gesto != Gesto.Desplazar) _cursor.TerminarDesplazamiento();

        // Al hacer pausa o perder la mano, olvidamos el suavizado. Así, cuando
        // volvamos a "Mover", el cursor no hace un viaje raro desde donde estaba.
        if (resultado.Gesto is Gesto.Pausa or Gesto.Ninguno) _cursor.Reiniciar();

        // El clic ya lo apuntamos aparte, y "Ninguno" no hace falta apuntarlo.
        if (resultado.Gesto is not (Gesto.Clic or Gesto.Ninguno))
        {
            Avisar($"Gesto: {resultado.Gesto} ({resultado.MsRespuesta:0} ms)");
        }

        GestoCambiado?.Invoke(this, resultado);
    }

    private void Avisar(string accion) => EventoOcurrido?.Invoke(this, EventoNexora.Ahora(OrigenEvento.Mano, accion));

    public void Dispose() => _detector?.Dispose();
}
