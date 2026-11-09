using OpenCvSharp;

namespace Nexora.Core.Modelos;

/// <summary>
/// Lo que nos devuelve el detector de caras en cada imagen.
/// </summary>
/// <param name="Cara">Recuadro de la cara (el de Haar Cascade, como en clase).</param>
/// <param name="OjoIzquierdo">Centro del ojo que sale a la IZQUIERDA de la imagen.</param>
/// <param name="OjoDerecho">Centro del ojo que sale a la DERECHA de la imagen.</param>
/// <param name="OjosDetectados">
/// true si los ojos los ha encontrado el detector de ojos de verdad; false si
/// los hemos calculado "a ojo" con las proporciones del recuadro de la cara.
/// </param>
/// <remarks>
/// Ojo con lo de izquierdo/derecho: como la imagen va volteada como un espejo,
/// el "ojo izquierdo de la imagen" es justo eso, el que se ve a la izquierda.
/// No nos complicamos con si es el ojo izquierdo de la persona o no.
/// </remarks>
public sealed record ResultadoCara(Rect Cara, Point2f OjoIzquierdo, Point2f OjoDerecho, bool OjosDetectados)
{
    /// <summary>Centro del recuadro de la cara (lo usa el plan B para mover el cursor con la cabeza).</summary>
    public Point2f Centro => new(Cara.X + Cara.Width / 2f, Cara.Y + Cara.Height / 2f);
}
