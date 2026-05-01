using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Assertions;

namespace Shtl.Mvvm
{
    public class VirtualCollectionBinding<TViewModel, TWidgetView> :
        AbstractEventBinding<VirtualCollectionBinding<TViewModel, TWidgetView>>
        where TViewModel : AbstractViewModel, new()
        where TWidgetView : AbstractWidgetView<TViewModel>, new()
    {
        private ReactiveVirtualList<TViewModel> _vmList;
        private VirtualScrollRect _scrollRect;
        private LayoutCalculator _layoutCalculator;
        private ViewRecyclingPool<TViewModel, TWidgetView> _recyclingPool;
        private readonly Dictionary<int, TWidgetView> _activeViews = new();
        private VisibleRange _currentRange;
        private readonly List<int> _indicesToRemove = new();
        private bool _isFixedLayout;
        private float _fixedItemHeight;

        public VirtualCollectionBinding<TViewModel, TWidgetView> Connect(
            ReactiveVirtualList<TViewModel> vmList,
            TWidgetView prefab,
            VirtualScrollRect scrollRect)
        {
            Assert.IsNotNull(scrollRect.Viewport,
                $"{nameof(VirtualScrollRect)}.Viewport обязателен — назначь RectTransform с RectMask2D");

            _vmList = vmList;
            _scrollRect = scrollRect;
            _recyclingPool = new ViewRecyclingPool<TViewModel, TWidgetView>(prefab, scrollRect.Viewport);

            return this;
        }

        public VirtualCollectionBinding<TViewModel, TWidgetView> Connect(
            ReactiveVirtualList<TViewModel> vmList,
            IWidgetViewFactory<TViewModel, TWidgetView> factory,
            VirtualScrollRect scrollRect)
        {
            // Симметрично prefab-перегрузке: без Viewport ViewportSize=0,
            // FindVisibleRange отдаёт пустой диапазон -- безмолвный сбой.
            Assert.IsNotNull(scrollRect, $"{nameof(VirtualScrollRect)} обязателен");
            Assert.IsNotNull(scrollRect.Viewport,
                $"{nameof(VirtualScrollRect)}.Viewport обязателен — назначь RectTransform с RectMask2D");

            _vmList = vmList;
            _scrollRect = scrollRect;
            _recyclingPool = new ViewRecyclingPool<TViewModel, TWidgetView>(factory);

            return this;
        }

        public override void Activate()
        {
            _layoutCalculator = new LayoutCalculator();

            _scrollRect.SetOnScrollPositionChanged(OnScrollPositionChanged);

            _vmList.Items.Connect(
                onContentChanged: OnContentChanged,
                onElementAdded: OnElementAdded,
                onElementReplaced: OnElementReplaced,
                onElementRemoved: OnElementRemoved
            );
        }

        private void OnContentChanged(ReactiveList<TViewModel> list)
        {
            var count = list.Count;
            RebuildLayout(count);
            _scrollRect.SetContentSize(_layoutCalculator.TotalHeight);
            UpdateVisibleRange();
        }

        private void OnElementAdded(int index, TViewModel element)
        {
            var count = _vmList.Count;
            _layoutCalculator.SetSpacing(_scrollRect.Spacing);

            var newHeight = _vmList.GetItemHeight(index);

            // B-7: при добавлении ПЕРВОГО элемента в пустой список инициализируем fixed-mode.
            // Без этого первый Add попадает в InsertAt-ветку (т.к. _isFixedLayout=false по умолчанию),
            // и LayoutCalculator навсегда деградирует в variable-mode -- даже если все последующие
            // элементы той же высоты.
            if (count == 1)
            {
                _isFixedLayout = true;
                _fixedItemHeight = newHeight;
            }

            if (_isFixedLayout && Math.Abs(newHeight - _fixedItemHeight) < 0.001f)
            {
                // O(N) rebuild prefix-sum, но fixed-mode сохраняется -> O(1) индексация остаётся.
                _layoutCalculator.Rebuild(count, _fixedItemHeight);
            }
            else
            {
                _layoutCalculator.InsertAt(index, count, GetHeightProvider());
                _isFixedLayout = false;
                _fixedItemHeight = 0f;
            }

            // Сдвигаем индексы в _activeViews ДО SetContentSize/ScrollPosition: оба триггерят
            // OnScrollPositionChanged → UpdateVisibleRange, и если _activeViews ещё содержит
            // старые ключи, View попадёт в "out-of-range" и будет Release'нут, а потом снова
            // выдан из пула при создании нового активного диапазона -- это приводит к
            // повторному OnDisposed (double-dispose).
            ShiftActiveViewIndices(index, 1);

            _scrollRect.SetContentSize(_layoutCalculator.TotalHeight);

            // Корректировка scroll position при добавлении выше viewport.
            // stride = height + spacing (см. инвариант prefix-sum в LayoutCalculator):
            // все элементы справа от index сдвигаются на ровно эту величину.
            if (index < _currentRange.FirstIndex)
            {
                _scrollRect.ScrollPosition += newHeight + _scrollRect.Spacing;
            }

            UpdateVisibleRange();
        }

        private void OnElementRemoved(int index, TViewModel element)
        {
            var heightOfRemoved = _layoutCalculator.GetItemHeight(index);

            // Если удалённый элемент имеет активный View -- освобождаем
            if (_activeViews.TryGetValue(index, out var view))
            {
                _recyclingPool.Release(view);
                _activeViews.Remove(index);
            }

            var count = _vmList.Count;
            _layoutCalculator.SetSpacing(_scrollRect.Spacing);

            if (_isFixedLayout)
            {
                // Все оставшиеся элементы по-прежнему той же высоты -> fixed-path сохраняется.
                _layoutCalculator.Rebuild(count, _fixedItemHeight);
            }
            else
            {
                _layoutCalculator.RemoveAt(index, count, GetHeightProvider());
            }

            // Сдвигаем индексы в _activeViews ДО SetContentSize/ScrollPosition (см. OnElementAdded
            // выше для обоснования).
            ShiftActiveViewIndices(index + 1, -1);

            _scrollRect.SetContentSize(_layoutCalculator.TotalHeight);

            // Корректировка scroll position при удалении выше viewport.
            // Симметрично OnElementAdded: stride = height + spacing.
            if (index < _currentRange.FirstIndex)
            {
                _scrollRect.ScrollPosition -= heightOfRemoved + _scrollRect.Spacing;
            }

            UpdateVisibleRange();
        }

        private void OnElementReplaced(int index, TViewModel element)
        {
            if (_activeViews.TryGetValue(index, out var view))
            {
                // Per Pitfall 3: Dispose перед Connect
                view.Dispose();
                view.Connect(element);
            }

            var newHeight = _vmList.GetItemHeight(index);
            _layoutCalculator.SetSpacing(_scrollRect.Spacing);

            if (_isFixedLayout && Math.Abs(newHeight - _fixedItemHeight) < 0.001f)
            {
                // Высота не изменилась -- prefix-sum остаётся валидным, layout пересчитывать не нужно.
            }
            else
            {
                // Инкрементальный пересчёт хвоста: O(N - index) вместо O(N) на полный rebuild.
                _layoutCalculator.UpdateAt(index, _vmList.Count, GetHeightProvider());
                _isFixedLayout = false;
                _fixedItemHeight = 0f;
            }

            _scrollRect.SetContentSize(_layoutCalculator.TotalHeight);
            UpdateVisibleRange();
        }

        private void OnScrollPositionChanged(float scrollPosition)
        {
            // W-06: контракт ReactiveVirtualList.ScrollPosition -- одностороннее **чтение**
            // позиции скролла из биндинга. Запись пользователем в ScrollPosition.Value НЕ
            // сдвигает scroll: VirtualScrollRect не подписывается на ReactiveValue. Если
            // потребуется императивный скролл из user-кода, использовать VirtualScrollRect
            // напрямую (внутренний API ScrollTo / ScrollToIndex), либо ввести двунаправленный
            // канал в будущем (потребует расширения ReactiveValue до multi-subscriber).
            _vmList.ScrollPosition.Value = scrollPosition;
            UpdateVisibleRange();
        }

        private void UpdateVisibleRange()
        {
            if (_vmList.Count == 0)
            {
                // Очищаем все активные Views
                foreach (var kvp in _activeViews)
                {
                    _recyclingPool.Release(kvp.Value);
                }

                _activeViews.Clear();
                _currentRange = new VisibleRange(0, 0, 0);
                _vmList.FirstVisibleIndex.Value = 0;
                _vmList.VisibleCount.Value = 0;
                return;
            }

            var scrollPosition = _scrollRect.ScrollPosition;

            var newRange = _layoutCalculator.FindVisibleRange(
                scrollPosition,
                _scrollRect.ViewportSize,
                _scrollRect.OverscanCount);

            // Определить элементы, вышедшие из диапазона
            _indicesToRemove.Clear();
            foreach (var kvp in _activeViews)
            {
                if (kvp.Key < newRange.FirstIndex || kvp.Key > newRange.LastIndex)
                {
                    _indicesToRemove.Add(kvp.Key);
                }
            }

            // Освобождаем вышедшие из диапазона View
            for (var i = 0; i < _indicesToRemove.Count; i++)
            {
                var idx = _indicesToRemove[i];
                _recyclingPool.Release(_activeViews[idx]);
                _activeViews.Remove(idx);
            }

            // Создаём View для новых элементов в диапазоне
            for (var idx = newRange.FirstIndex; idx <= newRange.LastIndex; idx++)
            {
                if (_activeViews.TryGetValue(idx, out var activeView))
                {
                    // Обновляем позицию существующего View
                    PositionView(activeView, idx, scrollPosition);
                    continue;
                }

                var view = _recyclingPool.Get();
                view.Connect(_vmList[idx]);
                // Якоря/pivot зависят только от Axis (фиксирован после Connect биндинга),
                // поэтому ставим один раз при выдаче из пула, а не на каждом ticke скролла.
                ConfigureViewAnchors(view);
                PositionView(view, idx, scrollPosition);
                _activeViews[idx] = view;
            }

            _currentRange = newRange;
            _vmList.FirstVisibleIndex.Value = newRange.FirstIndex;
            _vmList.VisibleCount.Value = newRange.Count;
        }

        // Якоря/pivot зависят только от Axis -- константы для всего жизненного цикла биндинга.
        // Ставим один раз при выдаче view из пула, чтобы не дёргать Unity native-side dirtyflags
        // на каждом ticke скролла.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConfigureViewAnchors(TWidgetView view)
        {
            var rt = view.GetComponent<RectTransform>();
            if (rt == null)
            {
                return;
            }

            if (_scrollRect.Axis == ScrollAxis.Vertical)
            {
                // Растягиваем по ширине viewport, фиксируем высоту по элементу.
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
            }
            else
            {
                // Растягиваем по высоте viewport, фиксируем ширину по элементу.
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0f);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PositionView(TWidgetView view, int index, float scrollPosition)
        {
            var rt = view.GetComponent<RectTransform>();
            if (rt == null)
            {
                return;
            }

            var offset = _layoutCalculator.GetItemOffset(index);
            var size = _layoutCalculator.GetItemHeight(index);

            if (_scrollRect.Axis == ScrollAxis.Vertical)
            {
                rt.anchoredPosition = new Vector2(0f, -(offset - scrollPosition));
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, size);
            }
            else
            {
                rt.anchoredPosition = new Vector2(offset - scrollPosition, 0f);
                rt.sizeDelta = new Vector2(size, rt.sizeDelta.y);
            }
        }

        private void ShiftActiveViewIndices(int fromIndex, int shift)
        {
            _indicesToRemove.Clear();

            foreach (var kvp in _activeViews)
            {
                if (kvp.Key >= fromIndex)
                {
                    _indicesToRemove.Add(kvp.Key);
                }
            }

            // Сортируем для корректного сдвига (при shift > 0 -- с конца, при shift < 0 -- с начала)
            if (shift > 0)
            {
                _indicesToRemove.Sort((a, b) => b.CompareTo(a));
            }
            else
            {
                _indicesToRemove.Sort();
            }

            for (var i = 0; i < _indicesToRemove.Count; i++)
            {
                var oldKey = _indicesToRemove[i];
                var view = _activeViews[oldKey];
                _activeViews.Remove(oldKey);
                _activeViews[oldKey + shift] = view;
            }
        }

        private void RebuildLayout(int count)
        {
            _layoutCalculator.SetSpacing(_scrollRect.Spacing);

            if (count == 0)
            {
                _layoutCalculator.Rebuild(0, 1f);
                _isFixedLayout = false;
                _fixedItemHeight = 0f;
                return;
            }

            // Проверяем фиксированная ли высота
            var firstHeight = _vmList.GetItemHeight(0);
            var isFixed = true;
            for (var i = 1; i < count; i++)
            {
                if (Math.Abs(_vmList.GetItemHeight(i) - firstHeight) > 0.001f)
                {
                    isFixed = false;
                    break;
                }
            }

            if (isFixed)
            {
                _layoutCalculator.Rebuild(count, firstHeight);
                _isFixedLayout = true;
                _fixedItemHeight = firstHeight;
            }
            else
            {
                _layoutCalculator.Rebuild(count, GetHeightProvider());
                _isFixedLayout = false;
                _fixedItemHeight = 0f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Func<int, float> GetHeightProvider()
        {
            return _vmList.GetItemHeight;
        }

        public override void Invoke()
        {
        }

        public override void Dispose()
        {
            // B-5: отвязываем callback ДО обнуления _scrollRect, чтобы последующие drag/wheel/setter
            // не дёргали биндинг с null-полями.
            _scrollRect?.SetOnScrollPositionChanged(null);

            // B-4: Unbind разрешает повторный Connect к Items после Dispose биндинга.
            _vmList?.Items.Unbind();

            // B-3: Release сам делает view.Dispose() (см. ViewRecyclingPool.Release),
            // дополнительный явный Dispose приводил к double-dispose.
            foreach (var kvp in _activeViews)
            {
                _recyclingPool?.Release(kvp.Value);
            }

            _activeViews.Clear();

            _recyclingPool?.DisposeAll();

            // Сброс fixed-флагов: биндинги переиспользуются через BindingPool.
            _isFixedLayout = false;
            _fixedItemHeight = 0f;

            _vmList = null;
            _scrollRect = null;
            _recyclingPool = null;
        }
    }
}
