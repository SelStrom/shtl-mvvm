using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Shtl.Mvvm
{
    internal class ViewRecyclingPool<TViewModel, TWidgetView>
        where TViewModel : AbstractViewModel, new()
        where TWidgetView : AbstractWidgetView<TViewModel>, new()
    {
        private readonly Stack<TWidgetView> _pool = new();
        private readonly IWidgetViewFactory<TViewModel, TWidgetView> _factory;
        private readonly TWidgetView _prefab;
        private readonly Transform _parent;

        public ViewRecyclingPool(IWidgetViewFactory<TViewModel, TWidgetView> factory)
        {
            _factory = factory;
        }

        public ViewRecyclingPool(TWidgetView prefab, Transform parent)
        {
            _prefab = prefab;
            _parent = parent;
        }

        public int Count => _pool.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TWidgetView Get()
        {
            if (_pool.Count > 0)
            {
                var view = _pool.Pop();
                view.gameObject.SetActive(true);
                return view;
            }

            return _factory != null
                ? _factory.CreateWidget(default)
                : Object.Instantiate(_prefab, _parent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release(TWidgetView view)
        {
            view.Dispose();
            view.gameObject.SetActive(false);
            _pool.Push(view);
        }

        public void DisposeAll()
        {
            while (_pool.Count > 0)
            {
                var view = _pool.Pop();
                if (_factory != null)
                {
                    _factory.RemoveWidget(view);
                }
                else
                {
                    Object.Destroy(view.gameObject);
                }
            }
        }
    }
}
