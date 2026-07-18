using System.Globalization;
using Microsoft.Extensions.Logging;

namespace FileManager.Services;

/// <inheritdoc cref="ILocalizationService"/>
public class LocalizationService : ILocalizationService
{
    public const string SystemLanguage = "";
    public const string DefaultLanguage = "en";

    private readonly ISettingsService _settings;
    private readonly ILogger<LocalizationService> _logger;
    private string _current = DefaultLanguage;

    public LocalizationService(ISettingsService settings, ILogger<LocalizationService> logger)
    {
        _settings = settings;
        _logger = logger;
        SetLanguage(_settings.Language);
    }

    public event EventHandler? LanguageChanged;

    public string CurrentLanguage => _current;

    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.GetCultureInfo(DefaultLanguage);

    public string this[string key]
    {
        get
        {
            var table = _current == "es" ? Spanish : English;
            if (table.TryGetValue(key, out var value))
                return value;

            if (English.TryGetValue(key, out var fallback))
            {
                _logger.LogWarning("Missing {Language} translation for key {Key}", _current, key);
                return fallback;
            }

            _logger.LogWarning("Unknown translation key {Key}", key);
            return key;
        }
    }

    public void SetLanguage(string? languageCode)
    {
        var resolved = Resolve(languageCode);
        if (resolved == _current && CurrentCulture is not null)
            return;

        _current = resolved;
        CurrentCulture = CultureInfo.GetCultureInfo(resolved);

        // Los formatos sensibles a la cultura siguen el idioma elegido (constitucion 15).
        CultureInfo.DefaultThreadCurrentCulture = CurrentCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CurrentCulture;

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Resuelve el idioma efectivo: el elegido por el usuario si esta soportado; si se pide
    /// seguir al sistema, el del sistema cuando este soportado; en cualquier otro caso, ingles.
    /// </summary>
    private static string Resolve(string? languageCode)
    {
        if (!string.IsNullOrWhiteSpace(languageCode))
            return IsSupported(languageCode) ? languageCode : DefaultLanguage;

        try
        {
            var system = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return IsSupported(system) ? system : DefaultLanguage;
        }
        catch (Exception)
        {
            return DefaultLanguage;
        }
    }

    private static bool IsSupported(string code) => code is "es" or "en";

    private static readonly Dictionary<string, string> English = new()
    {
        ["AppName"] = "File Manager",
        ["AppDescription"] = "Browse and manage the files on your device",
        ["Company"] = "Socratic",

        // Navegacion y barra superior
        ["InternalStorage"] = "Internal storage",
        ["Search"] = "Search",
        ["SearchPlaceholder"] = "Search in this folder…",
        ["NewFolder"] = "New folder",
        ["More"] = "More",
        ["Settings"] = "Settings",
        ["About"] = "About",
        ["Sort"] = "Sort by",
        ["ShowHiddenOn"] = "Show hidden files",
        ["ShowHiddenOff"] = "Hide hidden files",
        ["Refresh"] = "Refresh",
        ["SelectAll"] = "Select all",

        // Ordenacion
        ["SortNameAsc"] = "Name (A–Z)",
        ["SortNameDesc"] = "Name (Z–A)",
        ["SortDateDesc"] = "Date (newest first)",
        ["SortDateAsc"] = "Date (oldest first)",
        ["SortSizeDesc"] = "Size (largest first)",
        ["SortSizeAsc"] = "Size (smallest first)",

        // Lista
        ["EmptyFolder"] = "This folder is empty",
        ["EmptyFolderHint"] = "Create a folder or paste files here",
        ["NoSearchResults"] = "No files match your search",
        ["ItemsCount"] = "{0} items",
        ["OneItem"] = "1 item",
        ["FolderItems"] = "{0} items",
        ["FolderOneItem"] = "1 item",
        ["FolderEmpty"] = "Empty",
        ["Loading"] = "Loading…",

        // Acciones sobre un elemento
        ["Open"] = "Open",
        ["OpenWith"] = "Open with…",
        ["Rename"] = "Rename",
        ["Copy"] = "Copy",
        ["Cut"] = "Move",
        ["Paste"] = "Paste",
        ["Delete"] = "Delete",
        ["Share"] = "Share",
        ["Details"] = "Details",
        ["Cancel"] = "Cancel",
        ["Ok"] = "OK",
        ["Close"] = "Close",
        ["Save"] = "Save",
        ["Back"] = "← Back",

        // Dialogos
        ["NewFolderTitle"] = "New folder",
        ["NewFolderPrompt"] = "Folder name",
        ["RenameTitle"] = "Rename",
        ["RenamePrompt"] = "New name",
        ["DeleteTitle"] = "Delete",
        ["DeleteFileConfirm"] = "Delete “{0}”? This cannot be undone.",
        ["DeleteFolderConfirm"] = "Delete the folder “{0}” and all its contents? This cannot be undone.",
        ["DeleteManyConfirm"] = "Delete {0} selected items? This cannot be undone.",
        ["OverwriteTitle"] = "Item already exists",
        ["OverwriteConfirm"] = "“{0}” already exists in this folder. Replace it?",
        ["Replace"] = "Replace",
        ["KeepBoth"] = "Keep both",

        // Detalles
        ["DetailsName"] = "Name",
        ["DetailsPath"] = "Path",
        ["DetailsType"] = "Type",
        ["DetailsSize"] = "Size",
        ["DetailsModified"] = "Modified",
        ["DetailsContents"] = "Contents",
        ["TypeFolder"] = "Folder",
        ["TypeFile"] = "File",

        // Resultados
        ["Pasted"] = "{0} items pasted",
        ["Moved"] = "{0} items moved",
        ["Deleted"] = "{0} items deleted",
        ["Copied"] = "{0} items copied to clipboard",
        ["CutToClipboard"] = "{0} items ready to move",
        ["Renamed"] = "Renamed",
        ["FolderCreated"] = "Folder created",
        ["Working"] = "Working…",

        // Errores (constitucion 17: sin fallos silenciosos)
        ["Error"] = "Error",
        ["ErrorNameEmpty"] = "The name cannot be empty.",
        ["ErrorNameInvalid"] = "The name contains characters that are not allowed: \\ / : * ? \" < > |",
        ["ErrorNameExists"] = "An item with that name already exists in this folder.",
        ["ErrorAccessDenied"] = "Access to “{0}” was denied.",
        ["ErrorReadFolder"] = "The folder could not be opened: {0}",
        ["ErrorOpenFile"] = "The file could not be opened: {0}",
        ["ErrorNoAppForFile"] = "No app on this device can open this type of file.",
        ["ErrorShare"] = "The file could not be shared: {0}",
        ["ErrorDelete"] = "Some items could not be deleted: {0}",
        ["ErrorPaste"] = "Some items could not be pasted: {0}",
        ["ErrorRename"] = "The item could not be renamed: {0}",
        ["ErrorCreateFolder"] = "The folder could not be created: {0}",
        ["ErrorPasteIntoItself"] = "A folder cannot be copied into itself.",
        ["ErrorSearch"] = "The search could not be completed: {0}",

        // Permisos
        ["PermissionTitle"] = "Storage access required",
        ["PermissionBody"] = "To browse and manage the files on your device, this app needs access to all files. Android will now open the system settings so you can grant it.",
        ["PermissionGrant"] = "Grant access",
        ["PermissionDenied"] = "Without storage access the app cannot show your files.",
        ["PermissionOpenSettings"] = "Open settings",
        ["PermissionGranted"] = "Storage access granted",
        ["PermissionRetry"] = "Retry",

        // Ajustes
        ["SettingsTitle"] = "Settings",
        ["SettingsLanguage"] = "Language",
        ["SettingsLanguageSystem"] = "System language",
        ["SettingsLanguageEnglish"] = "English",
        ["SettingsLanguageSpanish"] = "Español",
        ["SettingsLanguageHint"] = "The app follows your device language and falls back to English when it is not supported.",
        ["SettingsDisplay"] = "Display",
        ["SettingsShowHidden"] = "Show hidden files",
        ["SettingsShowHiddenHint"] = "Show files and folders whose name starts with a dot.",
        ["SettingsConfirmDelete"] = "Ask before deleting",
        ["SettingsConfirmDeleteHint"] = "Show a confirmation dialog before deleting anything.",
        ["SettingsStorage"] = "Storage",
        ["SettingsPermissionState"] = "All files access",
        ["SettingsPermissionOk"] = "Granted",
        ["SettingsPermissionKo"] = "Not granted",

        // About
        ["AboutTitle"] = "About",
        ["AboutVersion"] = "Version {0}",
        ["AboutContact"] = "Contact",
        ["AboutContactHint"] = "Tap to send an email",
        ["AboutLanguageHint"] = "Select your preferred language",
        ["AboutDonation"] = "Support Development",
        ["AboutDonationButton"] = "Ko-fi.com - Buy me a coffee",
        ["AboutDonationHint"] = "Your support helps maintain and improve the app",
        ["AboutLegal"] = "Legal Notice",
        ["AboutLegal1"] = "This software is provided 'as is', without warranty of any kind. The user is responsible for proper use of the app and compliance with local laws.",
        ["AboutLegal2"] = "In no event shall the authors be liable for any direct, indirect, incidental or consequential damages arising from the use of this software.",
        ["AboutWarning"] = "⚠️ Use at your own risk",
        ["AboutPrivacy"] = "Privacy",
        ["AboutPrivacyText"] = "This app does not collect your personal data or send it to the developers. Information is processed on your device for the app's own purpose.",
        ["AboutLicense"] = "License",
        ["AboutLicenseText"] = "This app is free software distributed under the MIT license.",
        ["EmailSubject"] = "Contact from File Manager",
        ["ErrorEmailNotAvailable"] = "No email app is available on this device.",
        ["ErrorEmail"] = "The email app could not be opened",
        ["BrowserNotAvailable"] = "Browser not available",
        ["LinkCopied"] = "The link was copied to the clipboard",
        ["ErrorBrowser"] = "The browser could not be opened"
    };

    private static readonly Dictionary<string, string> Spanish = new()
    {
        ["AppName"] = "Gestor de Ficheros",
        ["AppDescription"] = "Explora y gestiona los ficheros de tu dispositivo",
        ["Company"] = "Socratic",

        // Navegacion y barra superior
        ["InternalStorage"] = "Almacenamiento interno",
        ["Search"] = "Buscar",
        ["SearchPlaceholder"] = "Buscar en esta carpeta…",
        ["NewFolder"] = "Nueva carpeta",
        ["More"] = "Más",
        ["Settings"] = "Configuración",
        ["About"] = "Acerca de",
        ["Sort"] = "Ordenar por",
        ["ShowHiddenOn"] = "Mostrar ficheros ocultos",
        ["ShowHiddenOff"] = "Ocultar ficheros ocultos",
        ["Refresh"] = "Actualizar",
        ["SelectAll"] = "Seleccionar todo",

        // Ordenacion
        ["SortNameAsc"] = "Nombre (A–Z)",
        ["SortNameDesc"] = "Nombre (Z–A)",
        ["SortDateDesc"] = "Fecha (más reciente primero)",
        ["SortDateAsc"] = "Fecha (más antiguo primero)",
        ["SortSizeDesc"] = "Tamaño (mayor primero)",
        ["SortSizeAsc"] = "Tamaño (menor primero)",

        // Lista
        ["EmptyFolder"] = "Esta carpeta está vacía",
        ["EmptyFolderHint"] = "Crea una carpeta o pega ficheros aquí",
        ["NoSearchResults"] = "Ningún fichero coincide con la búsqueda",
        ["ItemsCount"] = "{0} elementos",
        ["OneItem"] = "1 elemento",
        ["FolderItems"] = "{0} elementos",
        ["FolderOneItem"] = "1 elemento",
        ["FolderEmpty"] = "Vacía",
        ["Loading"] = "Cargando…",

        // Acciones sobre un elemento
        ["Open"] = "Abrir",
        ["OpenWith"] = "Abrir con…",
        ["Rename"] = "Renombrar",
        ["Copy"] = "Copiar",
        ["Cut"] = "Mover",
        ["Paste"] = "Pegar",
        ["Delete"] = "Eliminar",
        ["Share"] = "Compartir",
        ["Details"] = "Detalles",
        ["Cancel"] = "Cancelar",
        ["Ok"] = "Aceptar",
        ["Close"] = "Cerrar",
        ["Save"] = "Guardar",
        ["Back"] = "← Volver",

        // Dialogos
        ["NewFolderTitle"] = "Nueva carpeta",
        ["NewFolderPrompt"] = "Nombre de la carpeta",
        ["RenameTitle"] = "Renombrar",
        ["RenamePrompt"] = "Nuevo nombre",
        ["DeleteTitle"] = "Eliminar",
        ["DeleteFileConfirm"] = "¿Eliminar «{0}»? Esta acción no se puede deshacer.",
        ["DeleteFolderConfirm"] = "¿Eliminar la carpeta «{0}» y todo su contenido? Esta acción no se puede deshacer.",
        ["DeleteManyConfirm"] = "¿Eliminar los {0} elementos seleccionados? Esta acción no se puede deshacer.",
        ["OverwriteTitle"] = "El elemento ya existe",
        ["OverwriteConfirm"] = "«{0}» ya existe en esta carpeta. ¿Quieres reemplazarlo?",
        ["Replace"] = "Reemplazar",
        ["KeepBoth"] = "Conservar ambos",

        // Detalles
        ["DetailsName"] = "Nombre",
        ["DetailsPath"] = "Ruta",
        ["DetailsType"] = "Tipo",
        ["DetailsSize"] = "Tamaño",
        ["DetailsModified"] = "Modificado",
        ["DetailsContents"] = "Contenido",
        ["TypeFolder"] = "Carpeta",
        ["TypeFile"] = "Fichero",

        // Resultados
        ["Pasted"] = "{0} elementos pegados",
        ["Moved"] = "{0} elementos movidos",
        ["Deleted"] = "{0} elementos eliminados",
        ["Copied"] = "{0} elementos copiados al portapapeles",
        ["CutToClipboard"] = "{0} elementos listos para mover",
        ["Renamed"] = "Renombrado",
        ["FolderCreated"] = "Carpeta creada",
        ["Working"] = "Trabajando…",

        // Errores (constitucion 17: sin fallos silenciosos)
        ["Error"] = "Error",
        ["ErrorNameEmpty"] = "El nombre no puede estar vacío.",
        ["ErrorNameInvalid"] = "El nombre contiene caracteres no permitidos: \\ / : * ? \" < > |",
        ["ErrorNameExists"] = "Ya existe un elemento con ese nombre en esta carpeta.",
        ["ErrorAccessDenied"] = "Se ha denegado el acceso a «{0}».",
        ["ErrorReadFolder"] = "No se ha podido abrir la carpeta: {0}",
        ["ErrorOpenFile"] = "No se ha podido abrir el fichero: {0}",
        ["ErrorNoAppForFile"] = "Ninguna aplicación del dispositivo puede abrir este tipo de fichero.",
        ["ErrorShare"] = "No se ha podido compartir el fichero: {0}",
        ["ErrorDelete"] = "No se han podido eliminar algunos elementos: {0}",
        ["ErrorPaste"] = "No se han podido pegar algunos elementos: {0}",
        ["ErrorRename"] = "No se ha podido renombrar el elemento: {0}",
        ["ErrorCreateFolder"] = "No se ha podido crear la carpeta: {0}",
        ["ErrorPasteIntoItself"] = "Una carpeta no se puede copiar dentro de sí misma.",
        ["ErrorSearch"] = "No se ha podido completar la búsqueda: {0}",

        // Permisos
        ["PermissionTitle"] = "Se necesita acceso al almacenamiento",
        ["PermissionBody"] = "Para explorar y gestionar los ficheros del dispositivo, la aplicación necesita acceso a todos los ficheros. Android abrirá ahora los ajustes del sistema para que puedas concederlo.",
        ["PermissionGrant"] = "Conceder acceso",
        ["PermissionDenied"] = "Sin acceso al almacenamiento la aplicación no puede mostrar tus ficheros.",
        ["PermissionOpenSettings"] = "Abrir ajustes",
        ["PermissionGranted"] = "Acceso al almacenamiento concedido",
        ["PermissionRetry"] = "Reintentar",

        // Ajustes
        ["SettingsTitle"] = "Configuración",
        ["SettingsLanguage"] = "Idioma",
        ["SettingsLanguageSystem"] = "Idioma del sistema",
        ["SettingsLanguageEnglish"] = "English",
        ["SettingsLanguageSpanish"] = "Español",
        ["SettingsLanguageHint"] = "La aplicación sigue el idioma del dispositivo y usa inglés cuando no está soportado.",
        ["SettingsDisplay"] = "Visualización",
        ["SettingsShowHidden"] = "Mostrar ficheros ocultos",
        ["SettingsShowHiddenHint"] = "Muestra los ficheros y carpetas cuyo nombre empieza por punto.",
        ["SettingsConfirmDelete"] = "Preguntar antes de eliminar",
        ["SettingsConfirmDeleteHint"] = "Muestra un diálogo de confirmación antes de eliminar nada.",
        ["SettingsStorage"] = "Almacenamiento",
        ["SettingsPermissionState"] = "Acceso a todos los ficheros",
        ["SettingsPermissionOk"] = "Concedido",
        ["SettingsPermissionKo"] = "No concedido",

        // About
        ["AboutTitle"] = "Acerca de",
        ["AboutVersion"] = "Versión {0}",
        ["AboutContact"] = "Contacto",
        ["AboutContactHint"] = "Toca para enviar un correo electrónico",
        ["AboutLanguageHint"] = "Selecciona tu idioma preferido",
        ["AboutDonation"] = "Apoya el Desarrollo",
        ["AboutDonationButton"] = "Ko-fi.com - Invítame un café",
        ["AboutDonationHint"] = "Tu apoyo ayuda a mantener y mejorar la aplicación",
        ["AboutLegal"] = "Aviso Legal",
        ["AboutLegal1"] = "Este software se proporciona «tal cual», sin garantías de ningún tipo. El usuario es responsable del uso adecuado de la aplicación y del cumplimiento de las leyes locales.",
        ["AboutLegal2"] = "En ningún caso los autores serán responsables de daños directos, indirectos, incidentales o consecuentes que resulten del uso de este software.",
        ["AboutWarning"] = "⚠️ Uso bajo su propio riesgo",
        ["AboutPrivacy"] = "Privacidad",
        ["AboutPrivacyText"] = "Esta aplicación no recopila tus datos personales ni los envía a los desarrolladores. La información se procesa en tu dispositivo para la función propia de la app.",
        ["AboutLicense"] = "Licencia",
        ["AboutLicenseText"] = "Esta aplicación es software libre distribuido bajo licencia MIT.",
        ["EmailSubject"] = "Contacto desde Gestor de Ficheros",
        ["ErrorEmailNotAvailable"] = "No hay ninguna aplicación de correo disponible en este dispositivo.",
        ["ErrorEmail"] = "No se ha podido abrir la aplicación de correo",
        ["BrowserNotAvailable"] = "Navegador no disponible",
        ["LinkCopied"] = "El enlace se ha copiado al portapapeles",
        ["ErrorBrowser"] = "No se ha podido abrir el navegador"
    };
}
