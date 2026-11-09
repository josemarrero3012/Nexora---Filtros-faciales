using System.IO;

namespace Nexora.App.Servicios;

/// <summary>
/// Rutas de los archivos que necesita el programa. Están en la carpeta
/// "Recursos", que se copia junto al .exe al compilar.
/// </summary>
public static class RutasRecursos
{
    /// <summary>Carpeta Recursos al lado del ejecutable.</summary>
    public static string Carpeta => Path.Combine(AppContext.BaseDirectory, "Recursos");

    /// <summary>Haar Cascade de caras (el mismo de clase).</summary>
    public static string CascadaCara => Path.Combine(Carpeta, "Cascadas", "haarcascade_frontalface_default.xml");

    /// <summary>Haar Cascade de ojos.</summary>
    public static string CascadaOjos => Path.Combine(Carpeta, "Cascadas", "haarcascade_eye.xml");

    /// <summary>Carpeta con los PNG de los filtros.</summary>
    public static string CarpetaFiltros => Path.Combine(Carpeta, "Filtros");

    /// <summary>Modelo de 21 puntos de la mano (ONNX).</summary>
    public static string ModeloMano => Path.Combine(Carpeta, "Modelos", "handpose_estimation_mediapipe_2023feb.onnx");
}
