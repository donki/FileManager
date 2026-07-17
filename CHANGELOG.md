# Changelog

Todos los cambios relevantes de este proyecto se registran en este fichero (constitución §6 y §9).

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/).

## [2026.07.17.0] — 2026-07-17

`versionCode`: 202607170

### Cambiado
- El botón de búsqueda de la barra superior usa un icono **flat de contorno** (`ic_search.svg`,
  trazo blanco sin relleno) en lugar del emoji 🔍, que la fuente del sistema dibujaba relleno y
  multicolor. Es el estilo de iconografía que fija la constitución (anexo A.9): iconos de acción
  vectoriales y de contorno, nunca emoji. El botón pasa de `Button` con `Text` a `ImageButton`.

### Gobernanza
- Submódulo `constitution` actualizado a `95e2b59` (constitución §23): incorpora la **sección 24
  (Sistema de Diseño Visual)** y el **anexo A.9**, con la directriz de iconografía flat de contorno.

## [2026.07.16.1] — 2026-07-16

`versionCode`: 202607161

### Añadido
- `Resources/AppIcon/play_store_icon.png`: icono de ficha de Play Console de 512×512, opaco,
  compuesto por `appicon.svg` (fondo `#1E5C97`) y `appiconfg.svg` (carpeta con documento). El icono
  de launcher que genera MAUI dentro del AAB no lo usa Play Console como icono de tienda: es un
  asset aparte de la ficha.

### Cambiado
- Versión y `versionCode` incrementados para poder subir un AAB nuevo: Play Console ya tenía
  202607160 y no admite reutilizar un `versionCode`. Sin cambios funcionales respecto a
  2026.07.16.0.

## [2026.07.16.0] — 2026-07-16

`versionCode`: 202607160

Versión preparada para el primer envío a Play Console. Sin cambios funcionales respecto a
2026.07.15.0: solo se fija la versión a la fecha de publicación (§A.4). Absorbe el
`2026.07.15.1` que llegó a estar en el `csproj` sin entrada propia en este fichero.

### Cambiado
- Versión y `versionCode` fijados a la fecha de publicación.

## [2026.07.15.0] — 2026-07-15

`versionCode`: 202607150

### Añadido
- Primera versión del gestor de ficheros.
- Exploración de carpetas con ruta de navegación pulsable y botón atrás que sube un nivel.
- Iconos por tipo de contenido y detalles de cada entrada (fecha, tamaño, número de elementos).
- Operaciones: crear carpeta, renombrar, copiar, mover, eliminar, abrir y compartir.
- Portapapeles de ficheros entre carpetas con resolución de conflictos (reemplazar / conservar ambos).
- Búsqueda por nombre en la carpeta actual y sus subcarpetas, limitada a 500 resultados.
- Ordenación por nombre, fecha y tamaño, ascendente y descendente.
- Pantalla de configuración: idioma, ficheros ocultos, confirmación de borrado y estado del permiso.
- Página Acerca de según la plantilla de `Transversaral Req.md`.
- Localización en castellano e inglés, siguiendo el idioma del sistema con inglés por defecto.
- Tema claro y oscuro automático.
- Icono y pantalla de inicio propios (carpeta con documento).

### Corregido
- La pantalla de permisos no desaparecía al volver de los ajustes del sistema con el acceso ya
  concedido: la comprobación estaba en `OnAppearing`, que Android no dispara al reanudar la
  ventana desde otra Activity. Ahora se reevalúa también en el evento `Resumed` de la ventana.
  Detectado al validar en MuMu el flujo real del permiso.

### Gobernanza
- Incorporada la constitución como submódulo de solo lectura en `constitution/`, anclado al commit
  `160c54c` de [donki/constitution](https://github.com/donki/constitution) (constitución §23).
  Antes de publicar hay que comprobar si el anclaje sigue al día (§17).

### Notas técnicas
- Permiso `MANAGE_EXTERNAL_STORAGE` con pantalla de solicitud propia; requiere declaración
  en Play Console. Justificación en el README.
- Soporte del modo *edge-to-edge* obligatorio desde Android 15 (insets aplicados en `MainActivity`).
- Movimiento entre volúmenes distintos resuelto con copia y borrado, ya que `Directory.Move`
  no funciona entre la memoria interna y la tarjeta SD.

### Validación realizada
Compilación Debug y Release sin errores ni advertencias.

AVD Android 14 (API 34), tema claro: arranque, pantalla de permisos, listado del almacenamiento
interno, menús, cambio de idioma en caliente a castellano (incluidos los formatos de fecha),
página Acerca de y creación de carpeta (comprobada en el sistema de ficheros, no solo en pantalla).

MuMu Player Android 12 (API 32), tema oscuro, build Release (§A.8.1): flujo real del permiso
`MANAGE_EXTERNAL_STORAGE` (pantalla del sistema por app, activación y detección al volver),
listado, navegación a subcarpeta y botón atrás.

### Pendiente antes de publicar
- Validación en dispositivo Android **real** y en Android 15+, donde el modo *edge-to-edge* es
  obligatorio y ninguno de los dos emuladores usados lo fuerza (constitución §A.8.1 y §19).
- Generación del keystore y primera ejecución de `build_and_sign.ps1`.
- Declaración de permiso y metadata de Play Store en castellano e inglés.
