using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace Shtl.Mvvm
{
    public static class ViewModelToUIEventBindingsExtensions
    {
        public static void To<TViewModel, TWidgetView>(
            this BindFrom<ReactiveList<TViewModel>> from,
            List<TWidgetView> widgets,
            TWidgetView original,
            Transform parent
        )
            where TViewModel : AbstractViewModel, new()
            where TWidgetView : AbstractWidgetView<TViewModel>, new()
        {
            var binding = ElementCollectionBinding<TViewModel, TWidgetView>.GetOrCreate()
                .Connect(from.Source, widgets, original, parent);
            from.LinkTo(binding);
        }

        public static void To<TViewModel, TWidgetView>(
            this BindFrom<ReactiveList<TViewModel>> from,
            List<TWidgetView> widgets,
            IWidgetViewFactory<TViewModel, TWidgetView> factory
        )
            where TViewModel : AbstractViewModel, new()
            where TWidgetView : AbstractWidgetView<TViewModel>, new()
        {
            var binding = ElementCollectionBinding<TViewModel, TWidgetView>.GetOrCreate()
                .Connect(from.Source, widgets, factory);
            from.LinkTo(binding);
        }

        public static void To<TViewModel>(this BindFrom<TViewModel> from, AbstractWidgetView<TViewModel> view)
            where TViewModel : AbstractViewModel, new() =>
            from.LinkTo(WidgetViewBinding<TViewModel>.GetOrCreate().Connect(from.Source, view));

        public static void To(this BindFrom<ReactiveValue<string>> from, TMP_Text view) =>
            from.Source.Connect(value => view.text = value);

        public static void To(this BindFrom<ReactiveValue<int>> from, TMP_Text view) =>
            from.Source.Connect(value => view.SetText("{0}", value));

        public static void To(this BindFrom<ReactiveValue<long>> from, TMP_Text view) =>
            from.Source.Connect(value => view.SetText("{0}", value));

        public static void To(this BindFrom<ReactiveValue<Color>> from, TMP_Text view) =>
            from.Source.Connect(value => view.color = value);

        public static void To(this BindFrom<ReactiveValue<bool>> from, GameObject view) =>
            from.Source.Connect(view.SetActive);

        public static void To(this BindFrom<ReactiveValue<int>> from, RectTransform view) =>
            from.Source.Connect(view.SetSiblingIndex);
    }
}
