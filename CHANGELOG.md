# Changelog

Todos los cambios relevantes de este proyecto se registran en este fichero (constitución §6 y §9).

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/).

## [2026.11.27.0] — 2026-11-27

### Añadido
- **Miniaturas de fotos y vídeos**: en la lista y en la franja de «Últimos ficheros», las fotos
  (jpg, png, webp, heic, gif, bmp) y los vídeos (mp4, mkv, webm, 3gp, mov, avi…) enseñan una
  miniatura en vez del icono genérico, en un cuadrado redondeado; los vídeos llevan encima una marca
  de reproducir. Mientras se hace, o si el formato no se puede leer, se ve el icono de siempre. Vale
  también en la tarjeta SD.
- Interruptor **«Mostrar miniaturas»** en Configuración › Visualización (activado por defecto).
- Por dentro: las miniaturas (128 px) se hacen fuera del hilo de la interfaz, de dos en dos y la más
  reciente primero; se piden cuando una fila aparece y se sueltan cuando se recicla al desplazar, con
  un tope de pendientes. Se guardan en la caché de la app (clave: ruta, tamaño, fecha de modificación
  y lado; tope de 2000 ficheros) y en memoria solo se recuerda la ruta de cada una (tope de 512).
  Android 10 y siguientes usan `ThumbnailUtils` con tamaño y cancelación; Android 7 a 9,
  `BitmapFactory` reducido con `inSampleSize` y girado según el EXIF.
- Pruebas: 401 (antes 340): qué ficheros llevan miniatura, clave de caché, reducción y giro, caché en
  memoria y en disco, cola con tope y cancelación, fallos (se queda el icono), interruptor, y las
  pantallas (lista, recientes y Configuración).

### Cambiado
- **Cobertura sobre toda la app: 91,9 %** (antes 93,8 %). Baja porque el generador de miniaturas de
  Android (`Platforms/Android/ThumbnailGenerator.cs`, ~70 líneas estimadas) no se puede ejecutar en el
  banco; todo lo nuevo que no es de Android está al 100 % (cálculos de reducción y giro sacados a
  `Helpers/Thumbnails.cs` para poder probarlos). Sigue por encima del mínimo del 90 % (General §8.6).
- Pruebas: 340 (antes 291). Cobertura de toda la app con la medida común de General §8.6 (solo
  código ejecutable): **93,8 %** (antes 79,1 %). El banco compila ahora las páginas, el Shell, la App
  y el arranque, y prueba los adaptadores de MAUI con dobles; `cobertura.ps1` usa el script común.
- Por dentro, sin cambio visible: la interpretación de la lista de memorias (tarjeta SD) sale del
  bloque de Android a `FileSystemService.BuildVolumes`; `ItemTouchBehavior` separa la emisión de los
  gestos del enganche nativo; abrir/compartir, navegador y correo reciben sus servicios de MAUI por
  constructor; el menú lateral toma la versión de `IAppEnvironment` y la pantalla principal repinta
  la barra de pegar y los textos tras cambiar de idioma con su `Dispatcher` en vez de `MainThread`.

## [2026.11.24.1] — 2026-11-24

### Corregido
- Fuera de la raíz quedaba un hueco vacío encima de la lista: la franja de «Últimos ficheros»
  estaba oculta pero Android le guardaba el alto. Ahora se quita la cabecera entera.

## [2026.11.24.0] — 2026-11-24

### Añadido
- **Tarjeta SD**: con una tarjeta SD (u otra memoria extraíble) metida, aparece un botón de icono
  en la barra de arriba para cambiar entre «Almacenamiento interno» y «Tarjeta SD». Se puede
  explorar, copiar y mover entre las dos (mover entre memorias copia y borra el original). El
  título y la ruta de migas empiezan por el nombre de la memoria; en la raíz de la tarjeta, atrás
  vuelve a la memoria interna. Si se saca la tarjeta estando dentro, se vuelve a la interna.
- Pruebas: 291 (antes 285).

### Añadido
- **Compartir ficheros y carpetas**: «Compartir» está ahora también en el menú de las carpetas, en las
  acciones de la selección múltiple y como botón de icono en la barra de selección. Uno o varios
  elementos a la vez abren el selector de compartir de Android (ACTION_SEND / ACTION_SEND_MULTIPLE
  con URIs `content://` del FileProvider, sin copiar el fichero) con el tipo MIME común de todos
  (`image/*` si son imágenes distintas, `*/*` si se mezclan). Las carpetas se envían comprimidas en
  un `.zip` temporal en la caché de la aplicación, que se borra al compartir la siguiente vez.
- Pruebas: 285 (antes 269).

### Cambiado
- «Seleccionar todo» de la barra de selección pasa a botón de icono (con texto dejaba cortado el
  contador de seleccionados).

## [2026.10.16.1] — 2026-10-16

### Añadido
- **Últimos ficheros**: en la raíz, arriba de la lista, una franja horizontal con los 12 ficheros
  modificados más recientemente de todo el almacenamiento (icono, nombre en dos líneas, fecha y carpeta). Un toque
  abre el fichero; una pulsación larga lleva a su carpeta. Se carga en segundo plano sin frenar el
  listado, respeta «Mostrar ocultos» y se esconde en las subcarpetas, la búsqueda y la selección.
- Pruebas: 269 (antes 260).

## [2026.10.01.0] — 2026-10-01

### Cambiado
- **La lógica de las pantallas sale a clases que se pueden probar** (General §8.6): `ViewModels/`
  (`MainViewModel`, `SettingsViewModel`, `AboutViewModel`) tiene todo lo que hacían las páginas
  (carpeta actual, listado, filtro, búsqueda, selección múltiple, portapapeles, menús, idioma, permiso
  y contacto) y las páginas quedan como enlace fino que vuelca su estado en los controles. Los
  diálogos (`IDialogService`) y lo del sistema (`IAppEnvironment`: versión, navegador y correo) van
  detrás de interfaces. La comprobación de versión recibe el cliente HTTP y se prueba sin red. El
  usuario no ve ningún cambio.
- **Pruebas: 260** (antes 142). Cobertura sobre toda la app: **65,6 %** (antes 36,6 % con la medida
  corregida; ver abajo); de lo instrumentado, 99,4 %.
- **Medida de la cobertura corregida**: el `coverlet.runsettings` excluía `CompilerGeneratedAttribute`,
  que deja fuera el cuerpo de todos los métodos `async` y de las lambdas (sus máquinas de estado son
  código generado). Ahora solo se excluye `GeneratedCodeAttribute`. Con la medida vieja la versión
  anterior daba 36,2 %; con la nueva, 36,6 %.

### English
- Screen logic moved out of the pages into testable view-models (`ViewModels/`); dialogs and system
  services behind interfaces. No visible change.
- 260 automated tests (was 142); whole-app line coverage 65.6 % (was 36.6 %).
- Coverage settings no longer exclude compiler-generated code, which hid every `async` method body.

## [2026.09.30.0] — 2026-09-30

### Corregido
- **Pegar con «Reemplazar» ya no puede borrar lo que se pega.** Al pegar una carpeta en la carpeta
  que la contiene dos niveles arriba (por ejemplo `p/x/x` en `p`, donde ya existe `p/x`), reemplazar
  borraba `p/x` —y con ella el origen— antes de moverla. Ahora se rechaza ese elemento con un error y
  no se pierde nada.
- Al copiar una carpeta con un punto en el nombre dentro de su misma carpeta, la copia se llama
  `release.v2 (2)` y no `release (2).v2`.

### Añadido
- **Pruebas automatizadas** (General §8.6): proyecto `FileManager.Tests` (xUnit) con la lógica de
  ficheros, ordenación, búsqueda, validación de nombres, portapapeles, tamaños, tipos MIME, iconos,
  preferencias y traducciones. Se ejecutan con `dotnet test FileManager.Tests`.

### English
- Paste with «Replace» can no longer delete what is being pasted when the target folder contains it.
- Copying a folder whose name has a dot into its own folder now names it `release.v2 (2)`.
- Automated tests for the app logic (`dotnet test FileManager.Tests`).

## [2026.09.28.0] — 2026-09-28

### Añadido
- **Gestor global de excepciones** (General §6.12) con la pieza común `Mobile/Shared/CrashGuard.cs`:
  un error inesperado ya no cierra la aplicación; se registra con su traza en `crash.log` y se
  avisa en el idioma elegido en la app (es/en).

### Cambiado
- Fuera los emoji de la interfaz (General §6.2): los títulos de Configuración (Idioma,
  Visualización, Almacenamiento) y de Acerca de (Contacto, Idioma, Privacidad, Licencia, Aviso
  legal) llevan iconos planos SVG; los botones de idioma, la bandera dibujada (`ic_flag_es`,
  `ic_flag_us`) en vez del emoji; «Acerca de», «Volver», el aviso de riesgo y el cierre de la
  selección, su icono; y los estados vacíos (carpeta vacía, búsqueda sin resultados, sin permiso)
  un dibujo plano en vez del emoji.

### English
- Global exception handler: an unexpected error no longer closes the app; it is logged and you
  are told in the app's language.
- No more emoji in the interface: flat SVG icons in Settings and About, drawn flags on the
  language buttons, and flat drawings in the empty states.

## [2026.09.26.0] — 2026-09-26

### Corregido
- El botón de menú de la barra superior no abría nada: la aplicación arrancaba con un
  `NavigationPage` y no había menú lateral. Ahora arranca con un Shell con **menú hamburguesa**
  (constitución A.9): **Inicio**, **Configuración** y **Acerca de**, con la versión al pie. El menú
  «⋮» sigue abriendo Configuración y Acerca de como antes. La cabecera del menú se desplaza con
  la lista: con el móvil en horizontal y la letra grande tapaba «Acerca de».
- En Android 16 el botón de atrás salía de la aplicación desde una subcarpeta: con targetSdk 36
  entra el «atrás predictivo» y el botón no llegaba a la página. Se desactiva
  (`enableOnBackInvokedCallback="false"`) y vuelve a subir de carpeta.
- Atrás en Configuración o Acerca de abiertas desde el menú lateral vuelve a Inicio; en la raíz
  de Inicio, la aplicación se oculta (Mobile §7). Probado en el Xiaomi con la letra al 145 %.

## [2026.08.28.0] — 2026-08-28

`versionCode`: 202608280

### Corregido
- La **barra de selección múltiple ya no tapa el primer elemento**. Estaba superpuesta sobre la
  lista (`VerticalOptions="Start"` dentro del mismo `Grid`), justo encima de la primera fila, que
  suele ser la que se acaba de marcar con la pulsación larga. Ahora ocupa su propia fila del
  `Grid` de la página y empuja la lista hacia abajo.

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
