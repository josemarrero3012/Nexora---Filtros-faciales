namespace Nexora.Core.Modelos;

/// <summary>
/// Una fila del historial de eventos: la hora, quién lo ha provocado y qué ha
/// pasado. Es igual que el historial que hicimos en el mando multimodal de clase.
/// </summary>
/// <param name="Hora">Momento exacto en el que ocurrió.</param>
/// <param name="Origen">Mano, cara, voz o sistema.</param>
/// <param name="Accion">Texto que se enseña en la tabla (por ejemplo «Clic»).</param>
public sealed record EventoNexora(DateTime Hora, OrigenEvento Origen, string Accion)
{
    /// <summary>La hora ya formateada para la tabla (hh:mm:ss).</summary>
    public string HoraTexto => Hora.ToString("HH:mm:ss");

    /// <summary>Atajo para crear un evento con la hora de ahora mismo.</summary>
    public static EventoNexora Ahora(OrigenEvento origen, string accion) => new(DateTime.Now, origen, accion);

    /// <summary>
    /// Línea en formato CSV (separado por ';' para que Excel en español lo abra
    /// bien sin tocar nada). Lo usamos al exportar el historial para la memoria.
    /// </summary>
    public string ACsv() => $"{Hora:yyyy-MM-dd HH:mm:ss.fff};{Origen};{Accion.Replace(';', ',')}";
}
