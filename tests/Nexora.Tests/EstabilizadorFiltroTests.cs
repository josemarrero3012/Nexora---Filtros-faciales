using Nexora.Core.Vision;
using OpenCvSharp;

namespace Nexora.Tests;

/// <summary>
/// [Ampliación con uso de IA] Pruebas del estabilizador del filtro (aparecer y
/// desaparecer suave) y del ajuste a la luz.
/// </summary>
public class EstabilizadorFiltroTests
{
    private static readonly Point2f Izq = new(100, 100);
    private static readonly Point2f Der = new(200, 100);

    [Fact]
    public void AlAparecerLaCara_ElFiltroSubePocoAPoco()
    {
        var e = new EstabilizadorFiltro { FotogramasAparecer = 4 };
        Assert.Equal(0.25, e.Actualizar(true, Izq, Der).Opacidad, 3);
        Assert.Equal(0.50, e.Actualizar(true, Izq, Der).Opacidad, 3);
        e.Actualizar(true, Izq, Der);
        Assert.Equal(1.0, e.Actualizar(true, Izq, Der).Opacidad, 3);
        Assert.Equal(1.0, e.Actualizar(true, Izq, Der).Opacidad, 3); // no pasa de 1
    }

    [Fact]
    public void SiHaarFallaUnaImagen_ElFiltroSigueEnSuSitio()
    {
        var e = new EstabilizadorFiltro { FotogramasAparecer = 1, FotogramasDesaparecer = 10 };
        e.Actualizar(true, Izq, Der);

        EstadoFiltro sinCara = e.Actualizar(false, default, default);

        Assert.True(sinCara.Dibujar);
        Assert.Equal(Izq, sinCara.OjoIzquierdo);
        Assert.Equal(Der, sinCara.OjoDerecho);
        Assert.Equal(0.9, sinCara.Opacidad, 3);
    }

    [Fact]
    public void SiLaCaraNoVuelve_ElFiltroDesaparece()
    {
        var e = new EstabilizadorFiltro { FotogramasAparecer = 1, FotogramasDesaparecer = 3 };
        e.Actualizar(true, Izq, Der);
        e.Actualizar(false, default, default);
        e.Actualizar(false, default, default);
        Assert.False(e.Actualizar(false, default, default).Dibujar);
    }

    [Fact]
    public void SinCaraNunca_NoSePintaNada()
    {
        var e = new EstabilizadorFiltro();
        Assert.False(e.Actualizar(false, default, default).Dibujar);
    }

    [Theory]
    [InlineData(200, 1.0)]   // mucha luz: colores originales
    [InlineData(135, 1.0)]   // justo la referencia
    [InlineData(0, 0.5477)]  // a oscuras: nunca baja de raíz(0,30)
    public void FactorLuz_OscureceSoloConPocaLuz(double brillo, double esperado)
    {
        Assert.Equal(esperado, AplicadorFiltros.FactorLuz(brillo), 3);
    }

    [Fact]
    public void FactorLuz_CuantaMenosLuzMasOscuro()
    {
        Assert.True(AplicadorFiltros.FactorLuz(60) < AplicadorFiltros.FactorLuz(100));
    }
}
