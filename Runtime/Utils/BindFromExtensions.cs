namespace Shtl.Mvvm
{
    public static class BindFromExtensions
    {
        public static BindFrom<TSource> From<TSource>(this IEventBindingContext ctx, TSource source)
            => new(source, ctx);

        public static BindFrom<TSource>? FromUnsafe<TSource>(this IEventBindingContext ctx, TSource source)
            => source != null ? new BindFrom<TSource>(source, ctx) : null;
    }
}
