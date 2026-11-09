using OpenCvSharp;

namespace Nexora.Core.Utilidades;

/// <summary>
/// Cuatro cuentas de geometría que usamos en varios sitios (distancias,
/// puntos medios...). Las ponemos aquí para no repetirlas.
/// </summary>
public static class Geometria
{
    /// <summary>Distancia entre dos puntos (Pitágoras de toda la vida).</summary>
    public static double Distancia(Point2f a, Point2f b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Punto que queda justo en medio de dos puntos.</summary>
    public static Point2f PuntoMedio(Point2f a, Point2f b) => new((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);

    /// <summary>
    /// Ángulo (en grados) de la línea que va de <paramref name="a"/> a
    /// <paramref name="b"/>. Como en las imágenes la Y crece hacia abajo, un
    /// ángulo positivo significa que <paramref name="b"/> está más abajo que
    /// <paramref name="a"/> (la línea "baja" hacia la derecha).
    /// </summary>
    public static double AnguloGrados(Point2f a, Point2f b)
        => Math.Atan2(b.Y - a.Y, b.X - a.X) * 180.0 / Math.PI;

    /// <summary>Centro de un rectángulo.</summary>
    public static Point2f Centro(Rect r) => new(r.X + r.Width / 2f, r.Y + r.Height / 2f);

    /// <summary>Limita un valor entre un mínimo y un máximo.</summary>
    public static double Limitar(double valor, double minimo, double maximo)
        => Math.Max(minimo, Math.Min(maximo, valor));
}
