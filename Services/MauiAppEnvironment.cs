namespace FileManager.Services;

/// <inheritdoc cref="IAppEnvironment"/>
public class MauiAppEnvironment : IAppEnvironment
{
    private readonly IAppInfo _appInfo;
    private readonly IBrowser _browser;
    private readonly IEmail _email;

    public MauiAppEnvironment()
        : this(AppInfo.Current, Browser.Default, Email.Default)
    {
    }

    /// <summary>Con los servicios de MAUI que se le den (pruebas).</summary>
    internal MauiAppEnvironment(IAppInfo appInfo, IBrowser browser, IEmail email)
    {
        _appInfo = appInfo;
        _browser = browser;
        _email = email;
    }

    public string VersionString => _appInfo.VersionString;

    public Task OpenUrlAsync(Uri uri) => _browser.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);

    public async Task<bool> ComposeEmailAsync(string subject, string to)
    {
        try
        {
            await _email.ComposeAsync(new EmailMessage { Subject = subject, To = new List<string> { to } });
            return true;
        }
        catch (FeatureNotSupportedException)
        {
            return false;
        }
    }
}
