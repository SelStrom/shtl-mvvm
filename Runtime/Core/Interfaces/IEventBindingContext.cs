namespace Shtl.Mvvm
{
    public interface IEventBindingContext
    {
        //TODO @a.shatalov: pass something keyable instead of object
        TBinding AddBinding<TBinding>(object bindingKey, TBinding binding) where TBinding : AbstractEventBinding;
        void CleanUp();
    }
}
