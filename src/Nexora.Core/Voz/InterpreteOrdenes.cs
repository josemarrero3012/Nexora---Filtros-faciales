using System.Globalization;
using System.Text;
using Nexora.Core.Modelos;

namespace Nexora.Core.Voz;

/// <summary>
/// La lista cerrada de órdenes de voz y la traducción de "texto reconocido" a
/// <see cref="ComandoVoz"/>.
/// </summary>
/// <remarks>
/// <para>
/// Al reconocedor de System.Speech le damos SOLO estas frases (con
/// <c>Choices</c> y <c>GrammarBuilder</c>). Así no intenta entender cualquier
/// cosa y se equivoca mucho menos, que es lo que hicimos en las prácticas.
/// </para>
/// <para>
/// Esta clase no depende de System.Speech (que solo va en Windows), así que se
/// puede probar con tests en cualquier ordenador.
/// </para>
/// </remarks>
public static class InterpreteOrdenes
{
    /// <summary>
    /// Órdenes normales. Son las de la propuesta más «clic», que es la del plan B.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ComandoVoz> OrdenesPrincipales = new Dictionary<string, ComandoVoz>
    {
        ["activar control gestual"] = ComandoVoz.ActivarControlGestual,
        ["siguiente filtro"] = ComandoVoz.SiguienteFiltro,
        ["quitar filtro"] = ComandoVoz.QuitarFiltro,
        ["pausar control"] = ComandoVoz.PausarControl,
        ["salir"] = ComandoVoz.Salir,
        ["clic"] = ComandoVoz.Clic
    };

    /// <summary>
    /// Respuestas de la pregunta antes de salir. Solo se escuchan en ese momento
    /// y una sola vez, como en el ejercicio de «¿guardar los cambios?».
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ComandoVoz> OrdenesConfirmacion = new Dictionary<string, ComandoVoz>
    {
        ["sí"] = ComandoVoz.Si,
        ["no"] = ComandoVoz.No
    };

    /// <summary>
    /// Convierte el texto que ha reconocido System.Speech en una orden.
    /// </summary>
    /// <remarks>
    /// Comparamos sin mayúsculas ni tildes por si el reconocedor devuelve
    /// «Si» en vez de «sí» o cosas así.
    /// </remarks>
    public static ComandoVoz Interpretar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return ComandoVoz.Desconocido;

        string normalizado = Normalizar(texto);
        foreach (var (frase, comando) in OrdenesPrincipales.Concat(OrdenesConfirmacion))
        {
            if (Normalizar(frase) == normalizado) return comando;
        }
        return ComandoVoz.Desconocido;
    }

    /// <summary>
    /// ¿Le hacemos caso a la orden? Solo si el reconocedor está seguro al menos
    /// el mínimo que marcamos (60 % por defecto).
    /// </summary>
    /// <param name="confianza">Confianza que da System.Speech (de 0 a 1).</param>
    /// <param name="minimo">Confianza mínima (de 0 a 1).</param>
    public static bool EsFiable(double confianza, double minimo) => confianza >= minimo;

    /// <summary>Lo que contesta el programa en voz alta después de cada orden.</summary>
    public static string Respuesta(ComandoVoz comando) => comando switch
    {
        ComandoVoz.ActivarControlGestual => "Control gestual activado",
        ComandoVoz.PausarControl => "Control en pausa",
        ComandoVoz.QuitarFiltro => "Filtro quitado",
        ComandoVoz.Clic => "Clic",
        ComandoVoz.Salir => "¿Seguro que quieres salir? Di sí o no",
        ComandoVoz.Si => "Hasta luego",
        ComandoVoz.No => "Perfecto, seguimos",
        _ => string.Empty
    };

    /// <summary>Minúsculas, sin espacios de sobra y sin tildes.</summary>
    private static string Normalizar(string texto)
    {
        string descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (char c in descompuesto)
        {
            // Las tildes, al descomponer, quedan como un carácter aparte "de marca"; las saltamos.
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
