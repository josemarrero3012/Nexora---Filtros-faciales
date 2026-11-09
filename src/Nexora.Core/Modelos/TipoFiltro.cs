namespace Nexora.Core.Modelos;

/// <summary>
/// Los filtros faciales disponibles. La propuesta pedía al menos tres:
/// gafas, orejas y máscara.
/// </summary>
public enum TipoFiltro
{
    /// <summary>Sin filtro (lo que pone «quitar filtro»).</summary>
    Ninguno,

    /// <summary>Gafas de sol redondas.</summary>
    Gafas,

    /// <summary>Orejas, nariz y bigotes de gato.</summary>
    Orejas,

    /// <summary>Antifaz de carnaval (muy de Las Palmas, por cierto).</summary>
    Mascara
}
