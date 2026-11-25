#if ANDROID
using AndroidView = Android.Views.View;
#endif

namespace FileManager.Helpers;

/// <summary>
/// Toque y pulsacion larga sobre una fila de la lista, resueltos con los gestos nativos de Android.
/// </summary>
/// <remarks>
/// MAUI no trae gesto de pulsacion larga, y las dos alternativas de MAUI fallan en Android
/// (comprobado en el emulador, no deducido):
/// <list type="bullet">
///   <item><description><c>PointerGestureRecognizer</c> no dispara <c>PointerPressed</c> con el
///   dedo: mantener pulsado abria la carpeta como un toque normal.</description></item>
///   <item><description>Enganchar <c>View.LongClick</c> dejando el <c>TapGestureRecognizer</c> en
///   la fila tampoco funciona: el detector de gestos de MAUI consume el evento tactil antes de que
///   <c>onTouchEvent</c> llegue a evaluar la pulsacion larga.</description></item>
/// </list>
/// Por eso este comportamiento se encarga de <b>los dos</b> gestos: al no quedar ningun
/// <c>GestureRecognizer</c> en la fila, MAUI no instala su detector y Android reparte
/// <c>Click</c> y <c>LongClick</c> con su tiempo, su vibracion y su cancelacion al desplazar
/// la lista.
///
/// Los eventos se emiten con la <b>fila</b> como emisor, no con el comportamiento: MAUI no
/// propaga el <c>BindingContext</c> a los comportamientos, asi que el manejador se quedaba sin
/// saber que elemento se habia pulsado.
/// </remarks>
public partial class ItemTouchBehavior
{
    private View? _row;

    /// <summary>Toque corto sobre la fila.</summary>
    public event EventHandler? Tapped;

    /// <summary>Pulsacion mantenida sobre la fila.</summary>
    public event EventHandler? LongPressed;

    /// <summary>La fila sobre la que esta puesto (null al quitarlo).</summary>
    private void Attach(View? row) => _row = row;

    /// <summary>Emite el toque corto con la fila como emisor.</summary>
    internal void RaiseTapped() => Tapped?.Invoke(_row, EventArgs.Empty);

    /// <summary>Emite la pulsacion larga con la fila como emisor.</summary>
    internal void RaiseLongPressed() => LongPressed?.Invoke(_row, EventArgs.Empty);
}

#if ANDROID
public partial class ItemTouchBehavior : PlatformBehavior<View, AndroidView>
{
    protected override void OnAttachedTo(View bindable, AndroidView platformView)
    {
        Attach(bindable);
        platformView.Clickable = true;
        platformView.LongClickable = true;
        platformView.Click += OnClick;
        platformView.LongClick += OnLongClick;
    }

    protected override void OnDetachedFrom(View bindable, AndroidView platformView)
    {
        platformView.Click -= OnClick;
        platformView.LongClick -= OnLongClick;
        Attach(null);
    }

    private void OnClick(object? sender, EventArgs e) => RaiseTapped();

    private void OnLongClick(object? sender, AndroidView.LongClickEventArgs e)
    {
        // Consumir el evento evita que al soltar llegue tambien el toque, que abriria el elemento.
        e.Handled = true;
        RaiseLongPressed();
    }
}
#else
/// <summary>
/// Fuera de Android (el banco de pruebas, net10.0) no hay gesto nativo: el comportamiento solo
/// recuerda su fila, y las pruebas emiten el toque con <see cref="RaiseTapped"/>.
/// </summary>
public partial class ItemTouchBehavior : Behavior<View>
{
    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);
        Attach(bindable);
    }

    protected override void OnDetachingFrom(View bindable)
    {
        Attach(null);
        base.OnDetachingFrom(bindable);
    }
}
#endif
