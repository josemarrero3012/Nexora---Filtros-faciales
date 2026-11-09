using Nexora.Core.Configuracion;
using Nexora.Core.Modelos;
using OpenCvSharp;

namespace Nexora.Core.Vision;

/// <summary>
/// Busca la cara y los ojos con Haar Cascade, tal y como lo hicimos en clase.
/// </summary>
/// <remarks>
/// <para>Pasos:</para>
/// <list type="number">
///   <item>Con la imagen en gris, <c>DetectMultiScale</c> recorre la imagen con
///   una ventanita a varios tamaños buscando el dibujo de luces y sombras de una
///   cara (factor 1,1, cuatro vecinos y mínimo 80x80).</item>
///   <item>Nos quedamos con la cara más grande (la de la persona que está delante).</item>
///   <item>Dentro de la mitad de arriba de la cara buscamos los ojos con el MISMO
///   método, pero cargando el archivo entrenado para ojos.</item>
///   <item>Si no encuentra los dos ojos, los "inventamos" con las proporciones del
///   recuadro de la cara para que el filtro no se quede colgando.</item>
/// </list>
/// </remarks>
public sealed class DetectorCaras : IDisposable
{
    private readonly CascadeClassifier _clasificadorCara;
    private readonly CascadeClassifier _clasificadorOjos;
    private readonly AjustesNexora _ajustes;

    /// <param name="rutaCascadaCara">Ruta a haarcascade_frontalface_default.xml.</param>
    /// <param name="rutaCascadaOjos">Ruta a haarcascade_eye.xml.</param>
    /// <param name="ajustes">Ajustes compartidos (factor de escala, vecinos...).</param>
    public DetectorCaras(string rutaCascadaCara, string rutaCascadaOjos, AjustesNexora ajustes)
    {
        _ajustes = ajustes;
        _clasificadorCara = CargarCascada(rutaCascadaCara);
        _clasificadorOjos = CargarCascada(rutaCascadaOjos);
    }

    /// <summary>
    /// Busca la cara principal y sus ojos en la imagen en gris.
    /// </summary>
    /// <param name="gris">Imagen de la cámara ya pasada a gris (como trabaja Haar).</param>
    /// <returns>La cara con sus ojos, o null si no hay ninguna cara.</returns>
    public ResultadoCara? Detectar(Mat gris)
    {
        // 1) Buscamos todas las caras. Los parámetros son los de clase, pero
        //    sacados a AjustesNexora para poder probar otros valores.
        Rect[] caras = _clasificadorCara.DetectMultiScale(
            gris,
            scaleFactor: _ajustes.FactorEscalaCara,
            minNeighbors: _ajustes.VecinosMinimosCara,
            flags: HaarDetectionTypes.ScaleImage,
            minSize: new Size(_ajustes.LadoMinimoCara, _ajustes.LadoMinimoCara));

        if (caras.Length == 0) return null;

        // 2) Si sale más de una cara, nos quedamos con la más grande, que suele
        //    ser la de quien está usando el programa.
        Rect cara = caras.MaxBy(c => c.Width * c.Height);

        // 3) Buscamos los ojos solo en la mitad de arriba de la cara. Así va más
        //    rápido y no confunde la boca o la nariz con un ojo.
        if (BuscarOjos(gris, cara, out Point2f ojoIzq, out Point2f ojoDer))
        {
            return new ResultadoCara(cara, ojoIzq, ojoDer, OjosDetectados: true);
        }

        // 4) Plan "a ojo": colocamos los ojos según las proporciones de la cara.
        (Point2f izq, Point2f der) = GeometriaFiltro.OjosPorProporcion(cara);
        return new ResultadoCara(cara, izq, der, OjosDetectados: false);
    }

    /// <summary>
    /// Busca los dos ojos dentro de la mitad superior de la cara.
    /// </summary>
    /// <returns>true si encuentra uno a cada lado y a una distancia razonable.</returns>
    private bool BuscarOjos(Mat gris, Rect cara, out Point2f ojoIzq, out Point2f ojoDer)
    {
        ojoIzq = default;
        ojoDer = default;

        // Región de interés (ROI): la mitad de arriba de la cara.
        var mitadSuperior = new Rect(cara.X, cara.Y, cara.Width, cara.Height / 2);
        using var zonaOjos = new Mat(gris, mitadSuperior);

        int ladoMinimo = Math.Max(10, cara.Width / 8); // un ojo mide más o menos 1/5 de la cara
        Rect[] ojos = _clasificadorOjos.DetectMultiScale(
            zonaOjos,
            scaleFactor: _ajustes.FactorEscalaOjos,
            minNeighbors: _ajustes.VecinosMinimosOjos,
            flags: HaarDetectionTypes.ScaleImage,
            minSize: new Size(ladoMinimo, ladoMinimo));

        if (ojos.Length < 2) return false;

        // Separamos los candidatos según estén en la mitad izquierda o derecha
        // de la cara, y de cada lado nos quedamos con el más grande.
        float mitadX = cara.Width / 2f;
        Rect? mejorIzq = ojos.Where(o => o.X + o.Width / 2f < mitadX).OrderByDescending(o => o.Width * o.Height).Cast<Rect?>().FirstOrDefault();
        Rect? mejorDer = ojos.Where(o => o.X + o.Width / 2f >= mitadX).OrderByDescending(o => o.Width * o.Height).Cast<Rect?>().FirstOrDefault();

        if (mejorIzq is null || mejorDer is null) return false;

        // Los rectángulos de los ojos vienen en coordenadas de la ROI, así que
        // les sumamos la esquina de la cara para pasarlos a la imagen entera.
        ojoIzq = new Point2f(cara.X + mejorIzq.Value.X + mejorIzq.Value.Width / 2f,
                             cara.Y + mejorIzq.Value.Y + mejorIzq.Value.Height / 2f);
        ojoDer = new Point2f(cara.X + mejorDer.Value.X + mejorDer.Value.Width / 2f,
                             cara.Y + mejorDer.Value.Y + mejorDer.Value.Height / 2f);

        // Comprobación de sentido común: los ojos suelen estar separados entre
        // un 25 % y un 65 % del ancho de la cara. Si no, algo ha salido raro.
        double separacion = Math.Abs(ojoDer.X - ojoIzq.X) / cara.Width;
        return separacion is > 0.25 and < 0.65;
    }

    /// <summary>Carga un XML de Haar y avisa con un error claro si no está o está roto.</summary>
    private static CascadeClassifier CargarCascada(string ruta)
    {
        if (!File.Exists(ruta))
            throw new FileNotFoundException($"No encontramos el archivo de Haar Cascade: {ruta}", ruta);

        var clasificador = new CascadeClassifier(ruta);
        if (clasificador.Empty())
        {
            clasificador.Dispose();
            throw new InvalidOperationException($"OpenCV no pudo cargar el archivo de Haar Cascade: {ruta}");
        }
        return clasificador;
    }

    /// <summary>Libera los clasificadores (son objetos nativos de OpenCV).</summary>
    public void Dispose()
    {
        _clasificadorCara.Dispose();
        _clasificadorOjos.Dispose();
    }
}
