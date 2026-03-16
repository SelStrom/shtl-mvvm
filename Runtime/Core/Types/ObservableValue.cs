using System;
using System.Collections.Generic;

namespace Shtl.Mvvm
{
    public class ObservableValue<T> : IObservableValue<T>
    {
        public event Action<T> OnChanged;

        private T _value;

        public T Value
        {
            get => _value;
            set
            {
                if (!EqualityComparer<T>.Default.Equals(_value, value))
                {
                    _value = value;
                    OnChanged?.Invoke(value);
                }
            }
        }

        public ObservableValue(T initial)
        {
            _value = initial;
            OnChanged = null;
        }
    }
}
