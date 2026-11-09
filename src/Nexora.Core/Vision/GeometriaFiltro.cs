using Nexora.Core.Utilidades;
using OpenCvSharp;

namespace Nexora.Core.Vision;

/// <summary>
/// Dónde y cómo hay que colocar un filtro: cuánto escalarlo, cuánto girarlo y
/// en qué punto de la imagen va su centro.
/// </summary>
/// <param name="Escala">Por cuánto hay que multiplicar el tamaño del PNG.</param>
/// <param name="AnguloGrados">Inclinación de la cabeza (positivo = el ojo derecho de la imagen está más bajo).</param>
/// <param name="CentroDestino">Punto medio entre los ojos detectados.</param>
public readonly record struct ColocacionFiltro(double Escala, double AnguloGrados, Point2f CentroDestino);

/// <summary>
/// Las cuentas para colocar un filtro encima de la cara.
/// </summary>
/// <remarks>
/// Lo que explicamos en la propuesta: "los ojos hacen de regla y de
/// transportador". Con la distancia entre los ojos sabemos el tamaño y con la
/// inclinación de la línea que los une, el ángulo.
///
/// Esta clase no toca imágenes, solo números, así que la podemos probar con
/// tests sin necesitar la cámara ni las DLL de OpenCV.
/// </remarks>
public static class GeometriaFiltro
{
    /// <summary>
    /// Calcula la escala, el giro y el centro para que los ojos dibujados en el
    /// PNG caigan justo encima de los ojos detectados.
    /// </summary>
    /// <param name="ojoIzqPng">Ojo izquierdo dentro del PNG del filtro.</param>
    /// <param name="ojoDerPng">Ojo derecho dentro del PNG del filtro.</param>
    /// <param name="ojoIzqCara">Ojo izquierdo detectado en la imagen.</param>
    /// <param name="ojoDerCara">Ojo derecho detectado en la imagen.</param>
    public static ColocacionFiltro Calcular(Point2f ojoIzqPng, Point2f ojoDerPng, Point2f ojoIzqCara, Point2f ojoDerCara)
    {
        double distanciaPng = Geometria.Distancia(ojoIzqPng, ojoDerPng);
        double distanciaCara = Geometria.Distancia(ojoIzqCara, ojoDerCara);

        // Regla: si en la cara los ojos están al doble de distancia que en el
        // PNG, el PNG tiene que salir al doble de grande.
        double escala = distanciaPng > 0 ? distanciaCara / distanciaPng : 1.0;

        // Transportador: el giro es la inclinación de los ojos de la cara menos
        // la de los ojos del PNG (que en nuestros PNG es 0, están rectos).
        double angulo = Geometria.AnguloGrados(ojoIzqCara, ojoDerCara) - Geometria.AnguloGrados(ojoIzqPng, ojoDerPng);

        return new ColocacionFiltro(escala, angulo, Geometria.PuntoMedio(ojoIzqCara, ojoDerCara));
    }

    /// <summary>
    /// Cuando el detector de ojos no los encuentra, los colocamos con las
    /// proporciones típicas del recuadro que da Haar: a un 30 % y un 70 % del
    /// ancho y a un 40 % del alto.
    /// </summary>
    public static (Point2f Izquierdo, Point2f Derecho) OjosPorProporcion(Rect cara)
    {
        float y = cara.Y + cara.Height * 0.40f;
        return (new Point2f(cara.X + cara.Width * 0.30f, y),
                new Point2f(cara.X + cara.Width * 0.70f, y));
    }

    /// <summary>
    /// Matriz 2x3 de giro + escala alrededor de un centro. Es exactamente la
    /// fórmula que usa <c>Cv2.GetRotationMatrix2D</c> (la copiamos de la
    /// documentación de OpenCV) para poder comprobarla en los tests sin las DLL
    /// nativas.
    /// </summary>
    /// <remarks>
    /// <code>
    ///   α = escala·cos(ángulo)     β = escala·sin(ángulo)
    ///   | α   β   (1-α)·cx - β·cy |
    ///   | -β  α   β·cx + (1-α)·cy |
    /// </code>
    /// En OpenCV un ángulo positivo gira en sentido contrario a las agujas del
    /// reloj (tal y como se ve en pantalla).
    /// </remarks>
    public static double[] MatrizRotacion(Point2f centro, double anguloGrados, double escala)
    {
        double rad = anguloGrados * Math.PI / 180.0;
        double alfa = escala * Math.Cos(rad);
        double beta = escala * Math.Sin(rad);
        return
        [
            alfa, beta, (1 - alfa) * centro.X - beta * centro.Y,
            -beta, alfa, beta * centro.X + (1 - alfa) * centro.Y
        ];
    }

    /// <summary>Aplica una matriz 2x3 (en una tabla de 6 números) a un punto.</summary>
    public static Point2f Transformar(double[] m, Point2f p)
        => new((float)(m[0] * p.X + m[1] * p.Y + m[2]),
               (float)(m[3] * p.X + m[4] * p.Y + m[5]));
}
