using OpenCvSharp;

namespace Nexora.Core.Modelos;

/// <summary>
/// Los 21 puntos de la mano que nos devuelve el modelo, ya pasados a
/// coordenadas de la imagen completa de la cámara.
/// </summary>
/// <param name="Puntos">21 puntos (ver <see cref="Nexora.Core.Vision.PuntosMano"/> para saber cuál es cuál).</param>
/// <param name="Confianza">Lo seguro que está el modelo de que hay una mano (de 0 a 1).</param>
public sealed record ResultadoMano(Point2f[] Puntos, float Confianza);
