using Nexora.Core.Configuracion;
using Nexora.Core.Gestos;
using Nexora.Core.Modelos;

namespace Nexora.Tests;

/// <summary>
/// Pruebas de los cuatro gestos y de los "trucos" para que no bailen
/// (histéresis de la pinza y confirmación de varias imágenes).
/// </summary>
public class ClasificadorGestosTests
{
    private static ClasificadorGestos NuevoClasificador(int fotogramas = 3)
        => new(new AjustesNexora { FotogramasParaConfirmarGesto = fotogramas });

    [Fact]
    public void SoloIndiceEstirado_EsMover()
    {
        var clasificador = NuevoClasificador();
        var mano = ManoDePrueba.Crear(indice: true, corazon: false, anular: false, menique: false);
        Assert.Equal(Gesto.Mover, clasificador.ClasificarSinConfirmar(mano));
    }

    [Fact]
    public void IndiceYCorazon_EsDesplazar()
    {
        var clasificador = NuevoClasificador();
        var mano = ManoDePrueba.Crear(indice: true, corazon: true, anular: false, menique: false);
        Assert.Equal(Gesto.Desplazar, clasificador.ClasificarSinConfirmar(mano));
    }

    [Fact]
    public void Puno_EsPausa()
    {
        var clasificador = NuevoClasificador();
        var mano = ManoDePrueba.Crear(indice: false, corazon: false, anular: false, menique: false);
        Assert.Equal(Gesto.Pausa, clasificador.ClasificarSinConfirmar(mano));
    }

    [Fact]
    public void ManoAbierta_NoEsNingunGesto()
    {
        var clasificador = NuevoClasificador();
        var mano = ManoDePrueba.Crear(indice: true, corazon: true, anular: true, menique: true);
        Assert.Equal(Gesto.Ninguno, clasificador.ClasificarSinConfirmar(mano));
    }

    [Fact]
    public void PulgarTocandoIndice_EsClic()
    {
        var clasificador = NuevoClasificador();
        var mano = ManoDePrueba.Crear(indice: true, corazon: false, anular: false, menique: false, pinza: true);
        Assert.Equal(Gesto.Clic, clasificador.ClasificarSinConfirmar(mano));
    }

    [Fact]
    public void GestoNuevo_NoSeConfirmaHastaVerloVariasVeces()
    {
        var clasificador = NuevoClasificador(fotogramas: 3);
        var mover = ManoDePrueba.Crear(true, false, false, false);

        // Imagen 1 y 2: todavía no
        Assert.Equal(Gesto.Ninguno, clasificador.Actualizar(mover, 0).Gesto);
        Assert.Equal(Gesto.Ninguno, clasificador.Actualizar(mover, 33).Gesto);

        // Imagen 3: ya sí, y nos dice cuánto ha tardado en confirmarlo
        ResultadoGesto r = clasificador.Actualizar(mover, 66);
        Assert.Equal(Gesto.Mover, r.Gesto);
        Assert.True(r.HaCambiado);
        Assert.Equal(66, r.MsRespuesta);

        // Imagen 4: sigue igual pero ya no "ha cambiado"
        Assert.False(clasificador.Actualizar(mover, 99).HaCambiado);
    }

    [Fact]
    public void UnaImagenSueltaMala_NoCambiaElGesto()
    {
        var clasificador = NuevoClasificador(fotogramas: 3);
        var mover = ManoDePrueba.Crear(true, false, false, false);
        var desplazar = ManoDePrueba.Crear(true, true, false, false);

        for (int i = 0; i < 3; i++) clasificador.Actualizar(mover, i);
        Assert.Equal(Gesto.Mover, clasificador.GestoActual);

        // Una sola imagen "rara" no debe cambiar el gesto
        clasificador.Actualizar(desplazar, 10);
        clasificador.Actualizar(mover, 11);
        Assert.Equal(Gesto.Mover, clasificador.GestoActual);
    }

    [Fact]
    public void Pinza_SoloHaceUnClicAunqueSeQuedeCerrada()
    {
        var clasificador = NuevoClasificador(fotogramas: 2);
        var pinza = ManoDePrueba.Crear(true, false, false, false, pinza: true);

        int clics = 0;
        for (int i = 0; i < 10; i++)
        {
            if (clasificador.Actualizar(pinza, i).ClicNuevo) clics++;
        }
        Assert.Equal(1, clics);
    }

    [Fact]
    public void Histeresis_LaPinzaNoSeSueltaJustoEnElLimite()
    {
        // Umbral para cerrar 0,30 y para soltar 0,45. Una distancia de 0,38 está
        // en medio: si estaba cerrada, sigue cerrada; si estaba abierta, sigue abierta.
        var ajustes = new AjustesNexora { UmbralPinzaCerrar = 0.30, UmbralPinzaSoltar = 0.45 };
        var clasificador = new ClasificadorGestos(ajustes);

        var cerrada = ManoDePrueba.Crear(true, false, false, false, pinza: true);
        Assert.Equal(Gesto.Clic, clasificador.ClasificarSinConfirmar(cerrada));

        // Tamaño de la mano en ManoDePrueba: muñeca (100,200) → nudillo corazón (100,140) = 60 px.
        // Ponemos el pulgar a 0,38 · 60 ≈ 23 px de la punta del índice.
        var enMedio = ManoDePrueba.Crear(true, false, false, false);
        enMedio[4] = new OpenCvSharp.Point2f(enMedio[8].X, enMedio[8].Y + 23);

        Assert.Equal(Gesto.Clic, clasificador.ClasificarSinConfirmar(enMedio)); // sigue cerrada

        var abierta = ManoDePrueba.Crear(true, false, false, false);
        Assert.Equal(Gesto.Mover, clasificador.ClasificarSinConfirmar(abierta)); // se suelta

        Assert.Equal(Gesto.Mover, clasificador.ClasificarSinConfirmar(enMedio)); // y no se vuelve a cerrar
    }

    [Fact]
    public void SinMano_VuelveANingunoMasRapido()
    {
        var clasificador = NuevoClasificador(fotogramas: 4);
        var mover = ManoDePrueba.Crear(true, false, false, false);
        for (int i = 0; i < 4; i++) clasificador.Actualizar(mover, i);
        Assert.Equal(Gesto.Mover, clasificador.GestoActual);

        // Con 4 imágenes para confirmar, "Ninguno" solo necesita la mitad (2)
        clasificador.SinMano(10);
        Assert.Equal(Gesto.Ninguno, clasificador.SinMano(11).Gesto);
    }

    [Fact]
    public void MenosDe21Puntos_DaError()
    {
        var clasificador = NuevoClasificador();
        Assert.Throws<ArgumentException>(() => clasificador.Actualizar(new OpenCvSharp.Point2f[5], 0));
    }
}
