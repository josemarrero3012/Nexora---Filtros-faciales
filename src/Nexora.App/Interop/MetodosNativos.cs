using System.Runtime.InteropServices;

namespace Nexora.App.Interop;

/// <summary>
/// Funciones de Windows (user32.dll) para mover el ratón y hacer clic "de verdad".
/// </summary>
/// <remarks>
/// <para><b>Ampliación con uso de IA</b>: esto no lo vimos en clase.</para>
/// <para>
/// .NET no trae nada para mover el ratón del sistema, así que llamamos
/// directamente a Windows con <c>DllImport</c> (P/Invoke):
/// </para>
/// <list type="bullet">
///   <item><c>SetCursorPos</c>: pone el cursor en una posición de la pantalla.</item>
///   <item><c>SendInput</c>: simula que hemos pulsado/soltado un botón o girado la rueda.</item>
///   <item><c>GetCursorPos</c>: dice dónde está el cursor (lo necesita el plan B).</item>
///   <item><c>GetSystemMetrics</c>: dice el tamaño de la pantalla principal.</item>
/// </list>
/// </remarks>
internal static class MetodosNativos
{
    // ---- Constantes de Windows (sacadas de la documentación de Microsoft) ----

    /// <summary>Tipo de entrada: ratón.</summary>
    public const uint INPUT_MOUSE = 0;

    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002; // botón izquierdo pulsado
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;   // botón izquierdo soltado
    public const uint MOUSEEVENTF_WHEEL = 0x0800;    // giro de la rueda

    public const int SM_CXSCREEN = 0; // ancho de la pantalla principal
    public const int SM_CYSCREEN = 1; // alto de la pantalla principal

    // ---- Estructuras que espera SendInput ----

    /// <summary>Un "evento" de entrada (ratón, en nuestro caso).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public UnionEntrada U;
    }

    /// <summary>
    /// En C, INPUT lleva una "union" (ratón, teclado o hardware en la misma
    /// memoria). Nosotros solo usamos el ratón, que además es el más grande,
    /// así que el tamaño total sale bien.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct UnionEntrada
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
    }

    /// <summary>Datos de un evento de ratón.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;   // para la rueda: cuánto se gira (120 = un "golpe")
        public uint dwFlags;     // qué ha pasado (MOUSEEVENTF_...)
        public uint time;
        public IntPtr dwExtraInfo;
    }

    /// <summary>Punto de Windows (para GetCursorPos).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    // ---- Funciones ----

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT punto);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint numeroEntradas, INPUT[] entradas, int tamanoEntrada);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int indice);
}
