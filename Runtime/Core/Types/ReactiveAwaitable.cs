using System;
using System.Threading.Tasks;

namespace Shtl.Mvvm
{
    public class ReactiveAwaitable : IReactiveValue
    {
        private readonly ReactiveValue<TaskCompletionSource<bool>> _promise = new();

        public void Connect(Action<TaskCompletionSource<bool>> onWaitingStarted)
        {
            _promise.Connect(onWaitingStarted);
        }

        public async Task<bool> StartAsync()
        {
            if (_promise.Value is { Task: { IsCompleted: false } })
            {
                return await SuppressCancellation(_promise.Value.Task);
            }

            _promise.Value = new TaskCompletionSource<bool>();
            return await SuppressCancellation(_promise.Value.Task);
        }

        public void Unbind()
        {
            _promise.Value?.TrySetCanceled();
            _promise.Unbind();
        }

        void IReactiveValue.Dispose() => Unbind();

        private static async Task<bool> SuppressCancellation(Task<bool> task)
        {
            try
            {
                await task;
                return false;
            }
            catch (TaskCanceledException)
            {
                return true;
            }
        }
    }
}
