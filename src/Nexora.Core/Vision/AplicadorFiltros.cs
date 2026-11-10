using Nexora.Core.Modelos;
using OpenCvSharp;

namespace Nexora.Core.Vision;

/// <summary>
/// [Ampliación con uso de IA] Cómo de "realista" queremos pintar el filtro.
/// </summary>
/// <param name="Opacidad">0..1. La usa el <see cref="EstabilizadorFiltro"/> para que aparezca y desaparezca suave.</param>
/// <param name="Sombra">Pintar una sombra suave debajo del filtro (para que no parezca una pegatina).</param>
/// <param name="AjustarLuz">Oscurecer el filtro si la cara está poco iluminada (para que "viva" en la misma luz).</param>
public readonly record struct OpcionesFiltro(double Opacidad = 1.0, bool Sombra = false, bool AjustarLuz = false)
{
    /// <summary>Lo de siempre: el filtro tal cual, sin sombra ni ajuste de luz.</summary>
    public static OpcionesFiltro Basico => new(1.0, false, false);
}

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
        => Aplicar(imagen, tipo, ojoIzq, ojoDer, OpcionesFiltro.Basico);

    /// <summary>
    /// Igual que el anterior, pero con opacidad, sombra y ajuste de luz
    /// (ver <see cref="OpcionesFiltro"/>). Los pasos 1 a 5 son los mismos.
    /// </summary>
    public void Aplicar(Mat imagen, TipoFiltro tipo, Point2f ojoIzq, Point2f ojoDer, OpcionesFiltro opciones)
    {
        if (opciones.Opacidad <= 0) return;
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
        Rect zona = RecuadroOcupado(m, ancho, alto);

        // [IA] La sombra se desplaza y se difumina, así que ocupa un poco más
        // que el filtro. Agrandamos la zona para que no se corte por los bordes.
        int desplSombra = Math.Max(2, (int)Math.Round(ancho * ProporcionDesplazamientoSombra));
        int radioSombra = Math.Max(1, (int)Math.Round(ancho * ProporcionDifuminadoSombra));
        if (opciones.Sombra)
        {
            int margen = desplSombra + radioSombra * 2;
            zona = new Rect(zona.X - margen, zona.Y - margen, zona.Width + margen * 2, zona.Height + margen * 2);
        }
        zona &= new Rect(0, 0, imagen.Width, imagen.Height);
        if (zona.Width <= 0 || zona.Height <= 0) return; // el filtro cae fuera de la imagen

        // Pasamos la matriz a coordenadas de la zona (le restamos su esquina).
        using var matrizZona = new Mat(2, 3, MatType.CV_64FC1);
        matrizZona.Set(0, 0, m[0]); matrizZona.Set(0, 1, m[1]); matrizZona.Set(0, 2, m[2] - zona.X);
        matrizZona.Set(1, 0, m[3]); matrizZona.Set(1, 1, m[4]); matrizZona.Set(1, 2, m[5] - zona.Y);

        // 5) WarpAffine: giramos y colocamos el PNG. Lo que queda fuera del PNG
        //    se rellena con transparente (todo a 0, alfa incluido).
        using var capa = new Mat();
        Cv2.WarpAffine(escalado, capa, matrizZona, zona.Size, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));

        using var trozoImagen = new Mat(imagen, zona);

        // [IA] 6a) Luz: si la cara está a oscuras, el filtro también.
        if (opciones.AjustarLuz)
        {
            AjustarALaLuz(trozoImagen, capa);
        }

        // [IA] 6b) Sombra: el mismo PNG, un poco más abajo, borroso y oscuro.
        if (opciones.Sombra)
        {
            PintarSombra(trozoImagen, escalado, matrizZona, desplSombra, radioSombra, opciones.Opacidad);
        }

        // 6) Mezclamos con la imagen respetando la transparencia.
        MezclarConAlfa(trozoImagen, capa, opciones.Opacidad);
    }

    // [IA] Tamaño de la sombra, como fracción del ancho del filtro. Así la
    // sombra crece y encoge con la cara, igual que el propio filtro.
    private const double ProporcionDesplazamientoSombra = 0.018;
    private const double ProporcionDifuminadoSombra = 0.025;

    /// <summary>Cuánto oscurece la sombra como mucho (0 = nada, 1 = negro).</summary>
    private const double IntensidadSombra = 0.38;

    /// <summary>
    /// Brillo de referencia (0..255) de una cara "bien iluminada". Con este
    /// brillo o más el filtro se queda con sus colores originales.
    /// </summary>
    private const double BrilloReferencia = 135;

    /// <summary>
    /// [Ampliación con uso de IA] Sombra suave debajo del filtro.
    /// </summary>
    /// <remarks>
    /// Es el truco de siempre en diseño: una sombra separa el objeto del fondo
    /// y hace que parezca que está "encima" de la cara y no pintado sobre ella.
    /// <list type="number">
    ///   <item><c>WarpAffine</c> otra vez con la misma matriz, pero bajando el PNG
    ///   unos píxeles (la luz suele venir de arriba).</item>
    ///   <item>Nos quedamos solo con su alfa (la "silueta").</item>
    ///   <item><c>GaussianBlur</c> para que el borde de la sombra sea suave.</item>
    ///   <item>Oscurecemos la imagen ahí: <c>fondo · (1 − intensidad · silueta)</c>.</item>
    /// </list>
    /// </remarks>
    private static void PintarSombra(Mat fondo, Mat escalado, Mat matrizZona, int desplazamiento, int radio, double opacidad)
    {
        using var matrizSombra = matrizZona.Clone();
        matrizSombra.Set(1, 2, matrizZona.At<double>(1, 2) + desplazamiento);

        using var capaSombra = new Mat();
        Cv2.WarpAffine(escalado, capaSombra, matrizSombra, fondo.Size(), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));

        using var silueta = new Mat();
        Cv2.ExtractChannel(capaSombra, silueta, 3);
        silueta.ConvertTo(silueta, MatType.CV_32FC1, IntensidadSombra * opacidad / 255.0);

        int lado = radio * 2 + 1; // GaussianBlur pide un tamaño impar
        Cv2.GaussianBlur(silueta, silueta, new Size(lado, lado), 0);

        // factor = 1 − sombra, repetido en los 3 canales para multiplicar.
        using var factor = new Mat();
        Cv2.Subtract(Scalar.All(1.0), silueta, factor);
        using var factor3 = new Mat();
        Cv2.Merge([factor, factor, factor], factor3);

        using var fondoF = new Mat();
        fondo.ConvertTo(fondoF, MatType.CV_32FC3);
        Cv2.Multiply(fondoF, factor3, fondoF);
        fondoF.ConvertTo(fondo, MatType.CV_8UC3);
    }

    /// <summary>
    /// [Ampliación con uso de IA] Oscurece el color del filtro si la zona de la
    /// cara donde va está poco iluminada.
    /// </summary>
    /// <remarks>
    /// Sin esto, con poca luz la cara se ve oscura y el filtro brillante, y
    /// se nota mucho que está "pegado". Medimos el brillo medio
    /// (<c>CvtColor</c> a gris + <c>Mean</c>) justo debajo del filtro y
    /// multiplicamos sus colores por un factor entre 0,55 y 1. Usamos la raíz
    /// cuadrada para que el cambio sea suave y el filtro nunca se vuelva negro.
    /// El canal alfa no se toca.
    /// </remarks>
    private static void AjustarALaLuz(Mat fondo, Mat capa)
    {
        using var gris = new Mat();
        Cv2.CvtColor(fondo, gris, ColorConversionCodes.BGR2GRAY);

        // Solo cuenta lo que está debajo del filtro (alfa > 0).
        using var mascara = new Mat();
        Cv2.ExtractChannel(capa, mascara, 3);
        Cv2.Threshold(mascara, mascara, 0, 255, ThresholdTypes.Binary);
        if (Cv2.CountNonZero(mascara) == 0) return;

        double brillo = Cv2.Mean(gris, mascara).Val0;
        double factor = FactorLuz(brillo);
        if (factor >= 0.999) return;

        // Multiplicamos B, G y R; el alfa se queda igual (factor 1).
        Cv2.Multiply(capa, new Scalar(factor, factor, factor, 1.0), capa);
    }

    /// <summary>
    /// [IA] Factor por el que multiplicar el color del filtro según el brillo
    /// medio (0..255) de la cara. Público para poder probarlo en los tests.
    /// </summary>
    public static double FactorLuz(double brillo)
        => Math.Sqrt(Math.Clamp(brillo / BrilloReferencia, 0.30, 1.0));

    /// <summary>
    /// Mezcla una capa BGRA encima de un trozo de imagen BGR usando su canal alfa.
    /// </summary>
    /// <param name="fondo">Trozo de la imagen de la cámara (BGR, 3 canales). Se modifica.</param>
    /// <param name="capa">El filtro ya girado (BGRA, 4 canales) del mismo tamaño.</param>
    public static void MezclarConAlfa(Mat fondo, Mat capa) => MezclarConAlfa(fondo, capa, 1.0);

    /// <summary>
    /// Igual que el anterior, pero multiplicando el alfa por <paramref name="opacidad"/>
    /// (0..1). [IA] Lo usa el estabilizador para el aparecer/desaparecer suave.
    /// </summary>
    public static void MezclarConAlfa(Mat fondo, Mat capa, double opacidad)
    {
        // Separamos los 4 canales del PNG: azul, verde, rojo y alfa.
        Mat[] canales = Cv2.Split(capa);
        try
        {
            using var colorFiltro = new Mat();
            Cv2.Merge([canales[0], canales[1], canales[2]], colorFiltro);

            // Alfa de 0..255 a 0..1 y repetido en 3 canales para multiplicar fácil.
            using var alfa = new Mat();
            canales[3].ConvertTo(alfa, MatType.CV_32FC1, Math.Clamp(opacidad, 0, 1) / 255.0);
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
