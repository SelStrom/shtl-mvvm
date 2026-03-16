using UnityEngine;
using UnityEngine.Assertions;

namespace Shtl.Mvvm
{
    public abstract class AbstractWidgetView<TViewModel> : MonoBehaviour
        where TViewModel : AbstractViewModel, new()
    {
        public static string ViewModelPropertyName => nameof(ViewModel);

        public TViewModel ViewModel { get; private set; }

        private bool _initialized;

        private IEventBindingContext _bindingContext;
        protected IEventBindingContext Bind => _bindingContext ??= new EventBindingContext();

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }
            OnInitialized();
            _initialized = true;
        }

        public void Connect(TViewModel vm)
        {
            Assert.IsTrue(vm != ViewModel, "Something wrong. Connected view model must be not equals reset view model");

            Initialize();
            _bindingContext?.CleanUp();
            ViewModel?.Dispose();
            ViewModel = vm;
            OnConnected();
        }

        public void Dispose()
        {
            _bindingContext?.CleanUp();
            ViewModel?.Unbind();
            ViewModel = default;
            OnDisposed();
        }

        protected virtual void OnInitialized() { }
        protected abstract void OnConnected();
        protected virtual void OnDisposed() { }
    }
}
