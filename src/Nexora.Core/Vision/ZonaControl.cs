using OpenCvSharp;

namespace Nexora.Core.Vision;

/// <summary>
/// Calcula dónde va la zona de control (el recuadro en el que el usuario pone
/// la mano) dentro de la imagen de la cámara.
/// </summary>
/// <remarks>
/// La ponemos a la derecha de la imagen porque, como la imagen va en espejo, es
/// donde cae la mano derecha. Tiene que ser cuadrada porque el modelo de la
/// mano trabaja con imágenes cuadradas (224x224) y si no, la mano saldría
/// estirada.
/// </remarks>
public static class ZonaControl
{
    /// <summary>
    /// Recuadro cuadrado de la zona de control.
    /// </summary>
    /// <param name="tamanoImagen">Tamaño de la imagen de la cámara.</param>
    /// <param name="ladoRelativo">Lado del cuadrado como fracción del alto de la imagen.</param>
    public static Rect Calcular(Size tamanoImagen, double ladoRelativo)
    {
        int lado = (int)(Math.Min(tamanoImagen.Width, tamanoImagen.Height) * Math.Clamp(ladoRelativo, 0.2, 1.0));
        int margen = tamanoImagen.Width / 32; // un poquito separado del borde
        int x = Math.Max(0, tamanoImagen.Width - lado - margen);
        int y = Math.Max(0, (tamanoImagen.Height - lado) / 3); // algo más arriba del centro, que es más cómodo
        return new Rect(x, y, lado, lado);
    }

    /// <summary>
    /// La parte de dentro de la zona (quitando el margen). La punta del dedo en
    /// este recuadro se reparte por toda la pantalla.
    /// </summary>
    public static Rect ZonaUtil(Rect zona, double margenRelativo)
    {
        int margen = (int)(zona.Width * Math.Clamp(margenRelativo, 0, 0.45));
        return new Rect(zona.X + margen, zona.Y + margen, zona.Width - margen * 2, zona.Height - margen * 2);
    }
}
