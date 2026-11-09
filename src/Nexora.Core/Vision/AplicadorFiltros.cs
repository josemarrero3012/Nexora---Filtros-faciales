using Nexora.Core.Modelos;
using OpenCvSharp;

namespace Nexora.Core.Vision;

/// <summary>
/// Pega el PNG del filtro encima de la imagen de la cámara: lo escala, lo gira
/// y lo mezcla respetando su transparencia.
/// </summary>
/// <remarks>
/// <para>Funciones de OpenCV que usamos (las de la tabla de la propuesta):</para>
/// <list type="bullet">
///   <item><c>Resize</c>: cambiar el tamaño del PNG según la distancia entre los ojos.</item>
///   <item><c>GetRotationMatrix2D</c>: sacar la matriz de giro según la inclinación de los ojos.</item>
///   <item><c>WarpAffine</c>: aplicar esa matriz al PNG (girarlo y llevarlo a su sitio).</item>
/// </list>
/// <para>
/// La mezcla con transparencia es la fórmula de toda la vida:
/// <c>resultado = fondo · (1 − alfa) + filtro · alfa</c>, donde alfa es el
/// cuarto canal del PNG (0 = transparente, 255 = opaco).
/// </para>
/// </remarks>
public sealed class AplicadorFiltros : IDisposable
{
    // PNG ya cargados en memoria (BGRA, 4 canales), uno por tipo de filtro.
    private readonly Dictionary<TipoFiltro, Mat> _imagenes = new();

    /// <summary>
    /// Carga los PNG de todos los filtros del catálogo.
    /// </summary>
    /// <param name="carpetaFiltros">Carpeta donde están gafas.png, orejas.png y mascara.png.</param>
    public AplicadorFiltros(string carpetaFiltros)
    {
        foreach (DefinicionFiltro definicion in CatalogoFiltros.Todos)
        {
            string ruta = Path.Combine(carpetaFiltros, definicion.Archivo);
            if (!File.Exists(ruta))
                throw new FileNotFoundException($"Falta el PNG del filtro {definicion.NombreVisible}: {ruta}", ruta);

            // Unchanged = que NO quite el canal alfa. Si lo leyéramos en color
            // normal perderíamos la transparencia y se vería un cuadrado.
            Mat png = Cv2.ImRead(ruta, ImreadModes.Unchanged);
            if (png.Empty() || png.Channels() != 4)
            {
                png.Dispose();
                throw new InvalidOperationException($"El PNG {ruta} tiene que tener transparencia (4 canales).");
            }
            _imagenes[definicion.Tipo] = png;
        }
    }

    /// <summary>
    /// Dibuja el filtro sobre la imagen colocándolo encima de los ojos.
    /// </summary>
    /// <param name="imagen">Imagen de la cámara en BGR (se modifica directamente).</param>
    /// <param name="tipo">Qué filtro poner. Si es Ninguno, no hace nada.</param>
    /// <param name="ojoIzq">Ojo izquierdo de la imagen.</param>
    /// <param name="ojoDer">Ojo derecho de la imagen.</param>
    public void Aplicar(Mat imagen, TipoFiltro tipo, Point2f ojoIzq, Point2f ojoDer)
    {
        DefinicionFiltro? definicion = CatalogoFiltros.Buscar(tipo);
        if (definicion is null || !_imagenes.TryGetValue(tipo, out Mat? png)) return;

        // 1) ¿Cuánto escalar y girar? (los ojos hacen de regla y de transportador)
        ColocacionFiltro colocacion = GeometriaFiltro.Calcular(definicion.OjoIzquierdo, definicion.OjoDerecho, ojoIzq, ojoDer);

        int ancho = (int)Math.Round(png.Width * colocacion.Escala);
        int alto = (int)Math.Round(png.Height * colocacion.Escala);
        if (ancho < 2 || alto < 2) return; // demasiado pequeño para pintarlo

        // 2) Resize: el PNG al tamaño de la cara.
        using var escalado = new Mat();
        Cv2.Resize(png, escalado, new Size(ancho, alto), 0, 0, InterpolationFlags.Area);

        // Punto medio entre los ojos del PNG, ya escalado. Es el "pivote" del giro.
        var pivote = new Point2f(
            (float)((definicion.OjoIzquierdo.X + definicion.OjoDerecho.X) / 2 * colocacion.Escala),
            (float)((definicion.OjoIzquierdo.Y + definicion.OjoDerecho.Y) / 2 * colocacion.Escala));

        // 3) GetRotationMatrix2D: OpenCV gira en sentido contrario a las agujas
        //    del reloj con ángulos positivos. Nuestro ángulo positivo significa
        //    que la cabeza está inclinada hacia la derecha (sentido de las agujas),
        //    por eso le pasamos el ángulo cambiado de signo.
        using Mat matriz = Cv2.GetRotationMatrix2D(pivote, -colocacion.AnguloGrados, 1.0);

        // Además de girar, hay que mover el PNG para que su pivote caiga entre
        // los ojos de la cara. Eso es sumar un desplazamiento a la última columna.
        double desplX = colocacion.CentroDestino.X - pivote.X;
        double desplY = colocacion.CentroDestino.Y - pivote.Y;
        double[] m =
        [
            matriz.At<double>(0, 0), matriz.At<double>(0, 1), matriz.At<double>(0, 2) + desplX,
            matriz.At<double>(1, 0), matriz.At<double>(1, 1), matriz.At<double>(1, 2) + desplY
        ];

        // 4) Para no trabajar con la imagen entera, calculamos qué recuadro de
        //    la imagen ocupa el filtro una vez girado (sus 4 esquinas) y solo
        //    tocamos ese trozo (la "región de interés").
        Rect zona = RecuadroOcupado(m, ancho, alto) & new Rect(0, 0, imagen.Width, imagen.Height);
        if (zona.Width <= 0 || zona.Height <= 0) return; // el filtro cae fuera de la imagen

        // Pasamos la matriz a coordenadas de la zona (le restamos su esquina).
        using var matrizZona = new Mat(2, 3, MatType.CV_64FC1);
        matrizZona.Set(0, 0, m[0]); matrizZona.Set(0, 1, m[1]); matrizZona.Set(0, 2, m[2] - zona.X);
        matrizZona.Set(1, 0, m[3]); matrizZona.Set(1, 1, m[4]); matrizZona.Set(1, 2, m[5] - zona.Y);

        // 5) WarpAffine: giramos y colocamos el PNG. Lo que queda fuera del PNG
        //    se rellena con transparente (todo a 0, alfa incluido).
        using var capa = new Mat();
        Cv2.WarpAffine(escalado, capa, matrizZona, zona.Size, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));

        // 6) Mezclamos con la imagen respetando la transparencia.
        using var trozoImagen = new Mat(imagen, zona);
        MezclarConAlfa(trozoImagen, capa);
    }

    /// <summary>
    /// Mezcla una capa BGRA encima de un trozo de imagen BGR usando su canal alfa.
    /// </summary>
    /// <param name="fondo">Trozo de la imagen de la cámara (BGR, 3 canales). Se modifica.</param>
    /// <param name="capa">El filtro ya girado (BGRA, 4 canales) del mismo tamaño.</param>
    public static void MezclarConAlfa(Mat fondo, Mat capa)
    {
        // Separamos los 4 canales del PNG: azul, verde, rojo y alfa.
        Mat[] canales = Cv2.Split(capa);
        try
        {
            using var colorFiltro = new Mat();
            Cv2.Merge([canales[0], canales[1], canales[2]], colorFiltro);

            // Alfa de 0..255 a 0..1 y repetido en 3 canales para multiplicar fácil.
            using var alfa = new Mat();
            canales[3].ConvertTo(alfa, MatType.CV_32FC1, 1.0 / 255.0);
            using var alfa3 = new Mat();
            Cv2.Merge([alfa, alfa, alfa], alfa3);

            // Trabajamos en coma flotante para que no se "corten" los números.
            using var fondoF = new Mat();
            using var filtroF = new Mat();
            fondo.ConvertTo(fondoF, MatType.CV_32FC3);
            colorFiltro.ConvertTo(filtroF, MatType.CV_32FC3);

            // resultado = fondo + alfa · (filtro − fondo)
            // (es lo mismo que fondo·(1−alfa) + filtro·alfa, pero con una resta menos)
            using var diferencia = new Mat();
            Cv2.Subtract(filtroF, fondoF, diferencia);
            Cv2.Multiply(diferencia, alfa3, diferencia);
            Cv2.Add(fondoF, diferencia, fondoF);

            // Volvemos a 8 bits escribiendo directamente encima del trozo de imagen.
            fondoF.ConvertTo(fondo, MatType.CV_8UC3);
        }
        finally
        {
            foreach (Mat canal in canales) canal.Dispose();
        }
    }

    /// <summary>
    /// Recuadro (alineado con los ejes) que ocupa un rectángulo de
    /// <paramref name="ancho"/> x <paramref name="alto"/> después de aplicarle la matriz.
    /// </summary>
    private static Rect RecuadroOcupado(double[] m, int ancho, int alto)
    {
        Point2f[] esquinas =
        [
            GeometriaFiltro.Transformar(m, new Point2f(0, 0)),
            GeometriaFiltro.Transformar(m, new Point2f(ancho, 0)),
            GeometriaFiltro.Transformar(m, new Point2f(0, alto)),
            GeometriaFiltro.Transformar(m, new Point2f(ancho, alto))
        ];
        int x1 = (int)Math.Floor(esquinas.Min(p => p.X));
        int y1 = (int)Math.Floor(esquinas.Min(p => p.Y));
        int x2 = (int)Math.Ceiling(esquinas.Max(p => p.X));
        int y2 = (int)Math.Ceiling(esquinas.Max(p => p.Y));
        return new Rect(x1, y1, x2 - x1, y2 - y1);
    }

    /// <summary>Libera los PNG cargados.</summary>
    public void Dispose()
    {
        foreach (Mat imagen in _imagenes.Values) imagen.Dispose();
        _imagenes.Clear();
    }
}
