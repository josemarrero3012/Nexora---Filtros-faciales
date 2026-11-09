namespace Nexora.Core.Modelos;

/// <summary>
/// Lo que devuelve el clasificador de gestos después de mirar una imagen.
/// </summary>
/// <param name="Gesto">El gesto "confirmado" (el que ya se ha repetido varias imágenes seguidas).</param>
/// <param name="HaCambiado">true solo en la imagen en la que el gesto cambia (para apuntarlo en el historial una vez).</param>
/// <param name="ClicNuevo">true solo en la imagen en la que se cierra la pinza (así no se hacen dos clics).</param>
/// <param name="MsRespuesta">
/// Milisegundos desde que vimos el gesto por primera vez hasta que lo dimos por
/// bueno. Es uno de los "tiempos de respuesta" que pide la evaluación. Vale 0
/// cuando no ha cambiado nada.
/// </param>
public readonly record struct ResultadoGesto(Gesto Gesto, bool HaCambiado, bool ClicNuevo, double MsRespuesta);
