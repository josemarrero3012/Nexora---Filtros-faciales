using Nexora.Core.Modelos;
using Nexora.Core.Vision;
using OpenCvSharp;

namespace Nexora.Tests;

/// <summary>
/// Pruebas de las cuentas para colocar los filtros ("los ojos hacen de regla
/// y de transportador").
/// </summary>
public class GeometriaFiltroTests
{
    [Fact]
    public void OjosAlDobleDeDistancia_FiltroAlDobleDeTamano()
    {
        var c = GeometriaFiltro.Calcular(new Point2f(0, 0), new Point2f(100, 0), new Point2f(50, 50), new Point2f(250, 50));
        Assert.Equal(2.0, c.Escala, 3);
        Assert.Equal(0.0, c.AnguloGrados, 3);
        Assert.Equal(new Point2f(150, 50), c.CentroDestino);
    }

    [Fact]
    public void CabezaInclinada_DaElAnguloDeLosOjos()
    {
        // El ojo derecho 100 px más bajo y 100 px a la derecha → 45 grados
        var c = GeometriaFiltro.Calcular(new Point2f(0, 0), new Point2f(100, 0), new Point2f(0, 0), new Point2f(100, 100));
        Assert.Equal(45.0, c.AnguloGrados, 3);
    }

    [Theory]
    [InlineData(TipoFiltro.Gafas)]
    [InlineData(TipoFiltro.Orejas)]
    [InlineData(TipoFiltro.Mascara)]
    public void OjosDelPng_CaenEncimaDeLosOjosDeLaCara(TipoFiltro tipo)
    {
        // Hacemos lo mismo que AplicadorFiltros (escala + giro + traslado) pero
        // solo con números, y comprobamos que los ojos del PNG acaban encima de
        // los ojos detectados.
        DefinicionFiltro def = CatalogoFiltros.Buscar(tipo)!;
        var ojoIzqCara = new Point2f(250, 200);
        var ojoDerCara = new Point2f(330, 230); // cabeza inclinada

        ColocacionFiltro c = GeometriaFiltro.Calcular(def.OjoIzquierdo, def.OjoDerecho, ojoIzqCara, ojoDerCara);

        var pivote = new Point2f(
            (float)((def.OjoIzquierdo.X + def.OjoDerecho.X) / 2 * c.Escala),
            (float)((def.OjoIzquierdo.Y + def.OjoDerecho.Y) / 2 * c.Escala));
        double[] m = GeometriaFiltro.MatrizRotacion(pivote, -c.AnguloGrados, 1.0);
        m[2] += c.CentroDestino.X - pivote.X;
        m[5] += c.CentroDestino.Y - pivote.Y;

        Point2f izq = GeometriaFiltro.Transformar(m, new Point2f((float)(def.OjoIzquierdo.X * c.Escala), (float)(def.OjoIzquierdo.Y * c.Escala)));
        Point2f der = GeometriaFiltro.Transformar(m, new Point2f((float)(def.OjoDerecho.X * c.Escala), (float)(def.OjoDerecho.Y * c.Escala)));

        Assert.Equal(ojoIzqCara.X, izq.X, 0.01f);
        Assert.Equal(ojoIzqCara.Y, izq.Y, 0.01f);
        Assert.Equal(ojoDerCara.X, der.X, 0.01f);
        Assert.Equal(ojoDerCara.Y, der.Y, 0.01f);
    }

    [Fact]
    public void SinOjos_SeColocanConLasProporcionesDeLaCara()
    {
        var (izq, der) = GeometriaFiltro.OjosPorProporcion(new Rect(100, 100, 200, 200));
        Assert.Equal(new Point2f(160, 180), izq);
        Assert.Equal(new Point2f(240, 180), der);
    }

    [Fact]
    public void SiguienteFiltro_DaLaVuelta()
    {
        Assert.Equal(TipoFiltro.Gafas, CatalogoFiltros.Siguiente(TipoFiltro.Ninguno));
        Assert.Equal(TipoFiltro.Orejas, CatalogoFiltros.Siguiente(TipoFiltro.Gafas));
        Assert.Equal(TipoFiltro.Mascara, CatalogoFiltros.Siguiente(TipoFiltro.Orejas));
        Assert.Equal(TipoFiltro.Gafas, CatalogoFiltros.Siguiente(TipoFiltro.Mascara));
    }

    [Fact]
    public void ZonaControl_EsCuadradaYCabeEnLaImagen()
    {
        var imagen = new Size(640, 480);
        Rect zona = ZonaControl.Calcular(imagen, 0.6);
        Assert.Equal(zona.Width, zona.Height);
        Assert.True(zona.X >= 0 && zona.Y >= 0 && zona.Right <= 640 && zona.Bottom <= 480);
    }
}
