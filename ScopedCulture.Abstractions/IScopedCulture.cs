using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Toolbelt.Blazor.Server.ScopedCulture;

public interface IScopedCulture
{
    CultureInfo CurrentCulture { get; }

    CultureInfo CurrentUICulture { get; }

    void SetCurrentCulture(CultureInfo culture);

    void SetCurrentCulture(CultureInfo culture, CultureInfo uiCulture);

    void RefreshWhenCultureChanged(IHandleEvent eventHandler);

    public event EventHandler? CurrentCultureChanged;
}
