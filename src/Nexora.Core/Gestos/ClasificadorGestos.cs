using Nexora.Core.Configuracion;
using Nexora.Core.Modelos;
using Nexora.Core.Utilidades;
using Nexora.Core.Vision;
using OpenCvSharp;

namespace Nexora.Core.Gestos;

/// <summary>
/// Decide qué gesto está haciendo la mano a partir de sus 21 puntos.
/// </summary>
/// <remarks>
/// <para>
/// El modelo solo nos da los puntos; decidir qué gesto es lo hacemos nosotros
/// midiendo distancias, igual que en clase comparábamos el centro de la cara
/// con el centro de la pantalla.
/// </para>
/// <para>Reglas (en este orden):</para>
/// <list type="number">
///   <item><b>Clic</b>: la punta del pulgar y la del índice se tocan (pinza).</item>
///   <item><b>Pausa</b>: ningún dedo estirado (puño cerrado). El pulgar no cuenta.</item>
///   <item><b>Mover</b>: solo el índice estirado.</item>
///   <item><b>Desplazar</b>: índice y corazón estirados, anular y meñique doblados.</item>
///   <item><b>Ninguno</b>: cualquier otra cosa (por ejemplo, la mano abierta).</item>
/// </list>
/// <para>
/// Dos trucos para que no "baile":
/// </para>
/// <list type="bullet">
///   <item><b>Histéresis en la pinza</b>: un límite para cerrarla y otro más
///   grande para soltarla. Así, si la distancia está justo en el límite, no se
///   hacen dos clics sin querer.</item>
///   <item><b>Confirmación</b>: un gesto nuevo tiene que verse varias imágenes
///   seguidas antes de hacerle caso.</item>
/// </list>
/// </remarks>
public sealed class ClasificadorGestos
{
    private readonly AjustesNexora _ajustes;

    private bool _pinzaCerrada;            // estado de la pinza (para la histéresis)
    private Gesto _candidato = Gesto.Ninguno; // gesto que estamos "viendo" pero aún no está confirmado
    private int _repeticiones;             // cuántas imágenes seguidas llevamos viendo el candidato
    private double _msPrimeraVez;          // cuándo vimos el candidato por primera vez (para medir la respuesta)

    /// <summary>El último gesto confirmado.</summary>
    public Gesto GestoActual { get; private set; } = Gesto.Ninguno;

    public ClasificadorGestos(AjustesNexora ajustes)
    {
        _ajustes = ajustes;
    }

    /// <summary>
    /// Mira los puntos de la mano de esta imagen y actualiza el gesto.
    /// </summary>
    /// <param name="puntos">Los 21 puntos de la mano.</param>
    /// <param name="msAhora">
    /// Momento actual en milisegundos (cualquier reloj que vaya hacia delante).
    /// Lo pasamos como parámetro para poder hacer pruebas sin esperar.
    /// </param>
    public ResultadoGesto Actualizar(IReadOnlyList<Point2f> puntos, double msAhora)
    {
        if (puntos.Count != PuntosMano.Total)
            throw new ArgumentException($"Se esperaban {PuntosMano.Total} puntos y llegaron {puntos.Count}.", nameof(puntos));

        Gesto visto = ClasificarSinConfirmar(puntos);
        return Confirmar(visto, msAhora);
    }

    /// <summary>
    /// Avisamos de que no hay mano en esta imagen. Cuenta como "Ninguno" y
    /// además soltamos la pinza.
    /// </summary>
    public ResultadoGesto SinMano(double msAhora)
    {
        _pinzaCerrada = false;
        return Confirmar(Gesto.Ninguno, msAhora);
    }

    /// <summary>Lo deja todo como al principio.</summary>
    public void Reiniciar()
    {
        _pinzaCerrada = false;
        _candidato = Gesto.Ninguno;
        _repeticiones = 0;
        GestoActual = Gesto.Ninguno;
    }

    /// <summary>
    /// Decide el gesto SOLO con esta imagen (sin mirar las anteriores, salvo la
    /// histéresis de la pinza).
    /// </summary>
    public Gesto ClasificarSinConfirmar(IReadOnlyList<Point2f> p)
    {
        // Tamaño de la mano: de la muñeca al nudillo del corazón. Lo usamos para
        // que las distancias no dependan de lo cerca o lejos que esté la mano.
        double tamanoMano = Geometria.Distancia(p[PuntosMano.Muneca], p[PuntosMano.CorazonMcp]);
        if (tamanoMano < 1) return Gesto.Ninguno; // puntos raros, mejor no hacer nada

        // --- 1) Pinza (clic) con histéresis ---------------------------------
        double pinza = Geometria.Distancia(p[PuntosMano.PulgarPunta], p[PuntosMano.IndicePunta]) / tamanoMano;
        if (_pinzaCerrada)
        {
            // Ya estaba cerrada: solo la soltamos si se separan BASTANTE.
            if (pinza > _ajustes.UmbralPinzaSoltar) _pinzaCerrada = false;
        }
        else
        {
            // Estaba abierta: solo la cerramos si se juntan MUCHO.
            if (pinza < _ajustes.UmbralPinzaCerrar) _pinzaCerrada = true;
        }
        if (_pinzaCerrada) return Gesto.Clic;

        // --- 2) Qué dedos están estirados -----------------------------------
        bool indice = DedoEstirado(p, PuntosMano.IndicePunta, PuntosMano.IndicePip);
        bool corazon = DedoEstirado(p, PuntosMano.CorazonPunta, PuntosMano.CorazonPip);
        bool anular = DedoEstirado(p, PuntosMano.AnularPunta, PuntosMano.AnularPip);
        bool menique = DedoEstirado(p, PuntosMano.MeniquePunta, PuntosMano.MeniquePip);

        // --- 3) Reglas -------------------------------------------------------
        if (!indice && !corazon && !anular && !menique) return Gesto.Pausa;    // puño
        if (indice && !corazon && !anular && !menique) return Gesto.Mover;      // solo el índice
        if (indice && corazon && !anular && !menique) return Gesto.Desplazar;   // índice + corazón
        return Gesto.Ninguno;
    }

    /// <summary>
    /// Un dedo está estirado si su punta está bastante más lejos de la muñeca
    /// que su nudillo del medio. Cuando el dedo se dobla, la punta vuelve hacia
    /// la palma y queda MÁS CERCA de la muñeca que el nudillo.
    /// </summary>
    /// <remarks>
    /// Lo medimos con distancias a la muñeca (y no mirando si la punta está más
    /// arriba) para que funcione aunque la mano esté un poco girada.
    /// </remarks>
    public bool DedoEstirado(IReadOnlyList<Point2f> p, int punta, int nudillo)
    {
        double hastaPunta = Geometria.Distancia(p[PuntosMano.Muneca], p[punta]);
        double hastaNudillo = Geometria.Distancia(p[PuntosMano.Muneca], p[nudillo]);
        return hastaPunta > hastaNudillo * _ajustes.FactorDedoEstirado;
    }

    /// <summary>
    /// Solo cambiamos de gesto cuando el nuevo se ha visto varias imágenes seguidas.
    /// </summary>
    private ResultadoGesto Confirmar(Gesto visto, double msAhora)
    {
        if (visto == _candidato)
        {
            _repeticiones++;
        }
        else
        {
            _candidato = visto;
            _repeticiones = 1;
            _msPrimeraVez = msAhora;
        }

        // "Ninguno" lo aceptamos más rápido (con la mitad de imágenes) para que
        // el cursor se pare enseguida cuando quitamos la mano.
        int necesarias = visto == Gesto.Ninguno
            ? Math.Max(1, _ajustes.FotogramasParaConfirmarGesto / 2)
            : Math.Max(1, _ajustes.FotogramasParaConfirmarGesto);

        if (_repeticiones >= necesarias && GestoActual != _candidato)
        {
            GestoActual = _candidato;
            bool clic = GestoActual == Gesto.Clic; // el clic se hace UNA vez, al cerrar la pinza
            return new ResultadoGesto(GestoActual, HaCambiado: true, ClicNuevo: clic, MsRespuesta: msAhora - _msPrimeraVez);
        }

        return new ResultadoGesto(GestoActual, HaCambiado: false, ClicNuevo: false, MsRespuesta: 0);
    }
}
