using System;
using System.Collections.Generic;

namespace Shtl.Mvvm
{
    public class EventBindingContext : IEventBindingContext
    {
        private readonly Dictionary<object, AbstractEventBinding> _keyToBinding = new();

        public TBinding AddBinding<TBinding>(object bindingKey, TBinding binding)
            where TBinding : AbstractEventBinding
        {
            if (!_keyToBinding.TryAdd(bindingKey, binding))
            {
                throw new Exception($"The binding has already exists key: {bindingKey.ToString()}");
            }

            return binding;
        }

        public void InvokeAll()
        {
            foreach (var binding in _keyToBinding.Values)
            {
                binding.Invoke();
            }
        }

        public void CleanUp()
        {
            foreach (var binding in _keyToBinding.Values)
            {
                binding.Dispose();
                BindingPool.Release(binding);
            }

            _keyToBinding.Clear();
        }
    }
}
