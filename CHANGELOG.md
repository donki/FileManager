# Changelog

Todos los cambios relevantes de este proyecto se registran en este fichero (constitución §6 y §9).

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/).

## [2026.08.01.0] — 2026-08-01

`versionCode`: 202608010

### Añadido
- **Pulsación larga sobre un elemento**: entra en el modo selección y deja marcado **ese**
  elemento (nota de autor del 2026-08-01). Antes solo se entraba desde la barra de herramientas.
  El gesto lo resuelve `Helpers\ItemTouchBehavior.cs` con `View.Click` y `View.LongClick` de
  Android: MAUI no trae pulsación larga, `PointerGestureRecognizer` no dispara `PointerPressed`
  con el dedo, y dejar el `TapGestureRecognizer` en la fila impide que salte la pulsación larga
  porque su detector consume el evento táctil antes. Verificado en el emulador.

### Corregido
- La aplicación **abortaba al arrancar**: `UpdateService` no estaba registrado en `MauiProgram` y
  `MainPage` lo pide por constructor. El registro se perdió en el incidente de reorganización.
- El proyecto **no compilaba**: el `.csproj` había perdido los ficheros compartidos
  (`..\Shared\ModernDialog.cs`, `..\Shared\AuthorNotes.cs`) y el `Import` de `signing.props`.
- Icono y splash usaban todavía el azul `#1E5C97` de la marca anterior en el `.csproj`, cuando los
  SVG ya eran del índigo unificado `#3525CD`.
- `Resources\AppIcon\play_store_icon.png` regenerado desde los SVG actuales: mostraba el diseño
  azul y naranja anterior al rediseño del 28-jul.

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
