using OpenCvSharp;

namespace Nexora.Core.Utilidades;

/// <summary>
/// Suaviza un punto que va cambiando imagen a imagen (media móvil exponencial).
/// </summary>
/// <remarks>
/// La idea es la misma que el suavizado del cursor de clase: en vez de saltar
/// directamente al punto nuevo, nos movemos solo un trozo del camino
/// (<c>factor</c>). Si factor = 1 no suaviza nada; si factor = 0,2 va muy suave
/// pero con algo de retraso.
///
///     nuevo = anterior + factor * (medido - anterior)
/// </remarks>
public sealed class SuavizadorPunto
{
    private Point2f _valor;
    private bool _tieneValor;

    /// <summary>true cuando ya tenemos al menos un punto guardado.</summary>
    public bool TieneValor => _tieneValor;

    /// <summary>Último valor suavizado.</summary>
    public Point2f Valor => _valor;

    /// <summary>
    /// Mete un punto nuevo y devuelve el valor ya suavizado.
    /// </summary>
    /// <param name="medido">Punto que acabamos de medir en esta imagen.</param>
    /// <param name="factor">Cuánto nos fiamos del punto nuevo (0..1).</param>
    public Point2f Actualizar(Point2f medido, double factor)
    {
        if (!_tieneValor)
        {
            // La primera vez no hay nada que suavizar: nos quedamos con el punto tal cual.
            _valor = medido;
            _tieneValor = true;
            return _valor;
        }

        factor = Geometria.Limitar(factor, 0, 1);
        _valor = new Point2f(
            (float)(_valor.X + factor * (medido.X - _valor.X)),
            (float)(_valor.Y + factor * (medido.Y - _valor.Y)));
        return _valor;
    }

    /// <summary>Olvida el punto guardado (por ejemplo, cuando se pierde la cara).</summary>
    public void Reiniciar() => _tieneValor = false;
}
