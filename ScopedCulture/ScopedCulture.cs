using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Toolbelt.Blazor.Server.ScopedCulture.Internals;

namespace Toolbelt.Blazor.Server.ScopedCulture;

internal class ScopedCulture : IScopedCulture
{
    public CultureInfo CurrentCulture { get; private set; }

    public CultureInfo CurrentUICulture { get; private set; }

    public event EventHandler? CurrentCultureChanged;

    private IServiceProvider Services { get; }

    private WeakRefHandlerCollection Handlers { get; } = new();

    private bool Installed { get; set; }

    public ScopedCulture(IServiceProvider services)
    {
        this.CurrentCulture = CultureInfo.CurrentCulture;
        this.CurrentUICulture = CultureInfo.CurrentUICulture;
        this.Services = services;
        this.EnsureInstalled();
    }

    public void SetCurrentCulture(CultureInfo culture) => this.SetCurrentCulture(culture, culture);

    public void SetCurrentCulture(CultureInfo culture, CultureInfo uiCulture)
    {
        if (this.CurrentCulture == culture || this.CurrentUICulture == uiCulture) return;

        this.CurrentCulture = culture;
        this.CurrentUICulture = uiCulture;
        CultureInfo.CurrentCulture = this.CurrentCulture;
        CultureInfo.CurrentUICulture = this.CurrentUICulture;

        this.CurrentCultureChanged?.Invoke(this, EventArgs.Empty);
        this.Handlers.InvokeStateHasChanged();
    }

    public void EnsureInstalled()
    {
        lock (this)
        {
            if (this.Installed) return;
            this.Installed = true;

            // What this method does is:
            // (service as ICircuitAccessor).Circuit.CircuitHost.RemoteRenderer.Dispatcher = new RendererSynchronizationContextDispatcher(this);

            const string namespc = "Microsoft.AspNetCore.Components.Server.Circuits.";
            var types = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes());

            var typeofICircuitAccessor = types.FirstOrDefault(type => type.FullName == namespc + "ICircuitAccessor");
            var typeofCircuitHost = types.FirstOrDefault(type => type.FullName == namespc + "CircuitHost");
            var typeofRemoteRenderer = types.FirstOrDefault(type => type.FullName == namespc + "RemoteRenderer");
            var circuitHostField = typeof(Circuit).GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(field => field.FieldType.FullName == namespc + "CircuitHost");
            if (typeofICircuitAccessor == null || typeofCircuitHost == null || circuitHostField == null || typeofRemoteRenderer == null) return;

            var circuitProp = typeofICircuitAccessor.GetProperty("Circuit");
            var rendererProp = typeofCircuitHost.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(prop => prop.PropertyType.FullName == namespc + "RemoteRenderer");
            var dispatcherBackFiled = typeofRemoteRenderer.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(field => field.FieldType.FullName == "Microsoft.AspNetCore.Components.Dispatcher");
            if (circuitProp == null || rendererProp == null || dispatcherBackFiled == null) return;


            //  circuitAccessor = this.Services.GetService<ICircuitAccessor>();
            var circuitAccessor = this.Services.GetService(typeofICircuitAccessor);
            if (circuitAccessor == null) return;

            //  circuit = circuitAccessor.Circuit;
            var circuit = circuitProp.GetValue(circuitAccessor);
            if (circuit == null) return;

            //  circuitHost = circuit.CircuitHost;
            var circuitHost = circuitHostField.GetValue(circuit);
            //  renderer = circuitHost.RemoteRenderer;
            var renderer = rendererProp.GetValue(circuitHost);

            //  renderer.Dispatcher = new RendererSynchronizationContextDispatcher(this);
            var dispatcher = new RendererSynchronizationContextDispatcher(this);
            dispatcherBackFiled.SetValue(renderer, dispatcher);

            SynchronizationContext.SetSynchronizationContext(dispatcher._context);
        }
    }

    public void RefreshWhenCultureChanged(IHandleEvent eventHandler)
    {
        this.Handlers.Add(eventHandler);
    }
}
