namespace Shtl.Mvvm
{
    public readonly struct BindFrom<TSource>
    {
        public readonly TSource Source;
        private readonly IEventBindingContext _ctx;

        public BindFrom(TSource source, IEventBindingContext ctx)
        {
            Source = source;
            _ctx = ctx;
        }

        public void LinkTo(AbstractEventBinding binding)
        {
            _ctx.AddBinding(Source, binding);
            binding.Activate();
        }
    }
}
