using Nexora.Core.Modelos;
using OpenCvSharp;

namespace Nexora.Core.Vision;

/// <summary>
/// Funciones para dibujar ayudas encima de la imagen: el recuadro de la cara,
/// los ojos, la zona de control y el esqueleto de la mano.
/// </summary>
/// <remarks>
/// Todo con <c>Rectangle</c>, <c>Circle</c>, <c>Line</c> y <c>PutText</c> de
/// OpenCV. Ojo: OpenCV usa los colores en orden BGR (azul, verde, rojo), así
/// que <c>new Scalar(0, 255, 0)</c> es verde y <c>new Scalar(0, 0, 255)</c> es rojo.
/// </remarks>
public static class Dibujante
{
    // Colores (en BGR, no en RGB)
    private static readonly Scalar Verde = new(80, 220, 100);
    private static readonly Scalar Amarillo = new(0, 210, 255);
    private static readonly Scalar Cian = new(230, 200, 40);
    private static readonly Scalar Blanco = new(255, 255, 255);
    private static readonly Scalar Rojo = new(60, 60, 230);
    private static readonly Scalar Negro = new(0, 0, 0);

    /// <summary>Dibuja el recuadro de la cara y un circulito en cada ojo.</summary>
    public static void DibujarCara(Mat imagen, ResultadoCara cara)
    {
        Cv2.Rectangle(imagen, cara.Cara, Verde, 2);

        // Ojos en amarillo si los ha encontrado el detector, en rojo si son "a ojo".
        Scalar colorOjos = cara.OjosDetectados ? Amarillo : Rojo;
        Cv2.Circle(imagen, ARedondo(cara.OjoIzquierdo), 5, colorOjos, -1, LineTypes.AntiAlias);
        Cv2.Circle(imagen, ARedondo(cara.OjoDerecho), 5, colorOjos, -1, LineTypes.AntiAlias);
    }

    /// <summary>Dibuja la zona de control (donde hay que poner la mano) y su margen interior.</summary>
    public static void DibujarZonaControl(Mat imagen, Rect zona, Rect zonaUtil, bool hayMano)
    {
        Cv2.Rectangle(imagen, zona, hayMano ? Verde : Cian, 2);
        // El margen interior con una línea fina: la punta del dedo en ese recuadro
        // pequeño ya llega a todas las esquinas de la pantalla.
        Cv2.Rectangle(imagen, zonaUtil, Cian, 1);
        EscribirTexto(imagen, "Zona de control", new Point(zona.X + 4, zona.Y - 8), 0.5);
    }

    /// <summary>Dibuja los 21 puntos de la mano y las líneas que los unen.</summary>
    public static void DibujarMano(Mat imagen, ResultadoMano mano)
    {
        foreach ((int desde, int hasta) in PuntosMano.Huesos)
        {
            Cv2.Line(imagen, ARedondo(mano.Puntos[desde]), ARedondo(mano.Puntos[hasta]), Blanco, 2, LineTypes.AntiAlias);
        }

        for (int i = 0; i < mano.Puntos.Length; i++)
        {
            // Las puntas de los dedos un poco más grandes para que se vean bien.
            bool esPunta = i is PuntosMano.PulgarPunta or PuntosMano.IndicePunta or PuntosMano.CorazonPunta
                or PuntosMano.AnularPunta or PuntosMano.MeniquePunta;
            Cv2.Circle(imagen, ARedondo(mano.Puntos[i]), esPunta ? 6 : 4, esPunta ? Rojo : Verde, -1, LineTypes.AntiAlias);
        }
    }

    /// <summary>
    /// Dibuja el centro de la imagen y la zona muerta del plan B (cursor con la
    /// cabeza), igual que en el ejercicio de clase.
    /// </summary>
    public static void DibujarZonaMuertaCabeza(Mat imagen, double radio, Point2f centroCara)
    {
        var centro = new Point(imagen.Width / 2, imagen.Height / 2);
        Cv2.Rectangle(imagen,
            new Rect(centro.X - (int)radio, centro.Y - (int)radio, (int)(radio * 2), (int)(radio * 2)),
            Cian, 1);
        Cv2.Line(imagen, centro, ARedondo(centroCara), Amarillo, 2, LineTypes.AntiAlias);
        Cv2.Circle(imagen, ARedondo(centroCara), 6, Amarillo, -1, LineTypes.AntiAlias);
    }

    /// <summary>
    /// Escribe un texto con un "borde" negro para que se lea bien sobre cualquier fondo.
    /// </summary>
    public static void EscribirTexto(Mat imagen, string texto, Point posicion, double tamano = 0.6)
    {
        // OpenCV no sabe pintar tildes con PutText, así que las quitamos.
        string limpio = QuitarTildes(texto);
        Cv2.PutText(imagen, limpio, posicion, HersheyFonts.HersheySimplex, tamano, Negro, 4, LineTypes.AntiAlias);
        Cv2.PutText(imagen, limpio, posicion, HersheyFonts.HersheySimplex, tamano, Blanco, 1, LineTypes.AntiAlias);
    }

    private static Point ARedondo(Point2f p) => new((int)Math.Round(p.X), (int)Math.Round(p.Y));

    private static string QuitarTildes(string texto)
        => texto.Replace('á', 'a').Replace('é', 'e').Replace('í', 'i').Replace('ó', 'o').Replace('ú', 'u')
                .Replace('Á', 'A').Replace('É', 'E').Replace('Í', 'I').Replace('Ó', 'O').Replace('Ú', 'U')
                .Replace('ñ', 'n').Replace('Ñ', 'N');
}
