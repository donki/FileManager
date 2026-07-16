# Alta en Google Play Console — File Manager

Datos para dar de alta la aplicación en Play Console (constitución §A.5 y §A.6). El alta inicial es
manual: la Play Developer API no permite crear aplicaciones, solo subir builds a una app existente.

## 1. Crear la aplicación

**Play Console → Todas las aplicaciones → Crear aplicación**

| Campo | Valor | Notas |
|---|---|---|
| Nombre de la aplicación | `File Manager` | Máx. 30 caracteres. Visible en la ficha. |
| Idioma predeterminado | Inglés (Estados Unidos) | Coherente con la app: si el idioma del sistema no está soportado, usa inglés. |
| Aplicación o juego | Aplicación | |
| Gratuita o de pago | Gratuita | **Irreversible**: una app gratuita no puede pasar a de pago. |

El identificador de paquete **`com.socratic.filemanager`** no se elige en este formulario: queda
fijado por el primer artefacto que se sube. Es permanente (§A.2) — comprobar que el AAB lleva
exactamente ese ApplicationId antes de subirlo.

## 2. Ficha de Play Store

### Descripción breve (máx. 80 caracteres)

- **EN:** `Browse, copy, move and share your files. Fast, offline, no ads, no tracking.`
- **ES:** `Explora, copia, mueve y comparte tus ficheros. Rápido, sin conexión ni anuncios.`

### Descripción completa (máx. 4000 caracteres)

**EN**
```
File Manager lets you browse, organise and share the files on your device. It works fully
offline: no data ever leaves your phone.

BROWSE
• Folder navigation with a tappable breadcrumb path
• The Android back button goes up one folder instead of closing the app
• Icons by content type: image, video, audio, document, archive, APK…
• Date, size and item count for every entry
• Show or hide hidden files

MANAGE
• Create folder, rename, copy, move, delete
• File clipboard: copy in one folder, paste in another
• Conflict resolution when pasting: replace or keep both
• Open files with the system's associated app
• Share files with any app
• Details: name, path, MIME type, size and modification date

SEARCH
• Search by name in the current folder and its subfolders
• Sort by name, date or size, ascending or descending

INTERFACE
• Card-based design with automatic light and dark themes
• Adapted to the edge-to-edge mode of Android 15+

LANGUAGES
• English and Spanish. Follows the system language by default.

PRIVACY
• No internet permission, no servers, no analytics, no ads.
• Open source under the MIT licence.
```

**ES**
```
File Manager te permite explorar, organizar y compartir los ficheros de tu dispositivo.
Funciona totalmente sin conexión: ningún dato sale del teléfono.

EXPLORACIÓN
• Navegación por carpetas con ruta de navegación pulsable
• El botón atrás de Android sube una carpeta en lugar de cerrar la aplicación
• Iconos por tipo de contenido: imagen, vídeo, audio, documento, comprimido, APK…
• Fecha, tamaño y número de elementos de cada entrada
• Mostrar u ocultar ficheros ocultos

GESTIÓN
• Crear carpeta, renombrar, copiar, mover, eliminar
• Portapapeles de ficheros: copiar en una carpeta y pegar en otra
• Resolución de conflictos al pegar: reemplazar o conservar ambos
• Abrir ficheros con la aplicación asociada del sistema
• Compartir ficheros con cualquier aplicación
• Detalles: nombre, ruta, tipo MIME, tamaño y fecha de modificación

BÚSQUEDA
• Búsqueda por nombre en la carpeta actual y sus subcarpetas
• Ordenación por nombre, fecha o tamaño, ascendente y descendente

INTERFAZ
• Diseño con tarjetas y tema claro y oscuro automático
• Adaptada al modo edge-to-edge de Android 15+

IDIOMAS
• Castellano e inglés. Por defecto sigue el idioma del sistema.

PRIVACIDAD
• Sin permiso de internet, sin servidores, sin estadísticas, sin anuncios.
• Código abierto con licencia MIT.
```

### Recursos gráficos (pendientes de generar)

| Recurso | Requisito | Estado |
|---|---|---|
| Icono de la app | PNG 512×512, 32 bits, ≤1 MB | Derivar de `Resources/AppIcon/appicon.svg` |
| Gráfico de funciones | PNG/JPG 1024×500 | Pendiente |
| Capturas de teléfono | Mín. 2, entre 320 px y 3840 px de lado | Pendiente — capturar en el Redmi |

Las capturas deben cubrir tema claro y oscuro y los dos idiomas (§A.8.2).

| Otros campos | Valor |
|---|---|
| Categoría | Herramientas |
| Etiquetas | Gestor de ficheros, Explorador |
| Correo de contacto | *(pendiente de decidir)* |
| Política de privacidad | **URL obligatoria — bloqueante, ver §5** |

## 3. Declaración de permiso: acceso a todos los ficheros

`MANAGE_EXTERNAL_STORAGE` obliga a rellenar el formulario **Política → Contenido de la aplicación →
Permiso de acceso a todos los ficheros**. Es el punto de mayor riesgo de rechazo.

- **Función principal declarada:** gestor de ficheros.
- **Justificación:** la app explora y gestiona el sistema de ficheros completo del dispositivo
  (copiar, mover, renombrar, eliminar, comprimir, compartir). Es el caso de uso que Google Play
  admite explícitamente para este permiso.
- **Por qué no basta el Storage Access Framework:** el SAF concede acceso carpeta a carpeta y no
  permite ver el sistema de ficheros completo, que es justamente la función principal. Decisión
  explícita del propietario, documentada en el README.
- **Vídeo de demostración:** Google exige un enlace (normalmente YouTube, no listado) mostrando el
  flujo real que necesita el permiso. **Pendiente de grabar.**

La revisión de esta declaración puede retrasar la publicación (§A.5).

## 4. Seguridad de los datos

La app no accede a la red (no declara `INTERNET`), no envía datos a ningún servidor y no recoge
estadísticas de uso.

| Pregunta | Respuesta |
|---|---|
| ¿Recopila o comparte datos de usuario? | No |
| ¿Cifra los datos en tránsito? | No aplica (no hay tránsito) |
| ¿Tiene anuncios? | No |
| ¿Contenido generado por usuarios? | No |
| Clasificación de contenido | Cuestionario → previsión: para todos los públicos |
| Público objetivo | *(pendiente de decidir)* |

## 5. Bloqueantes antes de poder publicar

1. **URL de política de privacidad.** Play la exige a todas las aplicaciones, incluso a las que no
   recopilan datos. Sin ella no se puede enviar la ficha. Opción sencilla: publicarla en GitHub
   Pages sobre este mismo repositorio.
2. **Keystore de firma.** No existe `socratic.keystore` en el repositorio y `build_and_sign.ps1`
   falla sin él. Crear una vez y guardar la copia de seguridad fuera del repo (§5):
   ```
   keytool -genkeypair -v -keystore socratic.keystore -alias socratic -keyalg RSA -keysize 2048 -validity 10000
   ```
   Perder este keystore impide publicar actualizaciones de la app para siempre.
3. **Vídeo de demostración** del acceso a todos los ficheros (§3).
4. **Capturas y gráfico de funciones** (§2).
5. **Correo de contacto** público para la ficha.

## 6. Primer envío

Canal obligatorio: **pruebas cerradas** (§A.5). No pasar a producción sin validar ahí.

Al publicar, fijar la versión a la fecha del día (§A.4):
`ApplicationDisplayVersion = AAAA.MM.DD.0` y `ApplicationVersion = AAAAMMDD0`.
