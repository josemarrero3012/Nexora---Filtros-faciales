using OpenCvSharp;

namespace Nexora.Tests;

/// <summary>
/// Fabrica manos "de mentira" (21 puntos) para probar los gestos sin cámara.
/// </summary>
/// <remarks>
/// La mano está de pie, con la muñeca abajo en (100, 200) y los dedos hacia
/// arriba (la Y de las imágenes crece hacia abajo, así que "arriba" es Y menor).
/// </remarks>
internal static class ManoDePrueba
{
    /// <summary>
    /// Crea una mano con los dedos que queramos estirados.
    /// </summary>
    /// <param name="indice">¿Índice estirado?</param>
    /// <param name="corazon">¿Corazón estirado?</param>
    /// <param name="anular">¿Anular estirado?</param>
    /// <param name="menique">¿Meñique estirado?</param>
    /// <param name="pinza">Si es true, la punta del pulgar se pega a la del índice.</param>
    /// <param name="desplazamientoY">Para mover la mano entera arriba o abajo.</param>
    public static Point2f[] Crear(bool indice, bool corazon, bool anular, bool menique, bool pinza = false, float desplazamientoY = 0)
    {
        var p = new Point2f[21];
        p[0] = new Point2f(100, 200); // muñeca

        // Pulgar, hacia la izquierda y separado del índice
        p[1] = new Point2f(80, 185);
        p[2] = new Point2f(65, 170);
        p[3] = new Point2f(55, 155);
        p[4] = new Point2f(45, 140);

        // Cada dedo: x de su base y si está estirado o no
        Dedo(p, 5, 85, indice);
        Dedo(p, 9, 100, corazon);
        Dedo(p, 13, 115, anular);
        Dedo(p, 17, 130, menique);

        if (pinza)
        {
            // Pulgar tocando la punta del índice
            p[4] = new Point2f(p[8].X + 3, p[8].Y + 3);
        }

        for (int i = 0; i < p.Length; i++) p[i] = new Point2f(p[i].X, p[i].Y + desplazamientoY);
        return p;
    }

    /// <summary>Coloca los 4 puntos de un dedo empezando en el índice <paramref name="mcp"/>.</summary>
    private static void Dedo(Point2f[] p, int mcp, float x, bool estirado)
    {
        p[mcp] = new Point2f(x, 140); // nudillo de la base
        if (estirado)
        {
            p[mcp + 1] = new Point2f(x, 110); // nudillo del medio
            p[mcp + 2] = new Point2f(x, 90);
            p[mcp + 3] = new Point2f(x, 70);  // punta, lejos de la muñeca
        }
        else
        {
            // Dedo doblado: la punta vuelve hacia la palma, más cerca de la muñeca.
            p[mcp + 1] = new Point2f(x, 115);
            p[mcp + 2] = new Point2f(x, 135);
            p[mcp + 3] = new Point2f(x, 150);
        }
    }
}
