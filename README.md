# Blazor Server Scoped Culture [![NuGet Package](https://img.shields.io/nuget/v/Toolbelt.Blazor.Server.ScopedCulture.svg)](https://www.nuget.org/packages/Toolbelt.Blazor.Server.ScopedCulture/)

_"It's a very dirty hack, but it works for now."_

## Summary

This is a library for Blazor Server apps adding an ability that **changes the current culture of each connection without reloading**.

![movie.1](https://raw.githubusercontent.com/jsakamoto/Toolbelt.Blazor.Server.ScopedCulture/main/.assets/movie.001.gif)

## Quick Start

### Installation

1. Add the "Toolbelt.Blazor.Server.ScopedCulture" NuGet package to your Blazor server application project.

```shell
dotnet add package Toolbelt.Blazor.Server.ScopedCulture --prerelease
```

2. Register the "Scoped Culture" service into DI container in your app.

```csharp
// Program.cs
using Toolbelt.Blazor.Extensions.DependencyInjection; // 👈 Open this namespace, and...
...
builder.Services.AddScopedCulture(); // 👈 Add this line.
...
```

3. To be convinience, open the "Toolbelt.Blazor.Server.ScopedCulture" name space globally.

```razor
@* _Import.razor *@
...
@* 👇 Open this name space. *@
@using Toolbelt.Blazor.Server.ScopedCulture
```

4. Surround the entire contents in `App.razor` with the `<ScopedCultureZone>` component tag.

```html
@* App.razor *@
<ScopedCultureZone>
  <Router AppAssembly="@typeof(Program).Assembly">
    ...
  </Router>
</ScopedCultureZone>
```

### Usage

When you want to change the current culture & current UI culture, don't set the culture values you want to change to `CultureInfo.CurrentCulture` and `CultureInfo.CurrentUICulture` static properties directly because it doesn't cause any effect.

Instead, now you can call the `SetCurrentCulture()` method of the `IScopedCulture` service anytime with the culture name you want to change.

The SignalR connection and application states will be kept.

```csharp
@* *.razor *@
@* 👇 Inject the IScopedCulture service into your Razor components. *@
@inject IScopedCulture ScopedCulture
...
@code {
  ...
  // 👇 Call "SetCurrentCulture()" method with the culture name such as "en", "sv", "ja", etc.
  this.ScopedCulture.SetCurrentCulture(cultureName);
  ...
```

### Track to changes in current culture

Suppose you need to track changing current culture in the current connection on Blazor server apps, particularly re-rendering components after changed culture. 

In that case, you can do that by one of the following three methods.


#### Method A. Surround contents by the `ScopedCultureZone` component

When you change the current culture in the current connection on Blazor Server apps, a `ScopedCultureZone` component's `StateHasChanged()` method will be invoked.

Then, that will cause re-rendering of the child content inside a `ScopedCultureZone` component.

```html
@* *.razor *@
<ScopedCultureZone>
  This area will be re-rendered every time you change the current 
  culture by using the "ScopedCulture.SetCurrentCulture()".
</ScopedCultureZone>
```

#### Method B. Register componets to re-render by the `RefreshWhenCultureChanged()` method

Once you invoke the `IScopedCulture.RefreshWhenCultureChanged()` method with your component as an argument, that component's `StateHasChanged()` method will be invoked every time you change the current culture in the current connection on Blazor Server apps.

```csharp
@* *.razor *@
@inject IScopedCulture ScopedCulture
...
@code 
{
  public override void OnInitialized() 
  {
    // 👇 After doing this, the "StateHasChanged()" method of this component
    //    will be invoked every time you change the current culture
    //    by using the "IScopedCulture.SetCurrentCulture()".
    this.ScopedCulture.RefreshWhenCultureChanged(this);
  }
}
```

#### Method C. Handle the `IScopedCulture.CurrentCultureChanged` event

```csharp
@* *.razor *@
@* 👇 Please remember to implement IDisposable interface. *@
@implements IDisposable
@inject IScopedCulture ScopedCulture
...
@code 
{
  public override void OnInitialized() 
  {
    // 👇 Handle the `IScopedCulture.CurrentCultureChanged` event.
    this.ScopedCulture.CurrentCultureChanged += this.ScopedCulture_CurrentCultureChanged;
  }

  private void ScopedCulture_CurrentCultureChanged(object sender, EventArgs e) {
    // 👉 This method will be invoked every time you change 
    // the current culture by using the "IScopedCulture.SetCurrentCulture()".
  }

  public void Dispose() {
    // 👇 Please remember to detach the event handler.
    this.ScopedCulture.CurrentCultureChanged -= this.ScopedCulture_CurrentCultureChanged;
  }
}
```

## Supported versions

Blazor Server apps on .NET Core 3.1 or later (including .NET 5.0, 6.0, 7.0) are supported.

## Disclaimer

Please remember that this library **access and overwrite non-public API** of the Blazor Server's infrastructure.

That means there is a risk that **this library might cause your apps to be crashed unexpectedly** in the current and future versions of .NET.

## Release notes

The release notes is [here](https://github.com/jsakamoto/Toolbelt.Blazor.Server.ScopedCulture/blob/main/ScopedCulture/RELEASE-NOTES.txt).

## License

[MIT License](https://github.com/jsakamoto/Toolbelt.Blazor.Server.ScopedCulture/blob/main/LICENSE)
