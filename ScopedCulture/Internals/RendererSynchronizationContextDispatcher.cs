// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Components;

namespace Toolbelt.Blazor.Server.ScopedCulture.Internals;

internal class RendererSynchronizationContextDispatcher : Dispatcher
{
    internal readonly RendererSynchronizationContext _context;

    public RendererSynchronizationContextDispatcher(IScopedCulture scopedCulture)
    {
        this._context = new RendererSynchronizationContext(scopedCulture);
        this._context.UnhandledException += (sender, e) =>
        {
            this.OnUnhandledException(e);
        };
    }

    public override bool CheckAccess() => SynchronizationContext.Current == this._context;

    public override Task InvokeAsync(Action workItem)
    {
        if (this.CheckAccess())
        {
            workItem();
            return Task.CompletedTask;
        }

        return this._context.InvokeAsync(workItem);
    }

    public override Task InvokeAsync(Func<Task> workItem)
    {
        if (this.CheckAccess())
        {
            return workItem();
        }

        return this._context.InvokeAsync(workItem);
    }

    public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem)
    {
        if (this.CheckAccess())
        {
            return Task.FromResult(workItem());
        }

        return this._context.InvokeAsync<TResult>(workItem);
    }

    public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem)
    {
        if (this.CheckAccess())
        {
            return workItem();
        }

        return this._context.InvokeAsync<TResult>(workItem);
    }
}
