using System.Diagnostics;

namespace Nexora.Core.Utilidades;

/// <summary>
/// Mide la fluidez del programa: cuántas imágenes por segundo (FPS) procesamos
/// y cuántos milisegundos tarda cada imagen. Son dos de las tres cosas que
/// prometimos medir en la propuesta (la tercera, los aciertos, la contamos a mano).
/// </summary>
/// <remarks>
/// Para los FPS guardamos el momento de las últimas imágenes en una cola y
/// miramos cuántas caben en el tiempo que ha pasado. Para los milisegundos
/// hacemos una media "suave" para que el número de la ventana no baile tanto.
/// </remarks>
public sealed class MedidorRendimiento
{
    private readonly Queue<long> _marcas = new();
    private readonly int _ventana;
    private readonly object _cerrojo = new();
    private double _msMedios;
    private bool _tieneMedia;

    /// <param name="ventana">Cuántas imágenes usamos para calcular los FPS (30 = más o menos el último segundo).</param>
    public MedidorRendimiento(int ventana = 30)
    {
        _ventana = Math.Max(2, ventana);
    }

    /// <summary>Imágenes por segundo de las últimas imágenes.</summary>
    public double Fps
    {
        get
        {
            lock (_cerrojo)
            {
                if (_marcas.Count < 2) return 0;
                double segundos = (_marcas.Last() - _marcas.Peek()) / (double)Stopwatch.Frequency;
                return segundos > 0 ? (_marcas.Count - 1) / segundos : 0;
            }
        }
    }

    /// <summary>Milisegundos medios que tarda en procesarse una imagen.</summary>
    public double MsPorFotograma
    {
        get { lock (_cerrojo) return _msMedios; }
    }

    /// <summary>
    /// Apunta que hemos terminado una imagen y lo que ha tardado.
    /// </summary>
    /// <param name="duracion">Lo que ha tardado en procesarse la imagen.</param>
    /// <param name="marcaTiempo">
    /// Momento en el que se terminó (en ticks de <see cref="Stopwatch"/>). Si no
    /// se pasa, se usa el momento actual. Lo dejamos como parámetro para poder
    /// hacer pruebas sin tener que esperar de verdad.
    /// </param>
    public void Registrar(TimeSpan duracion, long? marcaTiempo = null)
    {
        lock (_cerrojo)
        {
            _marcas.Enqueue(marcaTiempo ?? Stopwatch.GetTimestamp());
            while (_marcas.Count > _ventana) _marcas.Dequeue();

            double ms = duracion.TotalMilliseconds;
            // Media exponencial: 90 % de lo anterior y 10 % de lo nuevo.
            _msMedios = _tieneMedia ? _msMedios * 0.9 + ms * 0.1 : ms;
            _tieneMedia = true;
        }
    }

    /// <summary>Borra todo lo medido (por ejemplo, al reiniciar la cámara).</summary>
    public void Reiniciar()
    {
        lock (_cerrojo)
        {
            _marcas.Clear();
            _msMedios = 0;
            _tieneMedia = false;
        }
    }
}
