namespace Nexora.Core.Vision;

/// <summary>
/// Chuleta con el número de cada uno de los 21 puntos de la mano del modelo de
/// MediaPipe. La muñeca es el 0 y luego van cuatro puntos por dedo, desde la
/// base hasta la punta.
/// </summary>
/// <remarks>
/// <code>
///             8   12  16  20      ← puntas
///             |   |   |   |
///             7   11  15  19
///         4   |   |   |   |
///         |   6   10  14  18      ← nudillo del medio (PIP)
///         3   |   |   |   |
///          \  5---9---13--17      ← nudillo de la base (MCP)
///           2     \       /
///            \     \     /
///             1-----\---/
///                    0            ← muñeca
/// </code>
/// </remarks>
public static class PuntosMano
{
    /// <summary>Cuántos puntos devuelve el modelo.</summary>
    public const int Total = 21;

    public const int Muneca = 0;

    // Pulgar
    public const int PulgarBase = 1;
    public const int PulgarMcp = 2;
    public const int PulgarIp = 3;
    public const int PulgarPunta = 4;

    // Índice
    public const int IndiceMcp = 5;
    public const int IndicePip = 6;
    public const int IndiceDip = 7;
    public const int IndicePunta = 8;

    // Corazón (dedo del medio)
    public const int CorazonMcp = 9;
    public const int CorazonPip = 10;
    public const int CorazonDip = 11;
    public const int CorazonPunta = 12;

    // Anular
    public const int AnularMcp = 13;
    public const int AnularPip = 14;
    public const int AnularDip = 15;
    public const int AnularPunta = 16;

    // Meñique
    public const int MeniqueMcp = 17;
    public const int MeniquePip = 18;
    public const int MeniqueDip = 19;
    public const int MeniquePunta = 20;

    /// <summary>
    /// Parejas de puntos que se unen con una línea para dibujar el "esqueleto"
    /// de la mano encima de la imagen.
    /// </summary>
    public static readonly (int Desde, int Hasta)[] Huesos =
    [
        // Pulgar
        (0, 1), (1, 2), (2, 3), (3, 4),
        // Índice
        (0, 5), (5, 6), (6, 7), (7, 8),
        // Corazón
        (9, 10), (10, 11), (11, 12),
        // Anular
        (13, 14), (14, 15), (15, 16),
        // Meñique
        (0, 17), (17, 18), (18, 19), (19, 20),
        // Palma (la línea de los nudillos)
        (5, 9), (9, 13), (13, 17)
    ];
}
