using Nexora.Core.Configuracion;
using Nexora.Core.Utilidades;
using OpenCvSharp;

namespace Nexora.Core.Cursor;

/// <summary>
/// Calcula a dónde tiene que ir el cursor. Aquí no se mueve el ratón de verdad
/// (eso lo hace ServicioRaton en la aplicación con SetCursorPos y SendInput);
/// aquí solo hacemos las cuentas, así se pueden probar con tests.
/// </summary>
/// <remarks>
/// <para>Tiene tres "modos":</para>
/// <list type="bullet">
///   <item><b>Con la mano</b> (posición absoluta): la punta del índice dentro de
///   la zona de control se reparte por toda la pantalla. Usamos suavizado y la
///   zona muerta de clase para que el cursor no tiemble.</item>
///   <item><b>Con la cabeza</b> (plan B, como el mando de clase): si la cara está
///   dentro de la zona muerta del centro, el cursor no se mueve; si se sale, el
///   cursor se mueve hacia ese lado, más rápido cuanto más lejos.</item>
///   <item><b>Desplazar</b>: al subir o bajar la mano se generan "golpes" de la
///   rueda del ratón.</item>
/// </list>
/// </remarks>
public sealed class ControlCursor
{
    /// <summary>Lo que vale un golpe de rueda en Windows (WHEEL_DELTA).</summary>
    public const int UnidadRueda = 120;

    private readonly AjustesNexora _ajustes;
    private readonly SuavizadorPunto _suavizador = new();

    private Point? _ultimaEnviada;      // última posición a la que movimos el cursor
    private float? _yAnteriorScroll;    // altura de la mano en la imagen anterior (para desplazar)
    private double _acumuladoScroll;    // píxeles que llevamos movidos sin llegar a un "golpe" de rueda

    public ControlCursor(AjustesNexora ajustes)
    {
        _ajustes = ajustes;
    }

    /// <summary>
    /// Modo mano: pasa la punta del índice (en la imagen) a una posición de pantalla.
    /// </summary>
    /// <param name="punta">Punta del dedo índice en coordenadas de la imagen.</param>
    /// <param name="zonaUtil">Parte interior de la zona de control.</param>
    /// <param name="pantalla">Tamaño de la pantalla en píxeles.</param>
    /// <returns>
    /// La nueva posición del cursor, o null si el movimiento es tan pequeño que
    /// cae dentro de la zona muerta (y por tanto no hay que moverlo).
    /// </returns>
    public Point? PosicionConMano(Point2f punta, Rect zonaUtil, Size pantalla)
    {
        if (zonaUtil.Width <= 0 || zonaUtil.Height <= 0) return null;

        // 1) Posición relativa dentro de la zona (0 = borde izquierdo/arriba,
        //    1 = borde derecho/abajo). Si el dedo se sale, se queda en el borde.
        double nx = Geometria.Limitar((punta.X - zonaUtil.X) / zonaUtil.Width, 0, 1);
        double ny = Geometria.Limitar((punta.Y - zonaUtil.Y) / zonaUtil.Height, 0, 1);

        // 2) La pasamos a píxeles de pantalla.
        var objetivo = new Point2f((float)(nx * (pantalla.Width - 1)), (float)(ny * (pantalla.Height - 1)));

        // 3) Suavizado: solo recorremos un trozo del camino en cada imagen.
        Point2f suave = _suavizador.Actualizar(objetivo, _ajustes.SuavizadoCursor);
        var nueva = new Point((int)Math.Round(suave.X), (int)Math.Round(suave.Y));

        // 4) Zona muerta: si el cursor se movería menos de X píxeles, lo dejamos quieto.
        if (_ultimaEnviada is { } anterior)
        {
            double movimiento = Geometria.Distancia(new Point2f(anterior.X, anterior.Y), new Point2f(nueva.X, nueva.Y));
            if (movimiento < _ajustes.ZonaMuertaPx) return null;
        }

        _ultimaEnviada = nueva;
        return nueva;
    }

    /// <summary>
    /// Modo cabeza (plan B): mueve el cursor como un joystick según lo lejos
    /// que esté la cara del centro de la imagen.
    /// </summary>
    /// <param name="centroCara">Centro de la cara en la imagen.</param>
    /// <param name="tamanoImagen">Tamaño de la imagen de la cámara.</param>
    /// <param name="cursorActual">Dónde está el cursor ahora mismo.</param>
    /// <param name="pantalla">Tamaño de la pantalla.</param>
    /// <returns>La nueva posición, o null si la cara está dentro de la zona muerta.</returns>
    public Point? PosicionConCabeza(Point2f centroCara, Size tamanoImagen, Point cursorActual, Size pantalla)
    {
        // Diferencia entre el centro de la cara y el centro de la imagen.
        double dx = centroCara.X - tamanoImagen.Width / 2.0;
        double dy = centroCara.Y - tamanoImagen.Height / 2.0;

        // Zona muerta: por cada eje, si no la pasamos, ese eje no se mueve.
        double velX = VelocidadFueraDeZonaMuerta(dx);
        double velY = VelocidadFueraDeZonaMuerta(dy);
        if (velX == 0 && velY == 0) return null;

        int x = (int)Geometria.Limitar(cursorActual.X + velX, 0, pantalla.Width - 1);
        int y = (int)Geometria.Limitar(cursorActual.Y + velY, 0, pantalla.Height - 1);
        return new Point(x, y);
    }

    /// <summary>
    /// Gesto de desplazar: cuánto hay que girar la rueda según lo que ha subido
    /// o bajado la mano desde la imagen anterior.
    /// </summary>
    /// <param name="yMano">Altura de la mano en la imagen (por ejemplo, entre el índice y el corazón).</param>
    /// <returns>
    /// Cantidad para la rueda (múltiplo de 120). Positivo = hacia arriba, como
    /// en Windows. 0 si todavía no se ha movido lo suficiente.
    /// </returns>
    public int Desplazamiento(float yMano)
    {
        if (_yAnteriorScroll is null)
        {
            // Primera imagen del gesto: solo apuntamos dónde está la mano.
            _yAnteriorScroll = yMano;
            return 0;
        }

        // En la imagen la Y crece hacia abajo. Si la mano sube, la Y baja, y
        // queremos que la página suba (rueda positiva). Por eso restamos así.
        _acumuladoScroll += _yAnteriorScroll.Value - yMano;
        _yAnteriorScroll = yMano;

        int golpes = (int)(_acumuladoScroll / _ajustes.PasoDesplazamientoPx);
        if (golpes == 0) return 0;

        _acumuladoScroll -= golpes * _ajustes.PasoDesplazamientoPx; // guardamos lo que sobra para la próxima
        return golpes * UnidadRueda;
    }

    /// <summary>Para cuando se acaba el gesto de desplazar.</summary>
    public void TerminarDesplazamiento()
    {
        _yAnteriorScroll = null;
        _acumuladoScroll = 0;
    }

    /// <summary>
    /// Se olvida del suavizado y de la última posición. Lo llamamos cuando se
    /// pierde la mano o se hace pausa, para que al volver el cursor no dé un salto raro.
    /// </summary>
    public void Reiniciar()
    {
        _suavizador.Reiniciar();
        _ultimaEnviada = null;
        TerminarDesplazamiento();
    }

    /// <summary>
    /// Lo que se pasa de la zona muerta, multiplicado por la ganancia. Dentro de
    /// la zona muerta devuelve 0.
    /// </summary>
    private double VelocidadFueraDeZonaMuerta(double desplazamiento)
    {
        double zona = _ajustes.ZonaMuertaCabezaPx;
        if (Math.Abs(desplazamiento) <= zona) return 0;
        double fuera = Math.Abs(desplazamiento) - zona;
        return Math.Sign(desplazamiento) * fuera * _ajustes.GananciaCabeza;
    }
}
