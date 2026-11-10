using Nexora.Core.Modelos;

namespace Nexora.Core.Configuracion;

/// <summary>
/// Todos los "números mágicos" del proyecto juntos en un mismo sitio.
/// </summary>
/// <remarks>
/// <para>
/// En la propuesta dijimos que queríamos elegir cada valor con datos y no a ojo
/// (por ejemplo, probar el detector con 1,1 y con 1,3). Por eso los dejamos
/// aquí, con su valor por defecto, y algunos se pueden cambiar desde el panel
/// "Ajustes" de la ventana mientras el programa está funcionando.
/// </para>
/// <para>
/// Hay una sola instancia de esta clase que comparten todos los servicios. La
/// ventana (hilo de la interfaz) escribe y la cámara (su propio hilo) lee. Como
/// son tipos simples (bool, int, double), no pasa nada si en una imagen se lee
/// el valor viejo y en la siguiente el nuevo, así que no hace falta "lock".
/// </para>
/// </remarks>
public sealed class AjustesNexora
{
    // ------------------------------------------------------------------
    //  Interruptores de cada módulo (los "switches" de la ventana)
    // ------------------------------------------------------------------

    /// <summary>Si está activo, se buscan caras y se pinta el filtro.</summary>
    public bool FiltrosActivos { get; set; } = true;

    /// <summary>
    /// Si está activo, la mano (o la cabeza, en el plan B) mueve el ratón de
    /// verdad. Arranca apagado para que el cursor no se vuelva loco nada más
    /// abrir el programa.
    /// </summary>
    public bool ControlGestualActivo { get; set; }

    /// <summary>Plan B de la propuesta: mover el cursor con la cabeza en vez de con la mano.</summary>
    public bool ModoCabeza { get; set; }

    /// <summary>Pintar recuadros, puntos de la mano y textos de ayuda encima de la imagen.</summary>
    public bool MostrarAyudas { get; set; } = true;

    /// <summary>El filtro que está puesto ahora mismo.</summary>
    public TipoFiltro FiltroActual { get; set; } = TipoFiltro.Gafas;

    /// <summary>
    /// [Ampliación con uso de IA] Pintar el filtro con sombra suave, ajuste a
    /// la luz de la cara y aparecer/desaparecer suave. Se puede apagar desde la
    /// ventana para comparar con el filtro "tal cual" en las pruebas.
    /// </summary>
    public bool FiltroRealista { get; set; } = true;

    // ------------------------------------------------------------------
    //  Cámara
    // ------------------------------------------------------------------

    /// <summary>Índice de la webcam (0 = la que trae el portátil).</summary>
    public int IndiceCamara { get; set; }

    /// <summary>Ancho que le pedimos a la cámara. 640x480 va sobrado y es rápido.</summary>
    public int AnchoCamara { get; set; } = 640;

    /// <summary>Alto que le pedimos a la cámara.</summary>
    public int AltoCamara { get; set; } = 480;

    // ------------------------------------------------------------------
    //  Detector de caras y ojos (Haar Cascade, visto en clase)
    // ------------------------------------------------------------------

    /// <summary>
    /// Cuánto crece la "ventanita" del detector en cada pasada. En clase usamos
    /// 1,1 (de un 10 % en un 10 %). Con 1,3 va más rápido pero se le escapan caras.
    /// </summary>
    public double FactorEscalaCara { get; set; } = 1.1;

    /// <summary>
    /// Cuántas ventanas tienen que coincidir para dar la cara por buena. En clase
    /// pedíamos cuatro. Con más se equivoca menos, pero necesita mejor luz.
    /// </summary>
    public int VecinosMinimosCara { get; set; } = 4;

    /// <summary>Tamaño mínimo de la cara en píxeles (80 x 80, como en clase).</summary>
    public int LadoMinimoCara { get; set; } = 80;

    /// <summary>Factor de escala para el detector de ojos (dentro de la cara).</summary>
    public double FactorEscalaOjos { get; set; } = 1.1;

    /// <summary>
    /// Vecinos mínimos para los ojos. Lo subimos un poco respecto a la cara
    /// porque el archivo de ojos a veces confunde las cejas o la nariz con ojos.
    /// </summary>
    public int VecinosMinimosOjos { get; set; } = 6;

    /// <summary>
    /// Cuánto "se fía" de la posición nueva de los ojos en cada imagen (0..1).
    /// Con 1 el filtro va pegado pero tiembla; con valores bajos va más suave.
    /// </summary>
    public double SuavizadoOjos { get; set; } = 0.5;

    // ------------------------------------------------------------------
    //  Mano (modelo de 21 puntos con el módulo DNN, ampliación con IA)
    // ------------------------------------------------------------------

    /// <summary>Confianza mínima del modelo para decir que hay una mano (0..1).</summary>
    public float ConfianzaMinimaMano { get; set; } = 0.7f;

    /// <summary>
    /// Lado de la zona de control (el recuadro donde hay que poner la mano),
    /// como fracción del alto de la imagen. Con 0,6 y una imagen de 480 de alto
    /// sale un cuadrado de 288 px.
    /// </summary>
    public double LadoZonaControl { get; set; } = 0.6;

    /// <summary>
    /// Margen interior de la zona de control (fracción). La punta del dedo solo
    /// tiene que recorrer la parte de dentro para llegar a los bordes de la
    /// pantalla, así no hace falta sacar la mano del recuadro.
    /// </summary>
    public double MargenZonaControl { get; set; } = 0.15;

    /// <summary>
    /// Un dedo cuenta como "estirado" si su punta está al menos este número de
    /// veces más lejos de la muñeca que su nudillo del medio.
    /// </summary>
    public double FactorDedoEstirado { get; set; } = 1.15;

    /// <summary>
    /// Límite para CERRAR la pinza (distancia pulgar-índice dividida entre el
    /// tamaño de la mano). Por debajo, cuenta como clic.
    /// </summary>
    public double UmbralPinzaCerrar { get; set; } = 0.30;

    /// <summary>
    /// Límite para SOLTAR la pinza. Es más grande que el de cerrar a propósito
    /// (histéresis): así, si la distancia baila justo en el límite, no se hacen
    /// dos clics sin querer.
    /// </summary>
    public double UmbralPinzaSoltar { get; set; } = 0.45;

    /// <summary>
    /// Cuántas imágenes seguidas tiene que verse el mismo gesto para hacerle
    /// caso. A 30 imágenes por segundo, 3 imágenes son unos 100 ms.
    /// </summary>
    public int FotogramasParaConfirmarGesto { get; set; } = 3;

    // ------------------------------------------------------------------
    //  Cursor (suavizado y zona muerta, vistos en clase)
    // ------------------------------------------------------------------

    /// <summary>
    /// Suavizado del cursor (0..1). Cada imagen el cursor recorre este trozo del
    /// camino hasta donde está el dedo. Con 0,35 va fino sin notarse retraso.
    /// </summary>
    public double SuavizadoCursor { get; set; } = 0.35;

    /// <summary>
    /// Zona muerta del cursor con la mano, en píxeles de pantalla. Si el cursor
    /// se tendría que mover menos que esto, no lo movemos y así no tiembla.
    /// </summary>
    public double ZonaMuertaPx { get; set; } = 6;

    /// <summary>
    /// Zona muerta del plan B (cursor con la cabeza), en píxeles de la imagen de
    /// la cámara. Es la misma idea del ejercicio del mando con la cabeza: si la
    /// cara está cerca del centro, el cursor no se mueve.
    /// </summary>
    public double ZonaMuertaCabezaPx { get; set; } = 40;

    /// <summary>Velocidad del cursor con la cabeza: píxeles por imagen por cada píxel fuera de la zona muerta.</summary>
    public double GananciaCabeza { get; set; } = 0.25;

    /// <summary>Cada cuántos píxeles de movimiento de la mano hacemos una "rueda" de desplazamiento.</summary>
    public double PasoDesplazamientoPx { get; set; } = 12;

    // ------------------------------------------------------------------
    //  Voz (System.Speech, visto en clase)
    // ------------------------------------------------------------------

    /// <summary>Solo hacemos caso a la orden si el reconocedor está seguro al menos un 60 %.</summary>
    public double ConfianzaMinimaVoz { get; set; } = 0.6;
}
