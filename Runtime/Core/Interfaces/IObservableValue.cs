using System;

namespace Shtl.Mvvm
{
    public interface IObservableValue<out T>
    {
        event Action<T> OnChanged;
        T Value { get; }
    }
}
