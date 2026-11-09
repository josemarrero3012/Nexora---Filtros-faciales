"""
generar_filtros.py
==================

Script de apoyo que usamos para dibujar los PNG de los filtros (gafas, orejas
y máscara) en vez de bajarnos imágenes de internet con derechos de autor.

Solo necesita Python 3 y numpy. Se lanza desde la raíz del repositorio:

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
        """Pinta encima de lo que ya hay usando la fórmula de mezcla "over"."""
        a = self._cobertura(mascara_grande) * opacidad
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
    """Gafas de sol redondas. Ojos en (180, 110) y (460, 110)."""
    c = Lienzo(640, 220)
    oy = 110
    # Cristales oscuros semitransparentes
    for ox in (OJO_IZQ_X, OJO_DER_X):
        c.pintar(c.elipse(ox, oy, 105, 92), (25, 30, 45), opacidad=0.78)
    # Un brillito en cada cristal para que no parezcan dos agujeros negros
    for ox in (OJO_IZQ_X, OJO_DER_X):
        c.pintar(c.linea(ox - 55, oy - 35, ox - 20, oy - 62, 12), (255, 255, 255), opacidad=0.55)
    # Montura
    for ox in (OJO_IZQ_X, OJO_DER_X):
        c.pintar(c.anillo(ox, oy, 108, 95, 13), (15, 15, 15))
    # Puente entre los dos cristales
    c.pintar(c.linea(OJO_IZQ_X + 100, oy - 30, OJO_DER_X - 100, oy - 30, 13), (15, 15, 15))
    # Patillas cortas hacia los lados
    c.pintar(c.linea(OJO_IZQ_X - 106, oy - 25, 8, oy - 40, 13), (15, 15, 15))
    c.pintar(c.linea(OJO_DER_X + 106, oy - 25, 632, oy - 40, 13), (15, 15, 15))
    return c


def orejas():
    """Filtro de gato: orejas, nariz y bigotes. Ojos en (180, 540) y (460, 540).

    Las orejas van bastante por encima de los ojos (unos 310 px) para que
    queden encima de la cabeza y no en mitad de la frente.
    """
    c = Lienzo(640, 800)
    marron = (120, 80, 55)
    rosa = (240, 150, 170)
    # Orejas por fuera
    c.pintar(c.triangulo((40, 230), (110, 10), (250, 150)), marron)
    c.pintar(c.triangulo((600, 230), (530, 10), (390, 150)), marron)
    # Parte rosa de dentro
    c.pintar(c.triangulo((85, 200), (118, 70), (205, 150)), rosa)
    c.pintar(c.triangulo((555, 200), (522, 70), (435, 150)), rosa)
    # Nariz (debajo de los ojos, en el centro)
    c.pintar(c.triangulo((285, 690), (355, 690), (320, 730)), (230, 100, 130))
    c.pintar(c.linea(320, 730, 320, 755, 6), (60, 40, 30))
    # Bigotes, tres por lado
    for dy, inclinacion in ((-10, -18), (8, 0), (26, 18)):
        c.pintar(c.linea(275, 720 + dy, 110, 710 + dy + inclinacion, 5), (40, 30, 25))
        c.pintar(c.linea(365, 720 + dy, 530, 710 + dy + inclinacion, 5), (40, 30, 25))
    return c


def mascara():
    """Antifaz de carnaval. Ojos (agujeros) en (180, 130) y (460, 130)."""
    c = Lienzo(640, 260)
    oy = 130
    morado = (110, 40, 150)
    dorado = (235, 190, 70)
    # Forma del antifaz: dos elipses grandes unidas por el centro
    forma = c.elipse(OJO_IZQ_X, oy, 150, 95) | c.elipse(OJO_DER_X, oy, 150, 95) | c.elipse(320, oy - 15, 110, 60)
    c.pintar(forma, morado)
    # Ribete dorado por fuera
    borde = (c.elipse(OJO_IZQ_X, oy, 150, 95) & ~c.elipse(OJO_IZQ_X, oy, 141, 86)) | \
            (c.elipse(OJO_DER_X, oy, 150, 95) & ~c.elipse(OJO_DER_X, oy, 141, 86))
    # Solo dejamos el ribete que queda por fuera de la otra elipse
    borde &= ~(c.elipse(320, oy - 15, 100, 50))
    c.pintar(borde, dorado)
    # Puntitos dorados decorativos
    for x, y in ((320, 70), (290, 85), (350, 85), (60, 120), (580, 120)):
        c.pintar(c.elipse(x, y, 9, 9), dorado)
    # Agujeros de los ojos (transparentes)
    for ox in (OJO_IZQ_X, OJO_DER_X):
        c.borrar(c.elipse(ox, oy, 62, 40))
        c.pintar(c.anillo(ox, oy, 66, 44, 6), dorado)
    return c


if __name__ == "__main__":
    os.makedirs(SALIDA, exist_ok=True)
    for nombre, funcion in (("gafas", gafas), ("orejas", orejas), ("mascara", mascara)):
        ruta = os.path.join(SALIDA, nombre + ".png")
        funcion().guardar_png(ruta)
        print("Generado:", ruta)
