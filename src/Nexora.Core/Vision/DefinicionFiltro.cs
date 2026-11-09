using Nexora.Core.Modelos;
using OpenCvSharp;

namespace Nexora.Core.Vision;

/// <summary>
/// Datos fijos de cada filtro: qué PNG usa y dónde están los ojos DENTRO de
/// ese PNG. Esos dos puntos son la referencia para encajarlo en la cara.
/// </summary>
/// <param name="Tipo">Qué filtro es.</param>
/// <param name="NombreVisible">Nombre para enseñar en la ventana y decir por voz.</param>
/// <param name="Archivo">Nombre del PNG dentro de Recursos/Filtros.</param>
/// <param name="OjoIzquierdo">Dónde está el ojo izquierdo en el PNG (en píxeles).</param>
/// <param name="OjoDerecho">Dónde está el ojo derecho en el PNG (en píxeles).</param>
public sealed record DefinicionFiltro(TipoFiltro Tipo, string NombreVisible, string Archivo, Point2f OjoIzquierdo, Point2f OjoDerecho);

/// <summary>
/// Lista de filtros disponibles. Las coordenadas de los ojos tienen que
/// coincidir con las que usamos al dibujar los PNG (herramientas/generar_filtros.py).
/// </summary>
public static class CatalogoFiltros
{
    /// <summary>Todos los filtros con PNG (no incluye "Ninguno").</summary>
    public static readonly IReadOnlyList<DefinicionFiltro> Todos =
    [
        new(TipoFiltro.Gafas, "Gafas", "gafas.png", new Point2f(180, 110), new Point2f(460, 110)),
        new(TipoFiltro.Orejas, "Orejas de gato", "orejas.png", new Point2f(180, 540), new Point2f(460, 540)),
        new(TipoFiltro.Mascara, "Máscara", "mascara.png", new Point2f(180, 130), new Point2f(460, 130))
    ];

    /// <summary>Busca la definición de un filtro (null si es "Ninguno").</summary>
    public static DefinicionFiltro? Buscar(TipoFiltro tipo) => Todos.FirstOrDefault(f => f.Tipo == tipo);

    /// <summary>Nombre bonito de un filtro para la ventana y para la voz.</summary>
    public static string Nombre(TipoFiltro tipo) => Buscar(tipo)?.NombreVisible ?? "Sin filtro";

    /// <summary>
    /// El filtro que va después del actual (para la orden «siguiente filtro»).
    /// Va dando la vuelta: Ninguno → Gafas → Orejas → Máscara → Gafas...
    /// </summary>
    public static TipoFiltro Siguiente(TipoFiltro actual)
    {
        int indice = -1;
        for (int i = 0; i < Todos.Count; i++)
        {
            if (Todos[i].Tipo == actual) indice = i;
        }
        return Todos[(indice + 1) % Todos.Count].Tipo;
    }
}
