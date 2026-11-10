using Nexora.Core.Configuracion;
using Nexora.Core.Modelos;
using Nexora.Core.Utilidades;
using Nexora.Core.Vision;
using OpenCvSharp;

namespace Nexora.App.Servicios;

/// <summary>
/// Módulo de filtros faciales: busca la cara y los ojos, y pinta el filtro.
/// </summary>
/// <remarks>
/// Esta parte sale casi entera de clase (Haar Cascade). Lo que hace este
/// servicio es juntar el detector (<see cref="DetectorCaras"/>) con el que
/// pega los PNG (<see cref="AplicadorFiltros"/>) y avisar a la ventana cuando
/// aparece o desaparece la cara.
/// Se llama desde el hilo de la cámara, una vez por imagen.
/// </remarks>
public sealed class ServicioCara : IDisposable
{
    /// <summary>
    /// Imágenes seguidas sin cara que esperamos antes de decir "cara perdida".
    /// Haar a veces falla una imagen suelta y no queremos llenar el historial.
    /// </summary>
    private const int ImagenesParaDarPorPerdida = 15;

    private readonly AjustesNexora _ajustes;
    private readonly DetectorCaras _detector;
    private readonly AplicadorFiltros _aplicador;

    // Suavizamos la posición de los ojos para que el filtro no tiemble.
    private readonly SuavizadorPunto _ojoIzquierdo = new();
    private readonly SuavizadorPunto _ojoDerecho = new();

    // [Ampliación con uso de IA] Para que el filtro no parpadee cuando Haar
    // pierde la cara una imagen suelta (ver EstabilizadorFiltro).
    private readonly EstabilizadorFiltro _estabilizador = new();

    private bool _hayCara;
    private int _imagenesSinCara;

    /// <summary>Avisa de cosas para el historial (cara encontrada, cara perdida...).</summary>
    public event EventHandler<EventoNexora>? EventoOcurrido;

    public ServicioCara(AjustesNexora ajustes)
    {
        _ajustes = ajustes;
        _detector = new DetectorCaras(RutasRecursos.CascadaCara, RutasRecursos.CascadaOjos, ajustes);
        _aplicador = new AplicadorFiltros(RutasRecursos.CarpetaFiltros);
    }

    /// <summary>
    /// Procesa una imagen: busca la cara, pone el filtro y dibuja las ayudas.
    /// </summary>
    /// <param name="imagen">Imagen en color (se pinta encima).</param>
    /// <param name="gris">La misma imagen en gris (para el detector).</param>
    /// <returns>La cara encontrada (con los ojos ya suavizados) o null.</returns>
    public ResultadoCara? Procesar(Mat imagen, Mat gris)
    {
        ResultadoCara? cara = _detector.Detectar(gris);

        if (cara is null)
        {
            // Contamos imágenes sin cara y solo avisamos cuando ya son bastantes.
            _imagenesSinCara++;
            if (_hayCara && _imagenesSinCara >= ImagenesParaDarPorPerdida)
            {
                _hayCara = false;
                _ojoIzquierdo.Reiniciar();
                _ojoDerecho.Reiniciar();
                Avisar("Cara perdida");
            }

            // [IA] Sin cara en esta imagen: el filtro se queda donde estaba y se va apagando.
            PintarFiltro(imagen, _estabilizador.Actualizar(false, default, default));
            return null;
        }

        _imagenesSinCara = 0;
        if (!_hayCara)
        {
            _hayCara = true;
            Avisar("Cara encontrada");
        }

        // Ojos suavizados: así las gafas no "bailan" de una imagen a otra.
        Point2f izq = _ojoIzquierdo.Actualizar(cara.OjoIzquierdo, _ajustes.SuavizadoOjos);
        Point2f der = _ojoDerecho.Actualizar(cara.OjoDerecho, _ajustes.SuavizadoOjos);
        cara = cara with { OjoIzquierdo = izq, OjoDerecho = der };

        if (_ajustes.FiltrosActivos)
        {
            // [IA] Antes: _aplicador.Aplicar(imagen, _ajustes.FiltroActual, izq, der);
            // Ahora pasamos por el estabilizador para que aparezca suave.
            PintarFiltro(imagen, _estabilizador.Actualizar(true, izq, der));
        }

        if (_ajustes.MostrarAyudas)
        {
            Dibujante.DibujarCara(imagen, cara);
        }

        return cara;
    }

    /// <summary>
    /// [Ampliación con uso de IA] Pinta el filtro con lo que diga el
    /// estabilizador. Con «Filtro realista» apagado se pinta como siempre
    /// (sin sombra, sin ajuste de luz y sin aparecer suave).
    /// </summary>
    private void PintarFiltro(Mat imagen, EstadoFiltro estado)
    {
        if (!_ajustes.FiltrosActivos)
        {
            _estabilizador.Reiniciar();
            return;
        }

        if (!_ajustes.FiltroRealista)
        {
            // Modo "tal cual": solo cuando hay cara de verdad, como antes.
            if (_hayCara && _imagenesSinCara == 0)
                _aplicador.Aplicar(imagen, _ajustes.FiltroActual, estado.OjoIzquierdo, estado.OjoDerecho);
            return;
        }

        if (!estado.Dibujar) return;
        _aplicador.Aplicar(imagen, _ajustes.FiltroActual, estado.OjoIzquierdo, estado.OjoDerecho,
            new OpcionesFiltro(estado.Opacidad, Sombra: true, AjustarLuz: true));
    }

    private void Avisar(string accion) => EventoOcurrido?.Invoke(this, EventoNexora.Ahora(OrigenEvento.Cara, accion));

    public void Dispose()
    {
        _detector.Dispose();
        _aplicador.Dispose();
    }
}
