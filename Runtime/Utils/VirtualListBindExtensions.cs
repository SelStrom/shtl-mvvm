using System;

namespace Shtl.Mvvm
{
    public static class VirtualListBindExtensions
    {
        public static void To<TViewModel, TWidgetView>(
            this BindFrom<ReactiveVirtualList<TViewModel>> from,
            TWidgetView prefab,
            VirtualScrollRect scrollRect
        )
            where TViewModel : AbstractViewModel, new()
            where TWidgetView : AbstractWidgetView<TViewModel>, new()
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab), "Prefab cannot be null");
            }

            if (scrollRect == null)
            {
                throw new ArgumentNullException(nameof(scrollRect), "VirtualScrollRect cannot be null");
            }

            var binding = VirtualCollectionBinding<TViewModel, TWidgetView>.GetOrCreate()
                .Connect(from.Source, prefab, scrollRect);
            from.LinkTo(binding);
        }

        public static void To<TViewModel, TWidgetView>(
            this BindFrom<ReactiveVirtualList<TViewModel>> from,
            IWidgetViewFactory<TViewModel, TWidgetView> factory,
            VirtualScrollRect scrollRect
        )
            where TViewModel : AbstractViewModel, new()
            where TWidgetView : AbstractWidgetView<TViewModel>, new()
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (scrollRect == null)
            {
                throw new ArgumentNullException(nameof(scrollRect), "VirtualScrollRect cannot be null");
            }

            var binding = VirtualCollectionBinding<TViewModel, TWidgetView>.GetOrCreate()
                .Connect(from.Source, factory, scrollRect);
            from.LinkTo(binding);
        }
    }
}
