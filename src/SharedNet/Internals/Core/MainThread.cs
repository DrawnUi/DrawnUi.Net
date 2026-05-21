using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace DrawnUi.Draw.ApplicationModel
{
    public static class MainThread
    {
        private static Action<Action>? _beginInvokeHandler;
        private static Func<bool>? _isMainThreadHandler;

        public static bool IsMainThread => _isMainThreadHandler?.Invoke() ?? true;

        public static void Configure(Action<Action>? beginInvokeHandler, Func<bool>? isMainThreadHandler = null)
        {
            _beginInvokeHandler = beginInvokeHandler;
            _isMainThreadHandler = isMainThreadHandler;
        }

        public static void Reset()
        {
            _beginInvokeHandler = null;
            _isMainThreadHandler = null;
        }

        public static void BeginInvokeOnMainThread(Action action)
        {
            if (action == null)
                return;

            if (IsMainThread || _beginInvokeHandler == null)
            {
                action();
                return;
            }

            _beginInvokeHandler(action);
        }

        public static Task InvokeOnMainThreadAsync(Action action)
        {
            if (action == null)
                return Task.CompletedTask;

            if (IsMainThread || _beginInvokeHandler == null)
            {
                action();
                return Task.CompletedTask;
            }

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            _beginInvokeHandler(() =>
            {
                try
                {
                    action();
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            });

            return completion.Task;
        }

        public static async Task InvokeOnMainThreadAsync(Func<Task> action)
        {
            if (action == null)
            {
                return;
            }

            if (IsMainThread || _beginInvokeHandler == null)
            {
                await action();
                return;
            }

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            _beginInvokeHandler(async () =>
            {
                try
                {
                    await action();
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            });

            await completion.Task;
        }
    }
}

namespace DrawnUi.Draw
{
}

namespace DrawnUi.Draw
{
}

namespace DrawnUi
{
}

namespace DrawnUi.Draw
{
}

