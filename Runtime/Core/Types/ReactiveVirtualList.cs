using System;
using System.Runtime.CompilerServices;

namespace Shtl.Mvvm
{
    public class ReactiveVirtualList<TElement> : IReactiveValue
        where TElement : AbstractViewModel, new()
    {
        private readonly float _fixedHeight;
        private readonly Func<int, float> _heightProvider;

        public readonly ReactiveList<TElement> Items = new();
        public readonly ReactiveValue<float> ScrollPosition = new(0f);
        public readonly ReactiveValue<int> FirstVisibleIndex = new(0);
        public readonly ReactiveValue<int> VisibleCount = new(0);

        public ReactiveVirtualList(float fixedHeight)
        {
            _fixedHeight = fixedHeight;
            _heightProvider = null;
        }

        public ReactiveVirtualList(Func<int, float> heightProvider)
        {
            _fixedHeight = 0f;
            _heightProvider = heightProvider;
        }

        public ReactiveVirtualList()
        {
            _fixedHeight = 0f;
            _heightProvider = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetItemHeight(int index)
        {
            if (_heightProvider != null)
            {
                var value = _heightProvider(index);
                if (value <= 0f)
                {
                    throw new InvalidOperationException(
                        $"Height provider returned non-positive value {value} for index {index}");
                }

                return value;
            }

            if (_fixedHeight <= 0f)
            {
                // Parameterless конструктор оставляет _fixedHeight=0 и _heightProvider=null.
                // Тихий return 0 ломает виртуализацию: TotalHeight=0, FindVisibleRange отдаёт весь
                // список. Бросаем явно, чтобы ошибка проявилась в первом же вызове, а не позже.
                throw new InvalidOperationException(
                    "ReactiveVirtualList constructed without fixed height or height provider; cannot resolve item height.");
            }

            return _fixedHeight;
        }

        public void Add(TElement item) => Items.Add(item);

        public void RemoveAt(int index) => Items.RemoveAt(index);

        public void Clear() => Items.Clear();

        public int Count => Items.Count;

        public TElement this[int index] => Items[index];

        public void Dispose()
        {
            ((IReactiveValue)Items).Dispose();
            ScrollPosition.Dispose();
            FirstVisibleIndex.Dispose();
            VisibleCount.Dispose();
        }

        public void Unbind()
        {
            Items.Unbind();
            ScrollPosition.Unbind();
            FirstVisibleIndex.Unbind();
            VisibleCount.Unbind();
        }
    }
}
