namespace Nexora.Core.Modelos;

/// <summary>
/// Los cuatro gestos de la propuesta (más "Ninguno" para cuando no se
/// reconoce nada). Pocos gestos, pero que funcionen bien.
/// </summary>
public enum Gesto
{
    /// <summary>No hay mano o la postura no coincide con ningún gesto.</summary>
    Ninguno,

    /// <summary>Solo el índice levantado: la punta del dedo hace de cursor.</summary>
    Mover,

    /// <summary>Pinza entre el pulgar y el índice: hace clic.</summary>
    Clic,

    /// <summary>Índice y corazón levantados: subir o bajar la mano mueve la página.</summary>
    Desplazar,

    /// <summary>Puño cerrado: el cursor se queda quieto y podemos recolocar la mano.</summary>
    Pausa
}
