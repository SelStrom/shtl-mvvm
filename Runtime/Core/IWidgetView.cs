namespace Shtl.Mvvm
{
    public interface IWidgetView<in TViewModel>
        where TViewModel : AbstractViewModel, new()
    {
        void Connect(TViewModel vm);
        void Dispose();
    }
}
