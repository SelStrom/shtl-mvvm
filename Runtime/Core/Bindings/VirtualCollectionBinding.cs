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
                $"{nameof(VirtualScrollRect)}.Viewport is required — assign a RectTransform with RectMask2D");

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
            // Mirror the prefab overload: without Viewport, ViewportSize=0 and
            // FindVisibleRange returns an empty range -- a silent failure.
            Assert.IsNotNull(scrollRect, $"{nameof(VirtualScrollRect)} is required");
            Assert.IsNotNull(scrollRect.Viewport,
                $"{nameof(VirtualScrollRect)}.Viewport is required — assign a RectTransform with RectMask2D");

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

            // B-7: when adding the FIRST element to an empty list, initialise fixed-mode.
            // Without this, the first Add lands in the InsertAt branch (because _isFixedLayout=false by default),
            // and LayoutCalculator permanently degrades into variable-mode -- even if every subsequent
            // element has the same height.
            if (count == 1)
            {
                _isFixedLayout = true;
                _fixedItemHeight = newHeight;
            }

            if (_isFixedLayout && Math.Abs(newHeight - _fixedItemHeight) < 0.001f)
            {
                // O(N) rebuild of prefix-sum, but fixed-mode is preserved -> O(1) indexing still applies.
                _layoutCalculator.Rebuild(count, _fixedItemHeight);
            }
            else
            {
                _layoutCalculator.InsertAt(index, count, GetHeightProvider());
                _isFixedLayout = false;
                _fixedItemHeight = 0f;
            }

            // Shift indices in _activeViews BEFORE SetContentSize/ScrollPosition: both trigger
            // OnScrollPositionChanged → UpdateVisibleRange, and if _activeViews still holds the
            // old keys, the View falls into "out-of-range" and is Released, then handed out from
            // the pool again when the new active range is created -- this leads to a duplicate
            // OnDisposed (double-dispose).
            ShiftActiveViewIndices(index, 1);

            _scrollRect.SetContentSize(_layoutCalculator.TotalHeight);

            // Adjust scroll position when inserting above the viewport.
            // stride = height + spacing (see the prefix-sum invariant in LayoutCalculator):
            // every element to the right of index shifts by exactly this delta.
            if (index < _currentRange.FirstIndex)
            {
                _scrollRect.ScrollPosition += newHeight + _scrollRect.Spacing;
            }

            UpdateVisibleRange();
        }

        private void OnElementRemoved(int index, TViewModel element)
        {
            var heightOfRemoved = _layoutCalculator.GetItemHeight(index);

            // If the removed element has an active View, release it.
            if (_activeViews.TryGetValue(index, out var view))
            {
                _recyclingPool.Release(view);
                _activeViews.Remove(index);
            }

            var count = _vmList.Count;
            _layoutCalculator.SetSpacing(_scrollRect.Spacing);

            if (_isFixedLayout)
            {
                // All remaining elements still have the same height -> fixed-path is preserved.
                _layoutCalculator.Rebuild(count, _fixedItemHeight);
            }
            else
            {
                _layoutCalculator.RemoveAt(index, count, GetHeightProvider());
            }

            // Shift indices in _activeViews BEFORE SetContentSize/ScrollPosition (see OnElementAdded
            // above for the rationale).
            ShiftActiveViewIndices(index + 1, -1);

            _scrollRect.SetContentSize(_layoutCalculator.TotalHeight);

            // Adjust scroll position when removing above the viewport.
            // Mirroring OnElementAdded: stride = height + spacing.
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
                // Per Pitfall 3: Dispose before Connect.
                view.Dispose();
                view.Connect(element);
            }

            var newHeight = _vmList.GetItemHeight(index);
            _layoutCalculator.SetSpacing(_scrollRect.Spacing);

            if (_isFixedLayout && Math.Abs(newHeight - _fixedItemHeight) < 0.001f)
            {
                // Height did not change -- prefix-sum stays valid, no need to recompute layout.
            }
            else
            {
                // Incremental tail recompute: O(N - index) instead of O(N) for a full rebuild.
                _layoutCalculator.UpdateAt(index, _vmList.Count, GetHeightProvider());
                _isFixedLayout = false;
                _fixedItemHeight = 0f;
            }

            _scrollRect.SetContentSize(_layoutCalculator.TotalHeight);
            UpdateVisibleRange();
        }

        private void OnScrollPositionChanged(float scrollPosition)
        {
            // W-06: the contract of ReactiveVirtualList.ScrollPosition is one-way **read** of the
            // scroll position from the binding. A user write to ScrollPosition.Value does NOT
            // move the scroll: VirtualScrollRect is not subscribed to the ReactiveValue. If
            // imperative scrolling from user code is needed, use VirtualScrollRect directly
            // (internal ScrollTo / ScrollToIndex API), or introduce a bidirectional channel
            // later (would require extending ReactiveValue to multi-subscriber).
            _vmList.ScrollPosition.Value = scrollPosition;
            UpdateVisibleRange();
        }

        private void UpdateVisibleRange()
        {
            if (_vmList.Count == 0)
            {
                // Release every active View.
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

            // Determine which elements left the visible range.
            _indicesToRemove.Clear();
            foreach (var kvp in _activeViews)
            {
                if (kvp.Key < newRange.FirstIndex || kvp.Key > newRange.LastIndex)
                {
                    _indicesToRemove.Add(kvp.Key);
                }
            }

            // Release Views that fell out of the range.
            for (var i = 0; i < _indicesToRemove.Count; i++)
            {
                var idx = _indicesToRemove[i];
                _recyclingPool.Release(_activeViews[idx]);
                _activeViews.Remove(idx);
            }

            // Create Views for newly visible elements in the range.
            for (var idx = newRange.FirstIndex; idx <= newRange.LastIndex; idx++)
            {
                if (_activeViews.TryGetValue(idx, out var activeView))
                {
                    // Update the position of the existing View.
                    PositionView(activeView, idx, scrollPosition);
                    continue;
                }

                var view = _recyclingPool.Get();
                view.Connect(_vmList[idx]);
                // Anchors/pivot depend only on Axis (fixed after binding Connect),
                // so set them once when leaving the pool rather than every scroll tick.
                ConfigureViewAnchors(view);
                PositionView(view, idx, scrollPosition);
                _activeViews[idx] = view;
            }

            _currentRange = newRange;
            _vmList.FirstVisibleIndex.Value = newRange.FirstIndex;
            _vmList.VisibleCount.Value = newRange.Count;
        }

        // Anchors/pivot depend only on Axis -- constants for the entire binding lifetime.
        // Set them once when a view leaves the pool to avoid hitting Unity native-side dirtyflags
        // every scroll tick.
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
                // Stretch to viewport width, fix height to the item size.
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
            }
            else
            {
                // Stretch to viewport height, fix width to the item size.
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

            // Sort to shift safely (when shift > 0 process from the tail, when shift < 0 from the head).
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

            // Check whether all items share the same height.
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
            // B-5: detach the callback BEFORE nulling _scrollRect so subsequent drag/wheel/setter
            // calls don't poke a binding whose fields are null.
            _scrollRect?.SetOnScrollPositionChanged(null);

            // B-4: Unbind enables a fresh Connect to Items after the binding is Disposed.
            _vmList?.Items.Unbind();

            // B-3: Release itself calls view.Dispose() (see ViewRecyclingPool.Release);
            // an extra explicit Dispose would cause a double-dispose.
            foreach (var kvp in _activeViews)
            {
                _recyclingPool?.Release(kvp.Value);
            }

            _activeViews.Clear();

            _recyclingPool?.DisposeAll();

            // Reset fixed-mode flags: bindings are reused through BindingPool.
            _isFixedLayout = false;
            _fixedItemHeight = 0f;

            _vmList = null;
            _scrollRect = null;
            _recyclingPool = null;
        }
    }
}
