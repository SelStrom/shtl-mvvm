namespace Shtl.Mvvm
{
    public abstract class AbstractEventBinding
    {
        public abstract void Activate();
        public abstract void Invoke();
        public abstract void Dispose();
    }

    public abstract class AbstractEventBinding<TBinding> : AbstractEventBinding
        where TBinding : AbstractEventBinding, new()
    {
        public static TBinding GetOrCreate() => BindingPool.Get<TBinding>();
    }
}
