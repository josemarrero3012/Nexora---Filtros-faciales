using Nexora.Core.Configuracion;
using Nexora.Core.Cursor;
using OpenCvSharp;

namespace Nexora.Tests;

/// <summary>
/// Pruebas del cursor: reparto de la zona por la pantalla, zona muerta,
/// suavizado, plan B con la cabeza y gesto de desplazar.
/// </summary>
public class ControlCursorTests
{
    private static readonly Size Pantalla = new(1920, 1080);
    private static readonly Rect Zona = new(100, 100, 200, 200);

    [Fact]
    public void EsquinasDeLaZona_LleganALasEsquinasDeLaPantalla()
    {
        // Sin suavizado para ver el valor directo
        var control = new ControlCursor(new AjustesNexora { SuavizadoCursor = 1.0, ZonaMuertaPx = 0 });

        Assert.Equal(new Point(0, 0), control.PosicionConMano(new Point2f(100, 100), Zona, Pantalla));
        Assert.Equal(new Point(1919, 1079), control.PosicionConMano(new Point2f(300, 300), Zona, Pantalla));
    }

    [Fact]
    public void DedoFueraDeLaZona_SeQuedaEnElBorde()
    {
        var control = new ControlCursor(new AjustesNexora { SuavizadoCursor = 1.0, ZonaMuertaPx = 0 });
        Assert.Equal(new Point(1919, 0), control.PosicionConMano(new Point2f(9999, -50), Zona, Pantalla));
    }

    [Fact]
    public void MovimientoPequeno_CaeEnLaZonaMuertaYNoMueveElCursor()
    {
        var control = new ControlCursor(new AjustesNexora { SuavizadoCursor = 1.0, ZonaMuertaPx = 10 });

        Assert.NotNull(control.PosicionConMano(new Point2f(200, 200), Zona, Pantalla));
        // Mover el dedo 0,5 px en la zona son unos 5 px en pantalla: menos de 10, no se mueve
        Assert.Null(control.PosicionConMano(new Point2f(200.5f, 200), Zona, Pantalla));
        // Mover el dedo 5 px son unos 48 px: ahora sí
        Assert.NotNull(control.PosicionConMano(new Point2f(205, 200), Zona, Pantalla));
    }

    [Fact]
    public void Suavizado_SoloRecorreUnTrozoDelCamino()
    {
        var control = new ControlCursor(new AjustesNexora { SuavizadoCursor = 0.5, ZonaMuertaPx = 0 });

        control.PosicionConMano(new Point2f(100, 100), Zona, Pantalla); // empieza en (0, 0)
        Point? p = control.PosicionConMano(new Point2f(300, 100), Zona, Pantalla); // objetivo x = 1919

        // Con 0,5 solo avanza la mitad del camino
        Assert.NotNull(p);
        Assert.InRange(p.Value.X, 955, 965);
    }

    [Fact]
    public void Cabeza_DentroDeLaZonaMuerta_NoSeMueve()
    {
        var control = new ControlCursor(new AjustesNexora { ZonaMuertaCabezaPx = 40 });
        var imagen = new Size(640, 480);
        // Centro de la imagen (320, 240); la cara está a 30 px: dentro de la zona muerta
        Assert.Null(control.PosicionConCabeza(new Point2f(350, 260), imagen, new Point(500, 500), Pantalla));
    }

    [Fact]
    public void Cabeza_FueraDeLaZonaMuerta_SeMueveHaciaEseLado()
    {
        var control = new ControlCursor(new AjustesNexora { ZonaMuertaCabezaPx = 40, GananciaCabeza = 0.5 });
        var imagen = new Size(640, 480);
        // 100 px a la derecha del centro → 60 px fuera de la zona → 30 px de velocidad
        Point? p = control.PosicionConCabeza(new Point2f(420, 240), imagen, new Point(500, 500), Pantalla);
        Assert.Equal(new Point(530, 500), p);
    }

    [Fact]
    public void Desplazar_SubirLaManoDaRuedaPositiva()
    {
        var control = new ControlCursor(new AjustesNexora { PasoDesplazamientoPx = 10 });

        Assert.Equal(0, control.Desplazamiento(200)); // primera imagen: solo apunta
        Assert.Equal(0, control.Desplazamiento(195)); // 5 px: todavía no llega a un golpe
        Assert.Equal(ControlCursor.UnidadRueda, control.Desplazamiento(188)); // 12 px acumulados → 1 golpe
        Assert.Equal(-2 * ControlCursor.UnidadRueda, control.Desplazamiento(210)); // baja 22 px → 2 golpes hacia abajo
    }
}
