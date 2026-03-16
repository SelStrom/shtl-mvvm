namespace Shtl.Mvvm
{
    public interface IWidgetViewFactory<in TViewModel, TWidgetView>
        where TViewModel : AbstractViewModel, new()
        where TWidgetView : AbstractWidgetView<TViewModel>, new()
    {
        TWidgetView CreateWidget(TViewModel viewModel);
        void RemoveWidget(TWidgetView view);
    }
}
