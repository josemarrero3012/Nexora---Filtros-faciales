# Plantilla de pruebas · Nexora

Aquí apuntamos los resultados de las pruebas para la memoria. Son las tres cosas que dijimos en la propuesta: **fluidez**, **tiempo de respuesta** y **aciertos**.

## Condiciones de la prueba

Rellenar antes de empezar, porque los resultados cambian mucho según esto.

| Dato | Valor |
|---|---|
| Fecha | |
| Ordenador (procesador / RAM) | |
| Webcam (resolución) | 640 × 480 |
| Luz | habitación / aula / poca luz |
| Distancia a la cámara | |
| Ruido de fondo | silencio / algo de ruido / mucho ruido |
| Quién hace la prueba | |

---

## 1. Fluidez (imágenes por segundo)

Dejar el programa unos 30 segundos en cada caso y apuntar lo que pone en **Imágenes por segundo** y **Proceso por imagen**.

| Módulos encendidos | FPS | ms por imagen |
|---|---|---|
| Ninguno (solo cámara) | | |
| Solo filtros | | |
| Solo control gestual (mano) | | |
| Solo plan B (cabeza) | | |
| Filtros + mano + voz (los tres a la vez) | | |
| Solo filtros, con **Filtro realista** apagado | | |
| Solo filtros, con **Filtro realista** encendido | | |

> La sombra y el ajuste de luz (ampliación con IA) añaden algo de trabajo por imagen. Con las dos últimas filas se ve si se nota en los FPS.

## 2. Tiempo de respuesta

**Gestos**: el valor de **Respuesta del gesto** (desde que se empieza a ver el gesto hasta que se confirma). También sale en el historial entre paréntesis.
**Voz**: el valor de **Respuesta de la voz** (desde que terminamos de hablar hasta que llega la orden).

| Acción | Intento 1 | Intento 2 | Intento 3 | Intento 4 | Intento 5 | Media |
|---|---|---|---|---|---|---|
| Gesto Mover | | | | | | |
| Gesto Clic | | | | | | |
| Gesto Desplazar | | | | | | |
| Gesto Pausa | | | | | | |
| «siguiente filtro» | | | | | | |
| «activar control gestual» | | | | | | |

## 3. Aciertos

Repetir cada gesto y cada orden **10 veces** y contar cuántas sale bien. Apuntar también los fallos (qué hizo en vez de lo que tocaba). Con **Exportar CSV** se puede revisar luego el historial con calma.

### Gestos

| Gesto | Aciertos / 10 | Fallos (qué pasó) |
|---|---|---|
| Mover | | |
| Clic | | |
| Desplazar | | |
| Pausa | | |

### Órdenes de voz

| Orden | Aciertos / 10 | Confianza media | Fallos (qué pasó) |
|---|---|---|---|
| «activar control gestual» | | | |
| «siguiente filtro» | | | |
| «quitar filtro» | | | |
| «pausar control» | | | |
| «clic» | | | |
| «salir» + «sí» / «no» | | | |

### Filtros

| Situación | ¿Sigue a la cara? | ¿Se gira con la cabeza? | Observaciones |
|---|---|---|---|
| Cara de frente | | | |
| Cabeza inclinada | | | |
| Con gafas de verdad | | | |
| Poca luz | | | |
| Dos personas delante | | | |
| Tapar la cara un momento con la mano (¿parpadea el filtro?) | | | |
| Poca luz, con **Filtro realista** apagado y encendido | | | |

---

## 4. Ajustes del detector (elegir los valores con datos)

Se cambian en el panel **Ajustes para las pruebas** de la ventana, sin cerrar el programa.

### Factor de escala de la cara

| Factor | FPS | ¿Detecta la cara? (de 10 intentos) | Observaciones |
|---|---|---|---|
| 1,10 (clase) | | | |
| 1,20 | | | |
| 1,30 | | | |

### Vecinos mínimos

| Vecinos | Falsos positivos (caras que no son) | ¿Detecta con poca luz? | Observaciones |
|---|---|---|---|
| 2 | | | |
| 4 (clase) | | | |
| 6 | | | |

### Confianza mínima de la voz

| Mínimo | Órdenes buenas aceptadas / 10 | Órdenes «coladas» (ruido aceptado) | Observaciones |
|---|---|---|---|
| 50 % | | | |
| 60 % (propuesta) | | | |
| 70 % | | | |

### Zona muerta del cursor

| Zona muerta | ¿Tiembla el cursor? | ¿Cuesta hacer movimientos finos? |
|---|---|---|
| 0 px | | |
| 6 px (por defecto) | | |
| 15 px | | |

---

## Conclusiones

- Valores elegidos al final:
- Lo que mejor funciona:
- Lo que peor funciona y por qué:
- ¿Hizo falta el plan B?
