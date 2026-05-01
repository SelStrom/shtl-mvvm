namespace Shtl.Mvvm
{
    public interface IWidgetViewFactory<in TViewModel, TWidgetView>
        where TViewModel : AbstractViewModel, new()
        where TWidgetView : AbstractWidgetView<TViewModel>, new()
    {
        // CreateWidget вызывается ViewRecyclingPool при пустом пуле, в т.ч. ДО
        // первого Connect(viewModel). Реализация обязана корректно обрабатывать
        // viewModel == default (null для reference types): фактическая привязка
        // ViewModel выполняется отдельным вызовом view.Connect(...) после Get().
        TWidgetView CreateWidget(TViewModel viewModel);
        void RemoveWidget(TWidgetView view);
    }
}
