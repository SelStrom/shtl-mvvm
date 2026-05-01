namespace Shtl.Mvvm
{
    public interface IWidgetViewFactory<in TViewModel, TWidgetView>
        where TViewModel : AbstractViewModel, new()
        where TWidgetView : AbstractWidgetView<TViewModel>, new()
    {
        // CreateWidget is invoked by ViewRecyclingPool when the pool is empty, including BEFORE
        // the first Connect(viewModel). Implementations must correctly handle
        // viewModel == default (null for reference types): the actual ViewModel binding
        // is performed by a separate view.Connect(...) call after Get().
        TWidgetView CreateWidget(TViewModel viewModel);
        void RemoveWidget(TWidgetView view);
    }
}
