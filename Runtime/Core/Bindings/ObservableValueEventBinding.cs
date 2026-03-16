using System;

namespace Shtl.Mvvm
{
    public class ObservableValueEventBinding<TSource, TContext> : AbstractEventBinding<ObservableValueEventBinding<TSource, TContext>>
    {
        private ObservableValue<TSource> _src;
        private TContext _context;
        private Action<TSource, TContext> _callBack;

        internal ObservableValueEventBinding<TSource, TContext> SetCallBack(Action<TSource, TContext> callBack)
        {
            _callBack = callBack;
            return this;
        }

        public ObservableValueEventBinding<TSource, TContext> Connect(
            ObservableValue<TSource> src,
            TContext context
        )
        {
            _src = src;
            _context = context;
            return this;
        }

        public override void Activate()
        {
            _src.OnChanged += OnSrcChanged;
        }

        public override void Invoke()
        {
            OnSrcChanged(_src.Value);
        }

        private void OnSrcChanged(TSource src)
        {
            _callBack?.Invoke(src, _context);
        }

        public override void Dispose()
        {
            _src.OnChanged -= OnSrcChanged;

            _src = default;
            _context = default;
            _callBack = null;
        }
    }

    public class ObservableValueEventBinding<TSource> : AbstractEventBinding<ObservableValueEventBinding<TSource>>
    {
        private ObservableValue<TSource> _src;
        private Action<TSource> _callBack;

        public ObservableValueEventBinding<TSource> Connect(ObservableValue<TSource> src, Action<TSource> callBack)
        {
            _src = src;
            _callBack = callBack;

            return this;
        }

        public override void Activate()
        {
            _src.OnChanged += OnSrcChanged;
        }

        public override void Invoke()
        {
            OnSrcChanged(default);
        }

        private void OnSrcChanged(TSource _)
        {
            _callBack?.Invoke(_src.Value);
        }

        public override void Dispose()
        {
            _src.OnChanged -= OnSrcChanged;

            _src = default;
            _callBack = null;
        }
    }
}
