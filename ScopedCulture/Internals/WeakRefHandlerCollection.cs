using Microsoft.AspNetCore.Components;

namespace Toolbelt.Blazor.Server.ScopedCulture.Internals;

internal class WeakRefHandlerCollection
{
    private readonly List<WeakReference<IHandleEvent>> Collection = new();

    public void Add(IHandleEvent element)
    {
        lock (this.Collection)
        {
            this.SweepGarbageCollectedComponents();

            if (!this.Collection.Exists(cref => cref.TryGetTarget(out var c) && c == element))
                this.Collection.Add(new WeakReference<IHandleEvent>(element));
        }
    }

    public void ForEach(Action<IHandleEvent> action)
    {
        lock (this.Collection)
        {
            this.SweepGarbageCollectedComponents();

            foreach (var cref in this.Collection)
            {
                if (cref.TryGetTarget(out var element))
                {
                    action(element);
                }
            }
        }
    }

    private void SweepGarbageCollectedComponents()
    {
        lock (this.Collection)
        {
            // DEBUG: var beforeCount = this.Components.Count;
            for (var i = this.Collection.Count - 1; i >= 0; i--)
            {
                if (!this.Collection[i].TryGetTarget(out var _)) this.Collection.RemoveAt(i);
            }
            // DEBUG: var afterCount = this.Components.Count;
            // DEBUG: Console.WriteLine($"SweepGarbageCollectedComponents - {(beforeCount - afterCount)} objects are sweeped. ({this.Components.Count} objects are stay.)");
        }
    }

    public void InvokeStateHasChanged()
    {
        this.ForEach(handler =>
        {
            handler.HandleEventAsync(EventCallbackWorkItem.Empty, null);
        });
    }
}
