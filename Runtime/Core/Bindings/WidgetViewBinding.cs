namespace Shtl.Mvvm
{
    public class WidgetViewBinding<TViewModel> : AbstractEventBinding<WidgetViewBinding<TViewModel>>
        where TViewModel : AbstractViewModel, new()
    {
        private AbstractWidgetView<TViewModel> _view;
        private TViewModel _src;

        public WidgetViewBinding<TViewModel> Connect(TViewModel src, AbstractWidgetView<TViewModel> view)
        {
            _src = src;
            _view = view;
            return this;
        }

        public override void Activate() => _view.Connect(_src);

        public override void Invoke()
        {
        }

        public override void Dispose()
        {
            _view.Dispose();
            _view = null;
            _src = null;
        }
    }
}
