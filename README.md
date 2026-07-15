# 📁 File Manager / Gestor de Ficheros

Gestor de ficheros para Android desarrollado en .NET MAUI. Explora, organiza y comparte los
ficheros del dispositivo. Funciona totalmente sin conexión: ningún dato sale del teléfono.

Aplicación desarrollada según la [constitución](https://github.com/donki/constitution), incluida
aquí como submódulo de solo lectura en [`constitution/`](constitution) (constitución §23). Para
clonar el proyecto con ella:

```bash
git clone --recurse-submodules https://github.com/donki/FileManager.git
```

## ✨ Características

### 📂 Exploración
- Navegación por carpetas con **ruta de navegación** (breadcrumb) pulsable
- **Botón atrás** de Android sube una carpeta en lugar de cerrar la aplicación
- Iconos por tipo de contenido (imagen, vídeo, audio, documento, comprimido, APK…)
- Fecha, tamaño y número de elementos de cada entrada
- Mostrar u ocultar ficheros ocultos

### 🛠️ Gestión
- **Crear carpeta**, **renombrar**, **copiar**, **mover**, **eliminar**
- Portapapeles de ficheros: copiar en una carpeta y pegar en otra
- Resolución de conflictos al pegar: **reemplazar** o **conservar ambos**
- **Abrir** ficheros con la aplicación asociada del sistema
- **Compartir** ficheros con cualquier aplicación
- **Detalles**: nombre, ruta, tipo MIME, tamaño y fecha de modificación

### 🔍 Búsqueda
- Búsqueda por nombre dentro de la carpeta actual y sus subcarpetas
- Ordenación por nombre, fecha o tamaño (ascendente y descendente)

### 🌐 Idiomas
- Castellano e inglés
- Por defecto sigue el **idioma del sistema**; si no está soportado, usa **inglés**
- Se puede forzar el idioma desde Configuración

### 🎨 Interfaz
- Diseño con tarjetas, tema **claro y oscuro** automático
- Adaptada al modo *edge-to-edge* de Android 15+

## 🚀 Instalación

### Requisitos
- Android 7.0 (API 24) o superior
- .NET 9.0 SDK y carga de trabajo MAUI (`dotnet workload install maui`)

### Desde código fuente
```bash
dotnet restore
dotnet build -t:Run -f net9.0-android36.0
```

### En el emulador MuMu (validación rápida, constitución §A.8.1)
```powershell
./install_mumu.ps1 -BuildFirst -Launch
```
MuMu es Android 12 (API 32) y no incluye `appops`, así que el acceso a todos los ficheros se
concede desde la propia app, que es justo el flujo que conviene validar. No sustituye a la prueba
en dispositivo real: no cubre el modo *edge-to-edge* obligatorio de Android 15+.

## 🔒 Permisos y privacidad

| Permiso | Justificación |
|---|---|
| `MANAGE_EXTERNAL_STORAGE` | Función principal de la aplicación: explorar y gestionar los ficheros del dispositivo. Es el caso de uso que Google Play admite explícitamente para este permiso. |
| `READ_EXTERNAL_STORAGE` / `WRITE_EXTERNAL_STORAGE` | Solo hasta Android 10 (`maxSdkVersion="29"`), donde `MANAGE_EXTERNAL_STORAGE` todavía no existe. |

**Nota sobre el mínimo privilegio (constitución §3, §6 y A.3).** La constitución pide preferir las
APIs que no exigen permiso, y en concreto cita el *Storage Access Framework*. Aquí se ha descartado
por decisión explícita y documentada del propietario: el SAF obliga al usuario a conceder cada
carpeta por separado y no permite ver el sistema de ficheros completo, que es justamente la función
principal de esta aplicación. `MANAGE_EXTERNAL_STORAGE` es la excepción que Google Play admite para
este caso de uso.

Consecuencias en publicación:
- Requiere **declaración de permiso** en Play Console explicando la función principal.
- La revisión de esa declaración puede retrasar la publicación.
- Sin este acceso la aplicación muestra una pantalla que lleva a los ajustes del sistema; no falla en silencio.

La aplicación **no accede a la red**: no declara el permiso `INTERNET`, no envía datos a ningún servidor
y no recoge estadísticas de uso.

## 🛠️ Desarrollo

### Tecnologías
- **.NET 9.0** y **.NET MAUI**
- **C#** con `Nullable` e `ImplicitUsings` habilitados
- **Android SDK 36**

### Estructura del proyecto
```
FileManager/
├── Pages/                 # Páginas de la aplicación (XAML + code-behind)
├── Services/              # Lógica de negocio e interfaces
├── Models/                # Entidades de datos
├── Helpers/               # Utilidades transversales (iconos, MIME, tamaños, DI)
├── Platforms/Android/     # Código nativo: permisos, insets, toasts, manifiesto
├── Resources/             # Iconos, splash, estilos
└── constitution/          # Submódulo: gobernanza canónica (solo lectura, §23)
```

### Arquitectura de presentación

Capa de presentación ligera, el enfoque por defecto de la constitución §7: **code-behind delgado que
delega en `Services/`**, sin ViewModels ni librerías de binding.

- **Toda** la lógica de negocio vive en `Services/` detrás de interfaces (`IFileSystemService`,
  `IFileClipboardService`, `ILocalizationService`, `ISettingsService`, `IStoragePermissionService`,
  `IFileActionsService`, `IToastService`).
- El code-behind solo orquesta: pide datos al servicio, los vuelca en los controles y muestra errores.
- Todos los servicios se registran e inyectan por dependencias en `MauiProgram.cs`.
- El código específico de Android está encapsulado en `Platforms/Android`.

### Localización

Los textos están centralizados en `Services/LocalizationService.cs` (constitución §8). Cada página
vuelca los textos en un método `ApplyTexts()` que se llama en `OnAppearing` y al cambiar el idioma.
Las fechas, números y tamaños se formatean con `ILocalizationService.CurrentCulture`.

Para añadir un idioma: añadir el diccionario en `LocalizationService`, incluir el código en
`IsSupported`, y añadir la opción en `SettingsPage.LanguageCodes` y en el `Picker`.

## 📦 Publicación

Flujo estándar de la constitución (§A.6):

1. Actualizar `ApplicationDisplayVersion` y `ApplicationVersion` en `FileManager.csproj`.
2. Actualizar `CHANGELOG.md`.
3. Ejecutar `./build_and_sign.ps1` (genera APK y AAB firmados).
4. Validar el AAB en dispositivo real.
5. Subir a **pruebas cerradas** (canal obligatorio antes de producción, §A.5).
6. Verificar en Play Console: versionCode incremental, canal, icono, idioma por defecto y metadata.
7. Registrar la publicación (tag de versión).

## 🐛 Solución de problemas

**No se ve ningún fichero.** Comprueba en Configuración → Almacenamiento que el acceso a todos los
ficheros está concedido. Android no lo concede con el diálogo normal: hay que activarlo en los ajustes
del sistema.

**Una carpeta aparece vacía pero tiene contenido.** Puede ser `/Android/data` o `/Android/obb`, que
Android bloquea para todas las aplicaciones desde Android 11, incluso con `MANAGE_EXTERNAL_STORAGE`.

**Un fichero no se abre.** Significa que ninguna aplicación instalada declara soportar ese tipo MIME.

## 📄 Licencia

MIT. Ver [LICENSE](LICENSE). Dependencias de terceros y sus licencias en
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## 👨‍💻 Autor

Socratic.
