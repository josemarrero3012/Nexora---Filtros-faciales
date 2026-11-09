using Nexora.Core.Modelos;
using Nexora.Core.Voz;

namespace Nexora.Tests;

/// <summary>
/// Pruebas de la lista cerrada de órdenes de voz y del filtro del 60 %.
/// </summary>
public class InterpreteOrdenesTests
{
    [Theory]
    [InlineData("activar control gestual", ComandoVoz.ActivarControlGestual)]
    [InlineData("siguiente filtro", ComandoVoz.SiguienteFiltro)]
    [InlineData("quitar filtro", ComandoVoz.QuitarFiltro)]
    [InlineData("pausar control", ComandoVoz.PausarControl)]
    [InlineData("salir", ComandoVoz.Salir)]
    [InlineData("clic", ComandoVoz.Clic)]
    [InlineData("sí", ComandoVoz.Si)]
    [InlineData("no", ComandoVoz.No)]
    public void CadaFrase_DaSuOrden(string frase, ComandoVoz esperado)
        => Assert.Equal(esperado, InterpreteOrdenes.Interpretar(frase));

    [Theory]
    [InlineData("Si")]
    [InlineData("SÍ")]
    [InlineData("  sí  ")]
    public void DaIgualMayusculasTildesYEspacios(string frase)
        => Assert.Equal(ComandoVoz.Si, InterpreteOrdenes.Interpretar(frase));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ponme una canción")]
    public void FrasesQueNoEstanEnLaLista_SonDesconocidas(string? frase)
        => Assert.Equal(ComandoVoz.Desconocido, InterpreteOrdenes.Interpretar(frase));

    [Theory]
    [InlineData(0.59, false)]
    [InlineData(0.60, true)]
    [InlineData(0.95, true)]
    public void SoloHacemosCasoDel60PorCientoParaArriba(double confianza, bool esperado)
        => Assert.Equal(esperado, InterpreteOrdenes.EsFiable(confianza, 0.6));

    [Fact]
    public void TodasLasOrdenesTienenRespuestaHablada()
    {
        foreach (ComandoVoz comando in InterpreteOrdenes.OrdenesPrincipales.Values.Concat(InterpreteOrdenes.OrdenesConfirmacion.Values))
        {
            // «siguiente filtro» la contesta la ventana diciendo el nombre del filtro
            if (comando == ComandoVoz.SiguienteFiltro) continue;
            Assert.False(string.IsNullOrEmpty(InterpreteOrdenes.Respuesta(comando)), $"Falta respuesta para {comando}");
        }
    }
}
