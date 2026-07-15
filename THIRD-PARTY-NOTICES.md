# Avisos de terceros

Inventario de dependencias de terceros (constitución §4). Todas tienen licencia permisiva
(MIT / Apache-2.0); no se usa ninguna dependencia con licencia copyleft.

Antes de añadir una dependencia nueva hay que verificar su licencia y registrarla aquí.

| Dependencia | Uso en el proyecto | Licencia | Titular |
|---|---|---|---|
| [Microsoft.Maui.Controls](https://www.nuget.org/packages/Microsoft.Maui.Controls) | Framework de interfaz y APIs de plataforma (`Launcher`, `Share`, `Preferences`, `Email`, `Browser`, `Clipboard`) | MIT | Microsoft Corporation |
| [Microsoft.Extensions.Logging.Debug](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Debug) | Trazas de depuración, solo en configuración Debug | MIT | .NET Foundation y colaboradores |
| [Xamarin.AndroidX.Core](https://www.nuget.org/packages/Xamarin.AndroidX.Core) | `ViewCompat` y `WindowCompat` para aplicar los *insets* de las barras del sistema en `MainActivity`. Llega como dependencia transitiva de .NET MAUI | Apache-2.0 | The Android Open Source Project / .NET for Android |

## APIs del sistema operativo

El uso de APIs de Android (`Android.OS.Environment`, `Android.Provider.Settings`,
`Android.Widget.Toast`) no contamina la licencia del proyecto (constitución §4).

## Código propio

El resto del código es de desarrollo propio y se publica bajo la licencia MIT del proyecto
(ver [LICENSE](LICENSE)). No se ha forkeado ni vendorizado código de terceros.
