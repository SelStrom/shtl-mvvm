using System;
using System.Runtime.CompilerServices;

namespace Shtl.Mvvm
{
    internal readonly struct VisibleRange
    {
        public readonly int FirstIndex;
        public readonly int LastIndex;
        public readonly int Count;

        public VisibleRange(int firstIndex, int lastIndex, int count)
        {
            FirstIndex = firstIndex;
            LastIndex = lastIndex;
            Count = count;
        }
    }

    internal struct LayoutCalculator
    {
        private float[] _prefixHeights;
        private int _itemCount;
        private float _fixedHeight;
        private float _spacing;

        // Семантика prefix sum:
        //   _prefixHeights[i] = offset элемента i = sum_(k<i)(s_k) + i * _spacing
        //   _prefixHeights[i+1] - _prefixHeights[i] = s_i + _spacing (один stride, элемент + зазор после).
        //   TotalHeight исключает trailing spacing после последнего элемента: prefix[N] - _spacing.
        // При _spacing=0f индукция вырождается в классический prefix sum по размерам.

        public float TotalHeight
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_itemCount == 0)
                {
                    return 0f;
                }

                if (_fixedHeight > 0f)
                {
                    return _itemCount * _fixedHeight + (_itemCount - 1) * _spacing;
                }

                return _prefixHeights != null ? _prefixHeights[_itemCount] - _spacing : 0f;
            }
        }

        public void SetSpacing(float spacing)
        {
            _spacing = spacing;
        }

        public void Rebuild(int itemCount, float fixedHeight)
        {
            _itemCount = itemCount;
            _fixedHeight = fixedHeight;

            if (itemCount == 0)
            {
                return;
            }

            EnsureCapacity(itemCount);

            _prefixHeights[0] = 0f;
            for (var i = 0; i < itemCount; i++)
            {
                _prefixHeights[i + 1] = _prefixHeights[i] + fixedHeight + _spacing;
            }
        }

        public void Rebuild(int itemCount, Func<int, float> heightProvider)
        {
            _itemCount = itemCount;
            _fixedHeight = 0f;

            if (itemCount == 0)
            {
                return;
            }

            EnsureCapacity(itemCount);

            _prefixHeights[0] = 0f;
            for (var i = 0; i < itemCount; i++)
            {
                _prefixHeights[i + 1] = _prefixHeights[i] + heightProvider(i) + _spacing;
            }
        }

        public void InsertAt(int index, int newItemCount, Func<int, float> heightProvider)
        {
            _itemCount = newItemCount;
            _fixedHeight = 0f;

            EnsureCapacity(newItemCount);

            // Пересчитываем prefix sum начиная с index. prefix[index] остаётся корректным
            // (offset элемента index не зависит от элементов справа).
            for (var i = index; i < newItemCount; i++)
            {
                _prefixHeights[i + 1] = _prefixHeights[i] + heightProvider(i) + _spacing;
            }
        }

        public void RemoveAt(int index, int newItemCount, Func<int, float> heightProvider)
        {
            _itemCount = newItemCount;
            _fixedHeight = 0f;

            // Пересчитываем prefix sum начиная с index (см. комментарий в InsertAt).
            for (var i = index; i < newItemCount; i++)
            {
                _prefixHeights[i + 1] = _prefixHeights[i] + heightProvider(i) + _spacing;
            }
        }

        // Инкрементальный пересчёт хвоста prefix-sum при изменении высоты элемента index
        // (например, при Replace). Префикс [0..index] остаётся валидным и не пересчитывается.
        public void UpdateAt(int index, int itemCount, Func<int, float> heightProvider)
        {
            _itemCount = itemCount;
            _fixedHeight = 0f;

            EnsureCapacity(itemCount);

            for (var i = index; i < itemCount; i++)
            {
                _prefixHeights[i + 1] = _prefixHeights[i] + heightProvider(i) + _spacing;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetItemOffset(int index)
        {
            if (_fixedHeight > 0f)
            {
                return index * (_fixedHeight + _spacing);
            }

            if (_prefixHeights == null || index >= _prefixHeights.Length)
            {
                return 0f;
            }

            return _prefixHeights[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetItemHeight(int index)
        {
            if (_fixedHeight > 0f)
            {
                return _fixedHeight;
            }

            // prefix[i+1] - prefix[i] = s_i + spacing, поэтому raw-размер = разность минус spacing.
            return _prefixHeights[index + 1] - _prefixHeights[index] - _spacing;
        }

        public VisibleRange FindVisibleRange(float scrollPosition, float viewportHeight, int overscanCount)
        {
            if (_itemCount == 0)
            {
                return new VisibleRange(0, 0, 0);
            }

            var totalHeight = TotalHeight;

            // Clamp scroll position
            if (scrollPosition > totalHeight - viewportHeight)
            {
                scrollPosition = totalHeight - viewportHeight;
            }

            if (scrollPosition < 0f)
            {
                scrollPosition = 0f;
            }

            int firstVisible;
            int lastVisible;

            if (_fixedHeight > 0f)
            {
                // O(1) fast path для фиксированной высоты со spacing.
                // Stride = fixedHeight + spacing; при spacing=0 поведение идентично базовому.
                var stride = _fixedHeight + _spacing;
                if (stride <= 0f)
                {
                    firstVisible = 0;
                    lastVisible = _itemCount - 1;
                }
                else
                {
                    firstVisible = (int)(scrollPosition / stride);
                    var endPos = scrollPosition + viewportHeight;
                    lastVisible = (int)(endPos / stride);
                    // Симметрично binary-search ветке: если элемент начинается ровно на нижней
                    // границе viewport (endPos), он не виден -- сдвигаем на -1.
                    if (lastVisible * stride >= endPos)
                    {
                        lastVisible--;
                    }
                }
            }
            else
            {
                // Binary search для первого видимого элемента
                firstVisible = BinarySearchFirstVisible(scrollPosition);

                // Binary search для последнего видимого элемента.
                // BinarySearchFirstVisible возвращает первый индекс, чей prefix[i+1] > endPos,
                // т.е. элемент, выходящий за нижнюю границу. Если же prefix[i] == endPos,
                // элемент i начинается ровно на границе и не виден -- сдвигаем на -1.
                var endPos = scrollPosition + viewportHeight;
                var lastBoundary = BinarySearchFirstVisible(endPos);
                if (lastBoundary < _itemCount && _prefixHeights[lastBoundary] >= endPos)
                {
                    lastVisible = lastBoundary - 1;
                }
                else
                {
                    lastVisible = lastBoundary;
                }
            }

            // Clamp к границам
            if (firstVisible >= _itemCount)
            {
                firstVisible = _itemCount - 1;
            }

            if (lastVisible >= _itemCount)
            {
                lastVisible = _itemCount - 1;
            }

            // Применяем overscan
            firstVisible -= overscanCount;
            lastVisible += overscanCount;

            // Clamp после overscan
            if (firstVisible < 0)
            {
                firstVisible = 0;
            }

            if (lastVisible >= _itemCount)
            {
                lastVisible = _itemCount - 1;
            }

            var count = lastVisible - firstVisible + 1;

            return new VisibleRange(firstVisible, lastVisible, count);
        }

        private int BinarySearchFirstVisible(float scrollPosition)
        {
            var lo = 0;
            var hi = _itemCount;

            while (lo < hi)
            {
                var mid = lo + (hi - lo) / 2;
                if (_prefixHeights[mid + 1] <= scrollPosition)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            return lo;
        }

        private void EnsureCapacity(int itemCount)
        {
            var requiredLength = itemCount + 1;
            if (_prefixHeights == null || _prefixHeights.Length < requiredLength)
            {
                // Аллоцируем с запасом для уменьшения количества ресайзов
                var newCapacity = Math.Max(requiredLength, (_prefixHeights?.Length ?? 0) * 2);
                if (newCapacity < 16)
                {
                    newCapacity = 16;
                }

                Array.Resize(ref _prefixHeights, newCapacity);
            }
        }
    }
}
