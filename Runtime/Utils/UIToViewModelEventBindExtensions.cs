using System;
using System.Collections.Generic;
using UnityEngine.UI;

namespace Shtl.Mvvm
{
    public static class UIToViewModelEventBindingsExtensions
    {
        public static void To(this BindFrom<Button> from, ReactiveValue<Action> vmAction)
        {
            var binding = ButtonEventBinding<Action>.GetOrCreate();
            vmAction.Connect(binding.OnActionValueChanged);
            binding.Connect(from.Source, action => action?.Invoke());
            from.LinkTo(binding);
        }

        public static void To(this BindFrom<Button> from, Action onButtonClicked)
        {
            var binding = ButtonEventSimpleBinding.GetOrCreate()
                .Connect(from.Source, onButtonClicked);
            from.LinkTo(binding);
        }

        public static void To<TActionContext>(
            this BindFrom<Button> from,
            TActionContext actionContext,
            Action<TActionContext> onButtonClicked
        )
        {
            var binding = ButtonEventBinding<TActionContext>.GetOrCreate()
                .Connect(from.Source, onButtonClicked)
                .SetContext(actionContext);
            from.LinkTo(binding);
        }

        public static void To(
            this BindFrom<IReadOnlyCollection<Button>> from,
            ReactiveValue<Action> vmAction
        )
        {
            var binding = ButtonCollectionEventBinding<Action>.GetOrCreate();
            vmAction.Connect(binding.OnActionValueChanged);
            binding.Connect(from.Source, action => action?.Invoke());
            from.LinkTo(binding);
        }

        public static void To(
            this BindFrom<Button[]> from,
            ReactiveValue<Action> vmAction
        )
        {
            var binding = ButtonCollectionEventBinding<Action>.GetOrCreate();
            vmAction.Connect(binding.OnActionValueChanged);
            binding.Connect(from.Source, action => action?.Invoke());
            from.LinkTo(binding);
        }
    }
}
