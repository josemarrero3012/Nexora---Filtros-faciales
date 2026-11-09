using System.Runtime.InteropServices;
using Nexora.App.Interop;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace Nexora.App.Servicios;

/// <summary>
/// Mueve el ratón de Windows y hace clic o desplaza con la rueda.
/// </summary>
/// <remarks>
/// Es la única clase que toca el ratón de verdad. Las cuentas de a dónde ir
/// se hacen antes en <c>ControlCursor</c> (Nexora.Core).
/// Estas funciones se pueden llamar desde cualquier hilo (también desde el de la cámara).
/// </remarks>
public sealed class ServicioRaton
{
    /// <summary>Tamaño de la pantalla principal en píxeles.</summary>
    public Size TamanoPantalla => new(
        MetodosNativos.GetSystemMetrics(MetodosNativos.SM_CXSCREEN),
        MetodosNativos.GetSystemMetrics(MetodosNativos.SM_CYSCREEN));

    /// <summary>Dónde está el cursor ahora mismo.</summary>
    public Point PosicionActual
    {
        get
        {
            MetodosNativos.GetCursorPos(out MetodosNativos.POINT p);
            return new Point(p.X, p.Y);
        }
    }

    /// <summary>Pone el cursor en esa posición de la pantalla (SetCursorPos).</summary>
    public void MoverA(Point posicion) => MetodosNativos.SetCursorPos(posicion.X, posicion.Y);

    /// <summary>Clic izquierdo: pulsar y soltar (dos eventos con SendInput).</summary>
    public void Clic()
    {
        MetodosNativos.INPUT[] entradas =
        [
            EventoRaton(MetodosNativos.MOUSEEVENTF_LEFTDOWN, 0),
            EventoRaton(MetodosNativos.MOUSEEVENTF_LEFTUP, 0)
        ];
        Enviar(entradas);
    }

    /// <summary>
    /// Gira la rueda del ratón.
    /// </summary>
    /// <param name="cantidad">Múltiplos de 120. Positivo = hacia arriba, negativo = hacia abajo.</param>
    public void Rueda(int cantidad)
    {
        if (cantidad == 0) return;
        // mouseData es "uint" pero Windows lo lee con signo, por eso el unchecked.
        Enviar([EventoRaton(MetodosNativos.MOUSEEVENTF_WHEEL, unchecked((uint)cantidad))]);
    }

    private static MetodosNativos.INPUT EventoRaton(uint flags, uint datos) => new()
    {
        type = MetodosNativos.INPUT_MOUSE,
        U = new MetodosNativos.UnionEntrada
        {
            mi = new MetodosNativos.MOUSEINPUT { dwFlags = flags, mouseData = datos }
        }
    };

    private static void Enviar(MetodosNativos.INPUT[] entradas)
        => MetodosNativos.SendInput((uint)entradas.Length, entradas, Marshal.SizeOf<MetodosNativos.INPUT>());
}
