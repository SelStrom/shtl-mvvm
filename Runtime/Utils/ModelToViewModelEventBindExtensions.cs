using System;


namespace Shtl.Mvvm
{
    public static class ModelToViewModelEventBindingsExtensions
    {
        public static void To<TSource>(
            this BindFrom<ObservableValue<TSource>> from,
            ReactiveValue<TSource> vmParam
        ) =>
            from.To(vmParam, (src, dest) => dest.Value = src);

        public static void To<TSource, TContext>(
            this BindFrom<ObservableValue<TSource>> from,
            TContext context,
            Action<TSource, TContext> action
        )
        {
            var binding = ObservableValueEventBinding<TSource, TContext>.GetOrCreate()
                .SetCallBack(action)
                .Connect(from.Source, context);
            from.LinkTo(binding);
        }

        public static void To<TSource>(
            this BindFrom<ObservableValue<TSource>> from,
            Action<TSource> action
        )
        {
            var binding = ObservableValueEventBinding<TSource>.GetOrCreate()
                .Connect(from.Source, action);
            from.LinkTo(binding);
        }
    }
}
