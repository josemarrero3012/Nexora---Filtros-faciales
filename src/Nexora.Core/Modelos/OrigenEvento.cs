namespace Nexora.Core.Modelos;

/// <summary>
/// De dónde viene cada cosa que apuntamos en el historial.
/// Es la columna "Origen" de la tabla de la derecha de la ventana.
/// </summary>
public enum OrigenEvento
{
    /// <summary>Lo ha hecho la mano (un gesto, un clic, desplazar...).</summary>
    Mano,

    /// <summary>Lo ha hecho la cara (cara encontrada, filtro cambiado, plan B con la cabeza).</summary>
    Cara,

    /// <summary>Lo ha dicho el usuario por el micrófono.</summary>
    Voz,

    /// <summary>Cosas del propio programa: arrancar la cámara, errores, avisos...</summary>
    Sistema
}
