using Nexora.Core.Utilidades;
using OpenCvSharp;

namespace Nexora.Core.Vision;

/// <summary>
/// Qué hay que pintar en esta imagen según el estabilizador.
/// </summary>
/// <param name="Dibujar">false si no hay que pintar nada (no hay cara desde hace rato).</param>
/// <param name="OjoIzquierdo">Dónde poner el ojo izquierdo del filtro.</param>
/// <param name="OjoDerecho">Dónde poner el ojo derecho del filtro.</param>
/// <param name="Opacidad">Cuánto se ve el filtro (0 = nada, 1 = entero).</param>
public readonly record struct EstadoFiltro(bool Dibujar, Point2f OjoIzquierdo, Point2f OjoDerecho, double Opacidad);

/// <summary>
/// [Ampliación con uso de IA] Hace que el filtro aparezca y desaparezca poco a
/// poco en vez de parpadear.
/// </summary>
/// <remarks>
/// <para>
/// El problema: Haar Cascade a veces no encuentra la cara en una imagen suelta
/// (un parpadeo, la cabeza un poco girada, un cambio de luz). Antes, en esa
/// imagen el filtro desaparecía y en la siguiente volvía a salir, y se notaba
/// un "parpadeo" muy feo.
/// </para>
/// <para>La solución son dos reglas muy sencillas, solo con números:</para>
/// <list type="bullet">
///   <item>Si se pierde la cara, seguimos pintando el filtro en el último sitio
///   bueno, pero cada imagen un poco más transparente. Si la cara vuelve antes
///   de que llegue a 0, ni se nota.</item>
///   <item>Cuando la cara aparece, el filtro no sale de golpe: sube de
///   transparente a opaco en unas pocas imágenes.</item>
/// </list>
/// <para>
/// No toca el detector de clase: solo decide qué hacer con lo que el detector
/// ya ha devuelto. Por eso se puede probar con tests sin cámara.
/// </para>
/// </remarks>
public sealed class EstabilizadorFiltro
{
    private Point2f _ultimoIzq;
    private Point2f _ultimoDer;
    private bool _hayUltimo;
    private double _opacidad;

    /// <summary>Imágenes que tarda en aparecer el filtro (a 30 FPS, 4 son unos 130 ms).</summary>
    public int FotogramasAparecer { get; set; } = 4;

    /// <summary>Imágenes que tarda en desaparecer cuando se pierde la cara (10 son unos 330 ms).</summary>
    public int FotogramasDesaparecer { get; set; } = 10;

    /// <summary>Opacidad actual (0..1). Pública para los tests.</summary>
    public double Opacidad => _opacidad;

    /// <summary>
    /// Mete lo que ha pasado en esta imagen y devuelve qué hay que pintar.
    /// </summary>
    /// <param name="hayCara">true si el detector ha encontrado la cara en esta imagen.</param>
    /// <param name="ojoIzq">Ojo izquierdo (ya suavizado). Se ignora si no hay cara.</param>
    /// <param name="ojoDer">Ojo derecho (ya suavizado). Se ignora si no hay cara.</param>
    public EstadoFiltro Actualizar(bool hayCara, Point2f ojoIzq, Point2f ojoDer)
    {
        if (hayCara)
        {
            _ultimoIzq = ojoIzq;
            _ultimoDer = ojoDer;
            _hayUltimo = true;
            _opacidad = Geometria.Limitar(_opacidad + 1.0 / Math.Max(1, FotogramasAparecer), 0, 1);
        }
        else
        {
            _opacidad = Geometria.Limitar(_opacidad - 1.0 / Math.Max(1, FotogramasDesaparecer), 0, 1);
            // Con decimales, 1 − 3·(1/3) no da 0 exacto, sino 0,0000000000000001.
            if (_opacidad < 1e-6)
            {
                _opacidad = 0;
                _hayUltimo = false;
            }
        }

        return new EstadoFiltro(_hayUltimo && _opacidad > 0, _ultimoIzq, _ultimoDer, _opacidad);
    }

    /// <summary>Lo olvida todo (por ejemplo, al apagar los filtros).</summary>
    public void Reiniciar()
    {
        _hayUltimo = false;
        _opacidad = 0;
    }
}
