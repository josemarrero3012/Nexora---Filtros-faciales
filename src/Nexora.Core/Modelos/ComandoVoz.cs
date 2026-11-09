namespace Nexora.Core.Modelos;

/// <summary>
/// Las órdenes de voz que entiende Nexora. Cada valor se corresponde con una
/// frase de la lista cerrada que le pasamos al reconocedor (ver
/// <see cref="Nexora.Core.Voz.InterpreteOrdenes"/>).
/// </summary>
public enum ComandoVoz
{
    /// <summary>Lo que ha oído no está en la lista (no debería pasar con la gramática cerrada).</summary>
    Desconocido,

    /// <summary>«activar control gestual»</summary>
    ActivarControlGestual,

    /// <summary>«siguiente filtro»</summary>
    SiguienteFiltro,

    /// <summary>«quitar filtro»</summary>
    QuitarFiltro,

    /// <summary>«pausar control»</summary>
    PausarControl,

    /// <summary>«salir» (antes de cerrar pregunta si estamos seguros)</summary>
    Salir,

    /// <summary>«clic» (del plan B: el clic se hace con la voz)</summary>
    Clic,

    /// <summary>«sí» (solo se escucha cuando el programa pregunta antes de salir)</summary>
    Si,

    /// <summary>«no» (solo se escucha cuando el programa pregunta antes de salir)</summary>
    No
}
