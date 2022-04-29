// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable warnings

using System.Diagnostics;
using System.Globalization;

namespace Toolbelt.Blazor.Server.ScopedCulture.Internals;

[DebuggerDisplay("{_state,nq}")]
internal class RendererSynchronizationContext : SynchronizationContext
{
    private static readonly ContextCallback ExecutionContextThunk = (object state) =>
    {
        var item = (WorkItem)state;
        item.SynchronizationContext.ExecuteSynchronously(null, item.Callback, item.State);
    };

    private static readonly Action<Task, object> BackgroundWorkThunk = (Task task, object state) =>
    {
        var item = (WorkItem)state;
        item.SynchronizationContext.ExecuteBackground(item);
    };

    private readonly State _state;

    private IScopedCulture ScopedCulture { get; }

    public event UnhandledExceptionEventHandler UnhandledException;

    public RendererSynchronizationContext(IScopedCulture scopedCulture)
        : this(new State(), scopedCulture)
    {
    }

    private RendererSynchronizationContext(State state, IScopedCulture scopedCulture)
    {
        this._state = state;
        this.ScopedCulture = scopedCulture;
    }

    public Task InvokeAsync(Action action)
    {
        var completion = new RendererSynchronizationTaskCompletionSource<Action, object>(action);
        this.ExecuteSynchronouslyIfPossible((state) =>
        {
            var completion = (RendererSynchronizationTaskCompletionSource<Action, object>)state;
            try
            {
                CultureInfo.CurrentCulture = this.ScopedCulture.CurrentCulture;
                CultureInfo.CurrentUICulture = this.ScopedCulture.CurrentUICulture;

                completion.Callback();
                completion.SetResult(null);
            }
            catch (OperationCanceledException)
            {
                completion.SetCanceled();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }, completion);

        return completion.Task;
    }

    public Task InvokeAsync(Func<Task> asyncAction)
    {
        var completion = new RendererSynchronizationTaskCompletionSource<Func<Task>, object>(asyncAction);
        this.ExecuteSynchronouslyIfPossible(async (state) =>
        {
            var completion = (RendererSynchronizationTaskCompletionSource<Func<Task>, object>)state;
            try
            {
                CultureInfo.CurrentCulture = this.ScopedCulture.CurrentCulture;
                CultureInfo.CurrentUICulture = this.ScopedCulture.CurrentUICulture;

                await completion.Callback();
                completion.SetResult(null);
            }
            catch (OperationCanceledException)
            {
                completion.SetCanceled();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }, completion);

        return completion.Task;
    }

    public Task<TResult> InvokeAsync<TResult>(Func<TResult> function)
    {
        var completion = new RendererSynchronizationTaskCompletionSource<Func<TResult>, TResult>(function);
        this.ExecuteSynchronouslyIfPossible((state) =>
        {
            var completion = (RendererSynchronizationTaskCompletionSource<Func<TResult>, TResult>)state;
            try
            {
                CultureInfo.CurrentCulture = this.ScopedCulture.CurrentCulture;
                CultureInfo.CurrentUICulture = this.ScopedCulture.CurrentUICulture;

                var result = completion.Callback();
                completion.SetResult(result);
            }
            catch (OperationCanceledException)
            {
                completion.SetCanceled();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }, completion);

        return completion.Task;
    }

    public Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> asyncFunction)
    {
        var completion = new RendererSynchronizationTaskCompletionSource<Func<Task<TResult>>, TResult>(asyncFunction);
        this.ExecuteSynchronouslyIfPossible(async (state) =>
        {
            var completion = (RendererSynchronizationTaskCompletionSource<Func<Task<TResult>>, TResult>)state;
            try
            {
                CultureInfo.CurrentCulture = this.ScopedCulture.CurrentCulture;
                CultureInfo.CurrentUICulture = this.ScopedCulture.CurrentUICulture;

                var result = await completion.Callback();
                completion.SetResult(result);
            }
            catch (OperationCanceledException)
            {
                completion.SetCanceled();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }, completion);

        return completion.Task;
    }

    // asynchronously runs the callback
    //
    // NOTE: this must always run async. It's not legal here to execute the work item synchronously.
    public override void Post(SendOrPostCallback d, object state)
    {
        lock (this._state.Lock)
        {
            this._state.Task = this.Enqueue(this._state.Task, d, state, forceAsync: true);
        }
    }

    // synchronously runs the callback
    public override void Send(SendOrPostCallback d, object state)
    {
        var antecedent = default(Task);
        var completion = new TaskCompletionSource<object>();

        lock (this._state.Lock)
        {
            antecedent = this._state.Task;
            this._state.Task = completion.Task;
        }

        // We have to block. That's the contract of Send - we don't expect this to be used
        // in many scenarios in Components.
        //
        // Using Wait here is ok because the antecedent task will never throw.
        antecedent.Wait();

        this.ExecuteSynchronously(completion, d, state);
    }

    // shallow copy
    public override SynchronizationContext CreateCopy()
    {
        return new RendererSynchronizationContext(this._state, this.ScopedCulture);
    }

    // Similar to Post, but it can runs the work item synchronously if the context is not busy.
    //
    // This is the main code path used by components, we want to be able to run async work but only dispatch
    // if necessary.
    private void ExecuteSynchronouslyIfPossible(SendOrPostCallback d, object state)
    {
        var completion = default(TaskCompletionSource<object>);
        lock (this._state.Lock)
        {
            if (!this._state.Task.IsCompleted)
            {
                this._state.Task = this.Enqueue(this._state.Task, d, state);
                return;
            }

            // We can execute this synchronously because nothing is currently running
            // or queued.
            completion = new TaskCompletionSource<object>();
            this._state.Task = completion.Task;
        }

        this.ExecuteSynchronously(completion, d, state);
    }

    private Task Enqueue(Task antecedent, SendOrPostCallback d, object state, bool forceAsync = false)
    {
        // If we get here is means that a callback is being explicitly queued. Let's instead add it to the queue and yield.
        //
        // We use our own queue here to maintain the execution order of the callbacks scheduled here. Also
        // we need a queue rather than just scheduling an item in the thread pool - those items would immediately
        // block and hurt scalability.
        //
        // We need to capture the execution context so we can restore it later. This code is similar to
        // the call path of ThreadPool.QueueUserWorkItem and System.Threading.QueueUserWorkItemCallback.
        ExecutionContext executionContext = null;
        if (!ExecutionContext.IsFlowSuppressed())
        {
            executionContext = ExecutionContext.Capture();
        }

        var flags = forceAsync ? TaskContinuationOptions.RunContinuationsAsynchronously : TaskContinuationOptions.None;
        return antecedent.ContinueWith(BackgroundWorkThunk, new WorkItem()
        {
            SynchronizationContext = this,
            ExecutionContext = executionContext,
            Callback = d,
            State = state,
        }, CancellationToken.None, flags, TaskScheduler.Current);
    }

    private void ExecuteSynchronously(
        TaskCompletionSource<object> completion,
        SendOrPostCallback d,
        object state)
    {
        var original = Current;
        try
        {
            SetSynchronizationContext(this);
            this._state.IsBusy = true;

            d(state);
        }
        finally
        {
            this._state.IsBusy = false;
            SetSynchronizationContext(original);

            completion?.SetResult(default);
        }
    }

    private void ExecuteBackground(WorkItem item)
    {
        if (item.ExecutionContext == null)
        {
            try
            {
                this.ExecuteSynchronously(null, item.Callback, item.State);
            }
            catch (Exception ex)
            {
                this.DispatchException(ex);
            }

            return;
        }

        // Perf - using a static thunk here to avoid a delegate allocation.
        try
        {
            ExecutionContext.Run(item.ExecutionContext, ExecutionContextThunk, item);
        }
        catch (Exception ex)
        {
            this.DispatchException(ex);
        }
    }

    private void DispatchException(Exception ex)
    {
        var handler = UnhandledException;
        if (handler != null)
        {
            handler(this, new UnhandledExceptionEventArgs(ex, isTerminating: false));
        }
    }

    private class State
    {
        public bool IsBusy; // Just for debugging
        public object Lock = new object();
        public Task Task = Task.CompletedTask;

        public override string ToString()
        {
            return $"{{ Busy: {this.IsBusy}, Pending Task: {this.Task} }}";
        }
    }

    private class WorkItem
    {
        public RendererSynchronizationContext SynchronizationContext;
        public ExecutionContext ExecutionContext;
        public SendOrPostCallback Callback;
        public object State;
    }

    private class RendererSynchronizationTaskCompletionSource<TCallback, TResult> : TaskCompletionSource<TResult>
    {
        public RendererSynchronizationTaskCompletionSource(TCallback callback)
        {
            this.Callback = callback;
        }

        public TCallback Callback { get; }
    }
}
