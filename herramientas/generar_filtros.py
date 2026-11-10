"""
generar_filtros.py
==================

Script de apoyo que usamos para dibujar los PNG de los filtros (gafas, orejas
y máscara) en vez de bajarnos imágenes de internet con derechos de autor.

Solo necesita Python 3 y numpy.

[Ampliación con IA] En la versión 2 de los filtros añadimos degradados,
bordes difuminados y algunos detalles (ver docs/Uso-de-IA.md). Las
coordenadas de los ojos NO cambian, así que el código C# sigue igual. Se lanza desde la raíz del repositorio:

    python herramientas/generar_filtros.py

y deja los tres PNG en src/Nexora.App/Recursos/Filtros/.

Cómo funciona por encima:
  - Dibujamos cada figura en un lienzo 4 veces más grande (supermuestreo) y
    luego lo reducimos haciendo la media de cada bloque de 4x4. Así los bordes
    salen suavizados y no "en escalera".
  - Cada PNG tiene canal alfa (transparencia). Donde no hay dibujo, alfa = 0.
  - IMPORTANTE: en todos los filtros los ojos quedan en las mismas
    coordenadas (x = 180 y x = 460, separados 280 px). Esas coordenadas son las
    que ponemos en CatalogoFiltros.cs, y es lo que usa el programa para saber
    dónde "encajar" el PNG encima de los ojos que detecta OpenCV.
"""

import os
import struct
import zlib

import numpy as np

SUPER = 4  # factor de supermuestreo para suavizar los bordes

# Carpeta de salida (relativa a la raíz del repositorio)
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SALIDA = os.path.join(RAIZ, "src", "Nexora.App", "Recursos", "Filtros")


# ---------------------------------------------------------------------------
#  Utilidades de dibujo
# ---------------------------------------------------------------------------

class Lienzo:
    """Lienzo RGBA en coma flotante (valores de 0 a 1) con supermuestreo."""

    def __init__(self, ancho, alto):
        self.ancho = ancho
        self.alto = alto
        self.rgba = np.zeros((alto, ancho, 4), dtype=np.float64)
        # Coordenadas del centro de cada "subpíxel" en coordenadas del PNG final
        ys = (np.arange(alto * SUPER) + 0.5) / SUPER
        xs = (np.arange(ancho * SUPER) + 0.5) / SUPER
        self.x, self.y = np.meshgrid(xs, ys)

    def _cobertura(self, mascara_grande):
        """Pasa una máscara booleana grande a cobertura (0..1) del tamaño final."""
        m = mascara_grande.astype(np.float64)
        m = m.reshape(self.alto, SUPER, self.ancho, SUPER).mean(axis=(1, 3))
        return m

    def pintar(self, mascara_grande, color, opacidad=1.0):
        """Pinta encima de lo que ya hay usando la fórmula de mezcla "over".

        [Ampliación con IA] `color` puede ser un color fijo (r, g, b) o un
        degradado (una tabla alto x ancho x 3 sacada de `degradado`), y
        `opacidad` puede ser un número o una tabla alto x ancho.
        """
        a = self._cobertura(mascara_grande) * opacidad
        if isinstance(color, np.ndarray):
            r, g, b = (color[..., i] / 255.0 for i in range(3))
        else:
            r, g, b = [c / 255.0 for c in color]
        fondo_a = self.rgba[..., 3]
        nuevo_a = a + fondo_a * (1 - a)
        for i, c in enumerate((r, g, b)):
            fondo_c = self.rgba[..., i]
            # Guardamos el color "sin premultiplicar"
            num = c * a + fondo_c * fondo_a * (1 - a)
            self.rgba[..., i] = np.divide(num, nuevo_a, out=np.zeros_like(num), where=nuevo_a > 0)
        self.rgba[..., 3] = nuevo_a

    def borrar(self, mascara_grande):
        """Hace transparente una zona (para los agujeros de la máscara)."""
        a = self._cobertura(mascara_grande)
        self.rgba[..., 3] *= (1 - a)

    # --- [Ampliación con IA] Degradados y bordes suaves -------------------

    def degradado(self, y0, y1, color0, color1, x0=None, x1=None):
        """Degradado lineal de color0 a color1 (vertical, u horizontal si se
        dan x0 y x1). Devuelve una tabla alto x ancho x 3 del tamaño FINAL."""
        ys = (np.arange(self.alto) + 0.5)[:, None] * np.ones((1, self.ancho))
        xs = np.ones((self.alto, 1)) * (np.arange(self.ancho) + 0.5)[None, :]
        if x0 is not None:
            t = np.clip((xs - x0) / (x1 - x0), 0, 1)
        else:
            t = np.clip((ys - y0) / (y1 - y0), 0, 1)
        c0, c1 = np.array(color0, float), np.array(color1, float)
        return c0 + (c1 - c0) * t[..., None]

    def degradado_radial(self, cx, cy, radio, color_centro, color_borde):
        """Degradado circular: color_centro en (cx, cy) y color_borde a `radio`."""
        ys = (np.arange(self.alto) + 0.5)[:, None]
        xs = (np.arange(self.ancho) + 0.5)[None, :]
        t = np.clip(np.sqrt((xs - cx) ** 2 + (ys - cy) ** 2) / radio, 0, 1)
        c0, c1 = np.array(color_centro, float), np.array(color_borde, float)
        return c0 + (c1 - c0) * t[..., None]

    def elipse_suave(self, cx, cy, rx, ry, difuminado=0.5):
        """Elipse con el borde difuminado (para coloretes y brillos).
        Devuelve una máscara 0..1 en vez de verdadero/falso."""
        d = np.sqrt(((self.x - cx) / rx) ** 2 + ((self.y - cy) / ry) ** 2)
        return np.clip((1.0 - d) / difuminado, 0, 1)

    def elipse_girada(self, cx, cy, rx, ry, grados):
        """Elipse girada `grados` (para la pluma del antifaz)."""
        a = np.radians(grados)
        dx, dy = self.x - cx, self.y - cy
        u = dx * np.cos(a) + dy * np.sin(a)
        v = -dx * np.sin(a) + dy * np.cos(a)
        return (u / rx) ** 2 + (v / ry) ** 2 <= 1.0

    def arco(self, cx, cy, rx, ry, grosor, desde, hasta):
        """Trozo de anillo entre dos ángulos (en grados, 0 = derecha, 90 = abajo)."""
        ang = np.degrees(np.arctan2((self.y - cy) / ry, (self.x - cx) / rx)) % 360
        dentro = (ang >= desde % 360) & (ang <= hasta % 360) if desde % 360 <= hasta % 360 \
            else (ang >= desde % 360) | (ang <= hasta % 360)
        return self.anillo(cx, cy, rx, ry, grosor) & dentro

    # --- Figuras básicas (devuelven máscaras booleanas grandes) -------------

    def elipse(self, cx, cy, rx, ry):
        return ((self.x - cx) / rx) ** 2 + ((self.y - cy) / ry) ** 2 <= 1.0

    def anillo(self, cx, cy, rx, ry, grosor):
        fuera = self.elipse(cx, cy, rx, ry)
        dentro = self.elipse(cx, cy, rx - grosor, ry - grosor)
        return fuera & ~dentro

    def linea(self, x1, y1, x2, y2, grosor):
        """Segmento con extremos redondeados (distancia punto-segmento)."""
        dx, dy = x2 - x1, y2 - y1
        largo2 = dx * dx + dy * dy
        t = ((self.x - x1) * dx + (self.y - y1) * dy) / largo2
        t = np.clip(t, 0, 1)
        px, py = x1 + t * dx, y1 + t * dy
        return (self.x - px) ** 2 + (self.y - py) ** 2 <= (grosor / 2) ** 2

    def triangulo(self, p1, p2, p3):
        """Triángulo relleno usando el signo de los productos vectoriales."""
        def lado(a, b):
            return (b[0] - a[0]) * (self.y - a[1]) - (b[1] - a[1]) * (self.x - a[0])
        d1, d2, d3 = lado(p1, p2), lado(p2, p3), lado(p3, p1)
        neg = (d1 < 0) | (d2 < 0) | (d3 < 0)
        pos = (d1 > 0) | (d2 > 0) | (d3 > 0)
        return ~(neg & pos)

    def guardar_png(self, ruta):
        datos = (np.clip(self.rgba, 0, 1) * 255 + 0.5).astype(np.uint8)
        escribir_png(ruta, datos)


def escribir_png(ruta, rgba):
    """Escritor de PNG mínimo (RGBA de 8 bits) para no depender de Pillow."""
    alto, ancho, _ = rgba.shape
    # Cada fila empieza con un byte de filtro (0 = sin filtro)
    filas = b"".join(b"\x00" + rgba[y].tobytes() for y in range(alto))

    def trozo(tipo, datos):
        crc = zlib.crc32(tipo + datos) & 0xFFFFFFFF
        return struct.pack(">I", len(datos)) + tipo + datos + struct.pack(">I", crc)

    cabecera = struct.pack(">IIBBBBB", ancho, alto, 8, 6, 0, 0, 0)  # 8 bits, RGBA
    with open(ruta, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(trozo(b"IHDR", cabecera))
        f.write(trozo(b"IDAT", zlib.compress(filas, 9)))
        f.write(trozo(b"IEND", b""))


# ---------------------------------------------------------------------------
#  Los tres filtros
# ---------------------------------------------------------------------------

OJO_IZQ_X, OJO_DER_X = 180, 460  # mismas X en todos los filtros


def gafas():
    """Gafas de sol redondas de montura dorada. Ojos en (180, 110) y (460, 110).

    [Ampliación con IA] Versión 2: los cristales llevan un degradado (más
    oscuros arriba, como las gafas de verdad), la montura es de metal con un
    brillo encima, hay doble puente, y el reflejo es
    una franja curva en vez de una raya.
    """
    c = Lienzo(640, 220)
    oy = 110
    oro_claro, oro, oro_oscuro = (246, 222, 160), (204, 162, 82), (120, 86, 34)

    # Cristales: degradado de humo verdoso, más opaco arriba que abajo.
    color_cristal = c.degradado(oy - 92, oy + 92, (22, 34, 40), (70, 92, 96))
    opacidad_cristal = 0.90 - 0.30 * np.clip((np.arange(220)[:, None] + 0.5 - (oy - 92)) / 184, 0, 1)
    for ox in (OJO_IZQ_X, OJO_DER_X):
        c.pintar(c.elipse(ox, oy, 102, 90), color_cristal, opacidad=opacidad_cristal)

    # Reflejo: una franja curva arriba a la izquierda y un destello pequeño.
    for ox in (OJO_IZQ_X, OJO_DER_X):
        c.pintar(c.arco(ox, oy, 78, 68, 14, 200, 250), (255, 255, 255), opacidad=0.40)
        c.pintar(c.elipse_suave(ox + 48, oy + 44, 10, 10, 0.8), (255, 255, 255), opacidad=0.45)

    # Montura de metal: degradado dorado y un hilo de brillo por arriba.
    metal = c.degradado(oy - 100, oy + 100, oro_claro, oro_oscuro)
    for ox in (OJO_IZQ_X, OJO_DER_X):
        c.pintar(c.anillo(ox, oy, 108, 96, 9), metal)
        c.pintar(c.arco(ox, oy, 106, 94, 3, 205, 335), oro_claro, opacidad=0.9)

    # Doble puente (como las gafas de aviador).
    c.pintar(c.linea(OJO_IZQ_X + 102, oy - 34, OJO_DER_X - 102, oy - 34, 8), oro)
    c.pintar(c.linea(OJO_IZQ_X + 106, oy - 6, OJO_DER_X - 106, oy - 6, 5), oro_oscuro)

    # Patillas cortas hacia los lados, del mismo metal.
    c.pintar(c.linea(OJO_IZQ_X - 106, oy - 28, 8, oy - 42, 9), metal)
    c.pintar(c.linea(OJO_DER_X + 106, oy - 28, 632, oy - 42, 9), metal)
    return c


def orejas():
    """Filtro de gato: orejas, nariz, bigotes y coloretes. Ojos en (180, 540) y (460, 540).

    Las orejas van bastante por encima de los ojos (unos 310 px) para que
    queden encima de la cabeza y no en mitad de la frente.

    [Ampliación con IA] Versión 2: las orejas tienen un degradado (punta más
    oscura) y un pelito blanco en la base, la nariz tiene brillo, los bigotes
    son más finos y semitransparentes, y hay coloretes difuminados.
    """
    c = Lienzo(640, 800)
    pelo = c.degradado(10, 230, (68, 46, 34), (156, 112, 78))
    rosa = c.degradado(70, 200, (226, 128, 150), (248, 186, 196))

    # Orejas por fuera
    c.pintar(c.triangulo((40, 230), (110, 10), (250, 150)), pelo)
    c.pintar(c.triangulo((600, 230), (530, 10), (390, 150)), pelo)
    # Parte rosa de dentro
    c.pintar(c.triangulo((85, 200), (118, 70), (205, 150)), rosa)
    c.pintar(c.triangulo((555, 200), (522, 70), (435, 150)), rosa)
    # Pelitos blancos saliendo de la base de la oreja
    for (x1, y1, x2, y2) in ((110, 196, 128, 150), (135, 186, 146, 140), (160, 176, 165, 136),
                             (530, 196, 512, 150), (505, 186, 494, 140), (480, 176, 475, 136)):
        c.pintar(c.linea(x1, y1, x2, y2, 4), (250, 244, 236), opacidad=0.85)

    # Coloretes difuminados en las mejillas
    for x in (175, 465):
        c.pintar(c.elipse_suave(x, 665, 62, 34, 1.0), (240, 120, 140), opacidad=0.45)

    # Nariz: triángulo redondeado con un brillo arriba
    nariz = c.triangulo((288, 688), (352, 688), (320, 728)) | c.elipse(320, 690, 32, 10)
    c.pintar(nariz, c.degradado(680, 728, (236, 128, 152), (196, 78, 108)))
    c.pintar(c.elipse_suave(310, 690, 12, 5, 0.8), (255, 255, 255), opacidad=0.6)
    c.pintar(c.linea(320, 728, 320, 752, 5), (70, 46, 36))
    c.pintar(c.arco(304, 752, 16, 10, 4, 10, 120), (70, 46, 36))
    c.pintar(c.arco(336, 752, 16, 10, 4, 60, 170), (70, 46, 36))

    # Bigotes, tres por lado, finos y un poco transparentes
    for dy, inclinacion in ((-10, -18), (8, 0), (26, 18)):
        c.pintar(c.linea(272, 720 + dy, 104, 710 + dy + inclinacion, 3.5), (40, 30, 25), opacidad=0.85)
        c.pintar(c.linea(368, 720 + dy, 536, 710 + dy + inclinacion, 3.5), (40, 30, 25), opacidad=0.85)
    return c


def mascara():
    """Antifaz de carnaval. Ojos (agujeros) en (180, 130) y (460, 130).

    [Ampliación con IA] Versión 2: el morado lleva un degradado desde el
    centro, el ribete dorado tiene brillo, hay filigranas sobre los ojos,
    lentejuelas y una pluma a un lado (el carnaval de Las Palmas no se
    entiende sin plumas).
    """
    c = Lienzo(640, 260)
    oy = 130
    oro_claro, oro, oro_oscuro = (250, 226, 150), (222, 176, 72), (150, 108, 36)

    # Pluma (va primero para que quede por detrás del antifaz)
    pluma = c.degradado(0, 0, (40, 150, 150), (20, 70, 110), x0=560, x1=640)
    c.pintar(c.elipse_girada(588, 84, 55, 17, -62), pluma)
    c.pintar(c.linea(566, 128, 610, 38, 3), oro_claro, opacidad=0.9)
    for k in range(5):
        y = 116 - k * 15
        x = 566 + (128 - y) * 0.49
        c.pintar(c.linea(x, y, x + 22, y - 10, 2), (180, 230, 225), opacidad=0.5)

    # Forma del antifaz: dos elipses grandes unidas por el centro
    forma = c.elipse(OJO_IZQ_X, oy, 150, 95) | c.elipse(OJO_DER_X, oy, 150, 95) | c.elipse(320, oy - 15, 110, 60)
    c.pintar(forma, c.degradado_radial(320, oy - 20, 330, (150, 66, 182), (62, 18, 92)))
    # Un brillo suave arriba para que parezca satinado
    c.pintar(c.elipse_suave(320, oy - 70, 220, 40, 1.0) * forma, (255, 255, 255), opacidad=0.16)

    # Ribete dorado por fuera, con su brillo
    borde = (c.elipse(OJO_IZQ_X, oy, 150, 95) & ~c.elipse(OJO_IZQ_X, oy, 141, 86)) | \
            (c.elipse(OJO_DER_X, oy, 150, 95) & ~c.elipse(OJO_DER_X, oy, 141, 86))
    borde &= ~(c.elipse(320, oy - 15, 100, 50))
    c.pintar(borde, c.degradado(oy - 95, oy + 95, oro_claro, oro_oscuro))

    # Filigranas: arcos dorados finos encima de cada ojo
    for ox, sentido in ((OJO_IZQ_X, -1), (OJO_DER_X, 1)):
        c.pintar(c.arco(ox, oy, 96, 66, 4, 200, 340), oro, opacidad=0.9)
        c.pintar(c.arco(ox + sentido * 70, oy - 40, 28, 20, 3, 180, 360), oro, opacidad=0.8)
        c.pintar(c.elipse(ox + sentido * 98, oy - 40, 6, 6), oro_claro)

    # Lentejuelas: puntos dorados con un brillo blanco
    for x, y in ((320, 70), (292, 84), (348, 84), (320, 98), (60, 120), (580, 140), (110, 190), (530, 190)):
        c.pintar(c.elipse(x, y, 8, 8), oro)
        c.pintar(c.elipse(x - 2, y - 2, 3, 3), (255, 255, 255), opacidad=0.8)

    # Agujeros de los ojos (transparentes) con su ribete
    for ox in (OJO_IZQ_X, OJO_DER_X):
        c.borrar(c.elipse(ox, oy, 62, 40))
        c.pintar(c.anillo(ox, oy, 66, 44, 6), c.degradado(oy - 44, oy + 44, oro_claro, oro_oscuro))
    return c


if __name__ == "__main__":
    os.makedirs(SALIDA, exist_ok=True)
    for nombre, funcion in (("gafas", gafas), ("orejas", orejas), ("mascara", mascara)):
        ruta = os.path.join(SALIDA, nombre + ".png")
        funcion().guardar_png(ruta)
        print("Generado:", ruta)
