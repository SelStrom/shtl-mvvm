using System.Collections.Generic;
using UnityEngine;

namespace Shtl.Mvvm
{
    public class ElementCollectionBinding<TViewModel, TWidgetView> : AbstractEventBinding<ElementCollectionBinding<TViewModel, TWidgetView>>
        where TViewModel : AbstractViewModel, new()
        where TWidgetView : AbstractWidgetView<TViewModel>, new()
    {
        private List<TWidgetView> _widgets;
        private ReactiveList<TViewModel> _vmList;

        private TWidgetView _original;
        private Transform _parent;

        private IWidgetViewFactory<TViewModel, TWidgetView> _factory;

        public ElementCollectionBinding<TViewModel, TWidgetView> Connect(ReactiveList<TViewModel> vmList, List<TWidgetView> widgets, TWidgetView original, Transform parent)
        {
            _parent = parent;
            _original = original;
            _vmList = vmList;
            _widgets = widgets;

            return this;
        }

        public ElementCollectionBinding<TViewModel, TWidgetView> Connect(ReactiveList<TViewModel> vmList, List<TWidgetView> widgets, IWidgetViewFactory<TViewModel, TWidgetView> factory)
        {
            _vmList = vmList;
            _widgets = widgets;
            _factory = factory;

            return this;
        }

        public override void Activate()
        {
            _vmList.Connect(
                onContentChanged : OnContentChanged,
                onElementAdded : OnElementAdded,
                onElementReplaced : OnElementReplaced,
                onElementRemoved : OnElementRemoved
            );
        }

        private void OnContentChanged(ReactiveList<TViewModel> vmList)
        {
            var vmListCount = vmList.Count;

            while (_widgets.Count > vmListCount)
            {
                OnElementRemoved(_widgets.Count - 1, null);
            }

            for (var i = 0; i < _widgets.Count; i++)
            {
                OnElementReplaced(i, vmList[i]);
            }

            while (_widgets.Count < vmListCount)
            {
                var index = _widgets.Count;
                OnElementAdded(index, vmList[index]);
            }
        }

        private void OnElementAdded(int index, TViewModel element)
        {
            if (index < _widgets.Count)
            {
                OnElementReplaced(index, element);
            }
            else
            {
                var view = CreateWidget(element);
                view.Connect(element);

                _widgets.Add(view);
            }
        }

        private void OnElementReplaced(int index, TViewModel element)
        {
            var view = _widgets[index];
            if (view.ViewModel == element)
            {
                return;
            }

            _widgets[index].Connect(element);
        }

        private void OnElementRemoved(int index, TViewModel element)
        {
            var view = _widgets[index];
            // Only unbinds the view model, does not destroy it
            view.Dispose();
            _widgets.Remove(view);
            RemoveWidget(view);
        }

        private TWidgetView CreateWidget(TViewModel viewModel)
        {
            return _factory != null
                ? _factory.CreateWidget(viewModel)
                : Object.Instantiate(_original, _parent);
        }

        private void RemoveWidget(TWidgetView view)
        {
            if (_factory != null)
            {
                _factory.RemoveWidget(view);
            }
            else
            {
                Object.Destroy(view.gameObject);
            }
        }

        public override void Invoke()
        {
        }

        public override void Dispose()
        {
            foreach (var view in _widgets)
            {
                view.Dispose();
            }

            _widgets = null;
            _vmList = null;
            _original = default;
            _parent = null;
            _factory = null;
        }
    }
}
