using System;
using System.Collections.Generic;
using System.Linq;

namespace Shtl.Mvvm
{
    internal static class BindingPool
    {
        private interface IPoolHolder<out TBinding>
            where TBinding : AbstractEventBinding
        {
            public TBinding Get();
            void Dispose(AbstractEventBinding binding);
        }

        private class PoolHolder<TBinding> : IPoolHolder<TBinding>
            where TBinding : AbstractEventBinding, new()
        {
            private readonly Stack<TBinding> _stack = new();

            public TBinding Get() => _stack.TryPop(out var binding) ? binding : new TBinding();
            public void Dispose(AbstractEventBinding binding) => _stack.Push((TBinding)binding);
        }

        private static readonly Dictionary<Type, IPoolHolder<AbstractEventBinding>> _typeToPoolHolder = new();

        public static TBinding Get<TBinding>()
            where TBinding : AbstractEventBinding, new()
        {
            var type = typeof(TBinding);
            if (!_typeToPoolHolder.TryGetValue(type, out var holder))
            {
                var bindingHolder = new PoolHolder<TBinding>();
                _typeToPoolHolder.Add(type, bindingHolder);
                return bindingHolder.Get();
            }

            return (TBinding)holder.Get();
        }

        public static void Release(AbstractEventBinding abstractEventBinding)
        {
            if (_typeToPoolHolder.TryGetValue(abstractEventBinding.GetType(), out var holder))
            {
                holder.Dispose(abstractEventBinding);
            }
        }
    }
}
