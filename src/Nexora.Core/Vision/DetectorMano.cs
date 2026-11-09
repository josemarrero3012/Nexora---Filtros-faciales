using System.Runtime.InteropServices;
using Nexora.Core.Modelos;
using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace Nexora.Core.Vision;

/// <summary>
/// Saca los 21 puntos de la mano con un modelo ya entrenado (el de manos de
/// MediaPipe en formato ONNX) que abrimos con el módulo DNN de OpenCV.
/// </summary>
/// <remarks>
/// <para><b>Ampliación con uso de IA</b> (no lo vimos en clase).</para>
/// <para>
/// Cómo lo explicamos si nos preguntan: el modelo es como el XML del detector
/// de caras, pero en vez de decirnos «aquí hay una cara» nos dice dónde está
/// cada articulación de la mano.
/// </para>
/// <para>
/// El modelo que usamos es <c>handpose_estimation_mediapipe_2023feb.onnx</c>
/// del "model zoo" oficial de OpenCV (licencia Apache 2.0). Lo que espera:
/// </para>
/// <list type="bullet">
///   <item>Entrada: una imagen de 224x224 en RGB, con valores de 0 a 1, en
///   formato NHWC (1 x 224 x 224 x 3), que es como viene de TensorFlow.</item>
///   <item>Salidas: 63 números (21 puntos x, y, z en píxeles de esa imagen de
///   224), la confianza de que hay una mano, si es la izquierda o la derecha y
///   los puntos en 3D "del mundo" (estas dos últimas no las usamos).</item>
/// </list>
/// <para>
/// Para ponérselo fácil no le pasamos la imagen entera, solo un recuadro: la
/// zona de control. Así nos ahorramos un segundo modelo para buscar la palma.
/// </para>
/// </remarks>
public sealed class DetectorMano : IDisposable
{
    /// <summary>Lado de la imagen que espera el modelo.</summary>
    public const int TamanoEntrada = 224;

    private readonly Net _red;
    private readonly string[] _nombresSalidas;

    /// <param name="rutaModelo">Ruta al archivo .onnx del modelo de la mano.</param>
    public DetectorMano(string rutaModelo)
    {
        if (!File.Exists(rutaModelo))
            throw new FileNotFoundException($"No encontramos el modelo de la mano: {rutaModelo}", rutaModelo);

        // ReadNetFromOnnx: OpenCV ya sabe abrir modelos ONNX, así que no hace
        // falta otra librería (en la primera versión íbamos a usar ONNX Runtime).
        _red = CvDnn.ReadNetFromOnnx(rutaModelo)
               ?? throw new InvalidOperationException("OpenCV no pudo abrir el modelo de la mano.");

        // Lo ejecutamos en el procesador (CPU) con el motor propio de OpenCV,
        // que es lo que funciona en cualquier portátil sin tarjeta gráfica.
        _red.SetPreferableBackend(Backend.OPENCV);
        _red.SetPreferableTarget(Target.CPU);

        // Nombres de las capas de salida, para pedirle todas de una vez a Forward.
        _nombresSalidas = _red.GetUnconnectedOutLayersNames()
                          .Where(n => n is not null)
                          .Select(n => n!)
                          .ToArray();
    }

    /// <summary>
    /// Busca la mano dentro de la zona de control.
    /// </summary>
    /// <param name="imagenBgr">Imagen de la cámara (BGR, la de OpenCV).</param>
    /// <param name="zona">Recuadro cuadrado donde el usuario pone la mano.</param>
    /// <param name="confianzaMinima">Por debajo de esto decimos que no hay mano.</param>
    /// <returns>Los 21 puntos en coordenadas de la imagen completa, o null si no hay mano.</returns>
    public ResultadoMano? Detectar(Mat imagenBgr, Rect zona, float confianzaMinima)
    {
        // Por si acaso la zona se sale un poco de la imagen, la recortamos.
        zona &= new Rect(0, 0, imagenBgr.Width, imagenBgr.Height);
        if (zona.Width < 16 || zona.Height < 16) return null;

        // 1) Recortamos la zona de control y la preparamos como le gusta al modelo.
        using var recorte = new Mat(imagenBgr, zona);
        using var rgb = new Mat();
        Cv2.CvtColor(recorte, rgb, ColorConversionCodes.BGR2RGB); // OpenCV guarda en BGR, el modelo quiere RGB

        using var redimensionada = new Mat();
        Cv2.Resize(rgb, redimensionada, new Size(TamanoEntrada, TamanoEntrada), 0, 0, InterpolationFlags.Area);

        using var flotante = new Mat();
        redimensionada.ConvertTo(flotante, MatType.CV_32FC3, 1.0 / 255.0); // de 0..255 a 0..1

        // 2) Una imagen de 224x224 con 3 canales en memoria ya está guardada
        //    como "fila, columna, canal", que es justo el formato NHWC. Solo hay
        //    que decirle a OpenCV que la vea como un bloque de 4 dimensiones.
        using Mat blob = flotante.Reshape(1, 1, TamanoEntrada, TamanoEntrada, 3);

        // 3) Forward: le pasamos la imagen al modelo y recogemos todas las salidas.
        _red.SetInput(blob);
        Mat[] salidas = _nombresSalidas.Select(_ => new Mat()).ToArray();
        try
        {
            _red.Forward(salidas, _nombresSalidas);

            // Las salidas vienen en este orden: puntos, confianza, mano izquierda
            // o derecha, y puntos 3D. Por si acaso, las buscamos por su tamaño:
            // la primera de 63 números son los puntos y la primera de 1 número es
            // la confianza.
            float[]? puntosCrudos = null;
            float? confianza = null;
            foreach (Mat salida in salidas)
            {
                float[] datos = LeerFloats(salida);
                if (puntosCrudos is null && datos.Length == PuntosMano.Total * 3) puntosCrudos = datos;
                else if (confianza is null && datos.Length == 1) confianza = datos[0];
            }

            if (puntosCrudos is null || confianza is null) return null;
            if (confianza.Value < confianzaMinima) return null;

            // 4) Los puntos vienen en píxeles de la imagen de 224x224. Los pasamos
            //    a la imagen completa: escalamos al tamaño de la zona y le sumamos
            //    su esquina.
            float escalaX = zona.Width / (float)TamanoEntrada;
            float escalaY = zona.Height / (float)TamanoEntrada;
            var puntos = new Point2f[PuntosMano.Total];
            for (int i = 0; i < PuntosMano.Total; i++)
            {
                float x = puntosCrudos[i * 3];     // x
                float y = puntosCrudos[i * 3 + 1]; // y  (el tercero es la profundidad z, no lo usamos)
                puntos[i] = new Point2f(zona.X + x * escalaX, zona.Y + y * escalaY);
            }

            return new ResultadoMano(puntos, confianza.Value);
        }
        finally
        {
            foreach (Mat salida in salidas) salida.Dispose();
        }
    }

    /// <summary>
    /// Copia todos los números de una salida del modelo a un array de C#.
    /// </summary>
    private static float[] LeerFloats(Mat salida)
    {
        int total = (int)salida.Total();
        var datos = new float[total];
        if (total == 0) return datos;

        if (salida.IsContinuous())
        {
            Marshal.Copy(salida.Data, datos, 0, total);
        }
        else
        {
            // No debería pasar con las salidas de la red, pero por si acaso.
            using Mat copia = salida.Clone();
            Marshal.Copy(copia.Data, datos, 0, total);
        }
        return datos;
    }

    /// <summary>Libera la red neuronal.</summary>
    public void Dispose() => _red.Dispose();
}
