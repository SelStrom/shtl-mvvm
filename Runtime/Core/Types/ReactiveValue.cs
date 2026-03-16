using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Shtl.Mvvm
{
    [NotNull]
    public class ReactiveValue<TValue> : IReactiveValue
    {
        private TValue _value;

        private Action<TValue> _onChanged;

        public TValue Value
        {
            get => _value;
            set
            {
                if (!EqualityComparer<TValue>.Default.Equals(_value, value))
                {
                    _value = value;
                    _onChanged?.Invoke(value);
                }
            }
        }

        public ReactiveValue(TValue initValue)
        {
            _value = initValue;
        }

        public ReactiveValue() { }

        public void Connect(Action<TValue> onChanged)
        {
            var bound = _onChanged != null;
            if (bound)
            {
                throw new InvalidOperationException("Already bound");
            }

            _onChanged = onChanged;
            if (typeof(TValue).IsValueType || !EqualityComparer<TValue>.Default.Equals(_value, default(TValue)))
            {
                _onChanged.Invoke(Value);
            }
        }

        public void Unbind()
        {
            _onChanged = default;
        }

        public void Dispose()
        {
            Unbind();
            _value = default;
        }

        public override string ToString() => $"'{_value?.ToString()}'";
    }
}
