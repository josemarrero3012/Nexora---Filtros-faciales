using System.Diagnostics;
using Nexora.Core.Utilidades;

namespace Nexora.Tests;

/// <summary>Pruebas del medidor de FPS y milisegundos por imagen.</summary>
public class MedidorRendimientoTests
{
    [Fact]
    public void TreintaImagenesEnUnSegundo_SonUnos30Fps()
    {
        var medidor = new MedidorRendimiento(ventana: 31);
        long inicio = 0;
        long tickPorImagen = Stopwatch.Frequency / 30;
        for (int i = 0; i <= 30; i++)
        {
            medidor.Registrar(TimeSpan.FromMilliseconds(10), inicio + i * tickPorImagen);
        }
        Assert.InRange(medidor.Fps, 29.5, 30.5);
        Assert.Equal(10, medidor.MsPorFotograma, 3);
    }

    [Fact]
    public void SinImagenes_DaCero()
    {
        var medidor = new MedidorRendimiento();
        Assert.Equal(0, medidor.Fps);
        Assert.Equal(0, medidor.MsPorFotograma);
    }
}
