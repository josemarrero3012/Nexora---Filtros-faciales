# Nexora · Filtros faciales, gestos y voz

**Sistema multimodal de interacción persona-ordenador mediante visión artificial y reconocimiento de voz.**

Proyecto de **DAD · 2º DAM** — Unidad 2: *Interfaces Naturales de Usuario mediante voz y visión artificial*.

| | |
|---|---|
| **Integrantes** | Samara del Carmen Bolaños González · José Manuel López Marrero |
| **Centro** | Las Palmas de Gran Canaria |
| **Tecnología principal** | OpenCV (OpenCvSharp4), C#, WPF con MVVM, System.Speech |

La idea es sencilla: manejar el ordenador con la mano y con la voz, usando la webcam y el micrófono que ya trae cualquier portátil, y de paso ponernos filtros en la cara en tiempo real. Todo en una sola ventana y con cada módulo con su propio interruptor.

---

## Qué hace

| Módulo | Qué hace | Origen |
|---|---|---|
| **Filtros faciales** | Busca la cara y los ojos con Haar Cascade y les pone unas gafas, unas orejas de gato o un antifaz que siguen el movimiento (se escalan y se giran con la cabeza). | Visto en clase |
| **Control por gestos** | Saca 21 puntos de la mano con un modelo ONNX que abre el módulo DNN de OpenCV y reconoce 4 gestos para mover el ratón de verdad. | Ampliación con uso de IA |
| **Control por voz** | Entiende unas pocas órdenes en español (lista cerrada) y contesta en voz alta. Solo hace caso si está seguro al menos un 60 %. | Visto en clase |
| **Plan B** | Si la mano da guerra, el cursor se mueve con la cabeza usando la zona muerta y el clic se hace diciendo «clic». | Visto en clase |

### Gestos

Hay que poner la mano dentro del recuadro azul de la derecha (la **zona de control**).

| Gesto | Cómo se hace | Qué pasa |
|---|---|---|
| **Mover** | Solo el índice levantado | La punta del dedo es el cursor |
| **Clic** | Pinza: juntar las puntas del pulgar y el índice | Un clic izquierdo (solo uno aunque se quede cerrada) |
| **Desplazar** | Índice y corazón levantados | Al subir o bajar la mano, la página se mueve |
| **Pausa** | Puño cerrado | El cursor se queda quieto y se puede recolocar la mano |

### Órdenes de voz

| Orden | Qué hace |
|---|---|
| «activar control gestual» | Enciende el control con la mano |
| «pausar control» | Apaga el control con la mano |
| «siguiente filtro» | Cambia al siguiente filtro y dice cuál es |
| «quitar filtro» | Quita el filtro |
| «clic» | Hace un clic (sobre todo para el plan B) |
| «salir» | Pregunta si estamos seguros; entonces **solo** escucha «sí» o «no», una vez |

---

## Requisitos

- **Windows 10 u 11** (WPF, System.Speech y las funciones del ratón son solo de Windows).
- **SDK de .NET 10**.
- **Visual Studio 2026** (con la carga de trabajo *Desarrollo de escritorio de .NET*) o **JetBrains Rider 2025.3** o más nuevo.
- Una **webcam** y un **micrófono**.
- Para la voz: el **español instalado en Windows con el reconocimiento de voz** (*Configuración → Hora e idioma → Idioma y región → Español → Opciones → Voz*). Si no está, el resto del programa funciona igual y el historial avisa.

> En Linux o macOS el proyecto **compila** (por ejemplo, para mirarlo en Rider), pero **no se puede ejecutar**: hace falta Windows.

## Cómo abrirlo y ejecutarlo

### Visual Studio
1. Abrir `Nexora.sln`.
2. Clic derecho en **Nexora.App** → *Establecer como proyecto de inicio*.
3. **F5**. La primera vez tarda un poco porque descarga los paquetes NuGet.

### JetBrains Rider
1. *Open* → `Nexora.sln`.
2. Arriba a la derecha, elegir la configuración **Nexora.App**.
3. **Run** (Mayús + F10).

### Terminal
```bash
dotnet run --project src/Nexora.App
```

### Pruebas automáticas
Desde el *Explorador de pruebas* de Visual Studio, la ventana *Unit Tests* de Rider o con:
```bash
dotnet test
```
Son 46 pruebas de la lógica (gestos, zona muerta, colocación de filtros, órdenes de voz y medidor de FPS). No necesitan cámara ni micrófono, así que se pueden lanzar en cualquier ordenador.

---

## Estructura del proyecto

```
Nexora.sln
├── src/
│   ├── Nexora.Core/              ← la lógica (no depende de Windows ni de WPF)
│   │   ├── Configuracion/        AjustesNexora: todos los "números mágicos" juntos
│   │   ├── Modelos/              Gesto, ComandoVoz, TipoFiltro, EventoNexora, resultados...
│   │   ├── Vision/               DetectorCaras, DetectorMano, AplicadorFiltros, Dibujante...
│   │   ├── Gestos/               ClasificadorGestos (los 4 gestos con histéresis)
│   │   ├── Cursor/               ControlCursor (suavizado, zona muerta, plan B, rueda)
│   │   ├── Voz/                  InterpreteOrdenes (lista cerrada y el 60 %)
│   │   └── Utilidades/           Geometría, suavizado y medidor de rendimiento
│   │
│   └── Nexora.App/               ← la aplicación WPF (MVVM)
│       ├── Views/                MainWindow.xaml (solo Binding, nada de lógica)
│       ├── ViewModels/           MainViewModel (CommunityToolkit.Mvvm)
│       ├── Servicios/            Un servicio por módulo: Camara, Cara, Gestos, Voz, Raton
│       ├── Interop/              SetCursorPos, SendInput... (user32.dll)
│       └── Recursos/
│           ├── Cascadas/         haarcascade_frontalface_default.xml, haarcascade_eye.xml
│           ├── Filtros/          gafas.png, orejas.png, mascara.png
│           └── Modelos/          handpose_estimation_mediapipe_2023feb.onnx
│
├── tests/
│   └── Nexora.Tests/             ← pruebas con xUnit de Nexora.Core
│
├── herramientas/
│   └── generar_filtros.py        ← script con el que dibujamos los PNG de los filtros
│
└── docs/
    └── Pruebas.md                ← plantilla para apuntar las medidas de la memoria
```

**¿Por qué dos proyectos?** Así la ventana solo se encarga de pintar y escuchar eventos (MVVM, como en el mando multimodal de clase) y toda la lógica se puede probar con tests sin abrir la cámara.

---

## Cómo funciona por dentro

### El recorrido de cada imagen

La cámara trabaja en su **propio hilo** (`ServicioCamara`) para que la ventana no se congele. Cada imagen hace siempre el mismo camino y la cámara solo se lee una vez:

1. **`VideoCapture.Read`** — leemos una imagen (unas 30 por segundo).
2. **`Flip`** — la volteamos como un espejo para que al mover la mano a la derecha el cursor también vaya a la derecha.
3. **`CvtColor`** — la pasamos a gris, que es como trabaja el detector de caras (OpenCV guarda los colores en BGR, no en RGB).
4. **Mano** (`ServicioGestos`) → **cara y filtro** (`ServicioCara`) → **plan B**, según los módulos que estén encendidos.
5. La imagen ya procesada llega a la ventana con un **evento** y se pinta con **`ToWriteableBitmap`**.

### Qué usamos de OpenCV y dónde está en el código

| Qué hacemos | Con qué | Dónde | Origen |
|---|---|---|---|
| Leer la webcam imagen a imagen | `VideoCapture`, `Read` | `ServicioCamara` | Clase |
| Voltear la imagen como un espejo | `Flip` | `ServicioCamara` | Ampliación con IA |
| Pasar la imagen a gris para el detector | `CvtColor` | `ServicioCamara` | Clase |
| Encontrar la cara (1,1 · 4 vecinos · mínimo 80×80) | `CascadeClassifier`, `DetectMultiScale` | `DetectorCaras` | Clase |
| Encontrar los ojos dentro de la cara | Haar Cascade con el archivo de ojos | `DetectorCaras` | Clase |
| Escalar, girar y mezclar el PNG con su transparencia | `Resize`, `GetRotationMatrix2D`, `WarpAffine` | `AplicadorFiltros` | Ampliación con IA |
| Sacar los 21 puntos de la mano | Módulo DNN: `ReadNetFromOnnx`, `Forward` | `DetectorMano` | Ampliación con IA |
| Dibujar recuadros y llevar la imagen a la ventana | `Rectangle`, `ToWriteableBitmap` | `Dibujante`, `MainViewModel` | Clase |
| Entender las órdenes, filtrar por confianza (60 %) y contestar | `Choices`, `GrammarBuilder`, `SpeechSynthesizer` | `ServicioVoz`, `InterpreteOrdenes` | Clase |
| Ventana con un interruptor por módulo y un historial | WPF con MVVM (CommunityToolkit.Mvvm) | `MainWindow`, `MainViewModel` | Clase |
| Mover el ratón y hacer clic | `SetCursorPos`, `SendInput` | `ServicioRaton`, `MetodosNativos` | Ampliación con IA |

### Los filtros: «los ojos hacen de regla y de transportador»

Con los dos ojos ya tenemos todo lo necesario (`GeometriaFiltro`):

- La **distancia** entre ellos dice de qué **tamaño** poner el filtro.
- La **inclinación** de la línea que los une dice cuánto hay que **girarlo**.

En cada PNG los ojos están siempre en las mismas coordenadas (x = 180 y x = 460), y eso es lo que usamos para encajarlo. Si en una imagen no encuentra los ojos, los colocamos con las proporciones del recuadro de la cara para que el filtro no se quede colgando. Además suavizamos la posición de los ojos entre imágenes para que el filtro no tiemble.

### Los gestos: medir distancias entre puntos

El modelo solo nos da los 21 puntos; decidir el gesto lo hacemos nosotros (`ClasificadorGestos`):

- Un dedo está **estirado** si su punta está bastante más lejos de la muñeca que su nudillo del medio.
- La **pinza** compara la distancia pulgar–índice con el tamaño de la mano. Tiene **dos límites** (uno para cerrar y otro, más grande, para soltar) y así no se dispara dos veces sin querer.
- Un gesto nuevo tiene que verse **3 imágenes seguidas** para hacerle caso.

El cursor (`ControlCursor`) usa la **zona muerta** del ejercicio de la cabeza: si la mano casi no se mueve, el cursor se queda quieto y no tiembla. Además suavizamos la posición entre una imagen y la siguiente.

### La voz

`ServicioVoz` le da al reconocedor de System.Speech **solo** las frases válidas, así no intenta entender cualquier cosa. Escucha todo el rato y, cada vez que reconoce una, salta un evento con el texto y lo seguro que está. Si pasa del 60 %, hacemos la acción y el sintetizador la confirma. Mientras el programa habla, no le hace caso al micrófono para que no se «oiga a sí mismo».

---

## Evaluación

En la ventana se ven en directo las tres cosas que prometimos medir:

- **Fluidez**: imágenes por segundo y milisegundos por imagen.
- **Tiempo de respuesta**: del gesto (desde que se empieza a ver hasta que se confirma) y de la voz (desde que terminamos de hablar hasta que llega la orden).
- **Aciertos**: el historial apunta cada gesto y cada orden con su confianza. Con **Exportar CSV** se guarda en *Documentos* para contarlo en la memoria.

En el panel **Ajustes para las pruebas** se pueden cambiar en marcha el factor de escala del detector (1,1 / 1,3...), los vecinos mínimos, la confianza mínima de la voz, la zona muerta y el suavizado del cursor, para elegir cada valor con datos y no a ojo.

La plantilla para apuntar los resultados está en [`docs/Pruebas.md`](docs/Pruebas.md).

---

## Problemas típicos

| Problema | Solución |
|---|---|
| «No se pudo abrir la webcam» | Cerrar Teams, Zoom, el navegador... cualquier programa que la esté usando, y pulsar *Volver a abrir la cámara*. |
| «Windows no tiene el reconocedor de voz en español» | Instalar el paquete de voz en español en la configuración de Windows (ver *Requisitos*). |
| La cara no sale con poca luz | Encender una luz delante (no detrás) o bajar los vecinos mínimos en *Ajustes para las pruebas*. |
| La mano no se detecta bien | Poner la mano entera dentro del recuadro azul, con la palma hacia la cámara. Si sigue sin ir, activar el **plan B**. |
| El cursor se vuelve loco | Cerrar el puño (pausa), decir «pausar control» o apagar el interruptor de *Control gestual*. |

---

## Créditos y licencias

- **Haar Cascades** (`haarcascade_frontalface_default.xml`, `haarcascade_eye.xml`): del repositorio oficial de [OpenCV](https://github.com/opencv/opencv/tree/4.x/data/haarcascades), con la licencia que viene dentro de cada archivo.
- **Modelo de la mano** (`handpose_estimation_mediapipe_2023feb.onnx`): del [OpenCV Model Zoo](https://github.com/opencv/opencv_zoo/tree/main/models/handpose_estimation_mediapipe), basado en MediaPipe. Licencia Apache 2.0 (ver `Recursos/Modelos/LICENSE-handpose.txt`).
- **PNG de los filtros**: dibujados por nosotros con `herramientas/generar_filtros.py`.
- Librerías: [OpenCvSharp4](https://github.com/shimat/opencvsharp), [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet), System.Speech.
