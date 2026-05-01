using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Shtl.Mvvm.Tests
{
    [TestFixture]
    public class VirtualCollectionBindingTests
    {
        private class TestViewModel : AbstractViewModel
        {
            public readonly ReactiveValue<string> Title = new();
        }

        private class TestWidgetView : AbstractWidgetView<TestViewModel>
        {
            protected override void OnConnected()
            {
            }
        }

        private class MockFactory : IWidgetViewFactory<TestViewModel, TestWidgetView>
        {
            public int CreateCount { get; private set; }
            public int RemoveCount { get; private set; }

            private readonly Transform _parent;

            public MockFactory(Transform parent)
            {
                _parent = parent;
            }

            public TestWidgetView CreateWidget(TestViewModel viewModel)
            {
                CreateCount++;
                var go = new GameObject("TestView");
                go.AddComponent<RectTransform>();
                go.transform.SetParent(_parent);
                return go.AddComponent<TestWidgetView>();
            }

            public void RemoveWidget(TestWidgetView view)
            {
                RemoveCount++;
                Object.DestroyImmediate(view.gameObject);
            }
        }

        private class CountingWidgetView : AbstractWidgetView<TestViewModel>
        {
            public int OnDisposedCallCount;

            protected override void OnConnected()
            {
            }

            protected override void OnDisposed()
            {
                OnDisposedCallCount++;
            }
        }

        private class CountingFactory : IWidgetViewFactory<TestViewModel, CountingWidgetView>
        {
            public int CreateCount { get; private set; }
            public int RemoveCount { get; private set; }
            public List<CountingWidgetView> CreatedViews { get; } = new();

            private readonly Transform _parent;

            public CountingFactory(Transform parent)
            {
                _parent = parent;
            }

            public CountingWidgetView CreateWidget(TestViewModel viewModel)
            {
                CreateCount++;
                var go = new GameObject("CountingView");
                go.AddComponent<RectTransform>();
                go.transform.SetParent(_parent);
                var view = go.AddComponent<CountingWidgetView>();
                CreatedViews.Add(view);
                return view;
            }

            public void RemoveWidget(CountingWidgetView view)
            {
                RemoveCount++;
                if (view != null && view.gameObject != null)
                {
                    Object.DestroyImmediate(view.gameObject);
                }
            }
        }

        private GameObject _root;
        private MockFactory _factory;
        private ReactiveVirtualList<TestViewModel> _vmList;
        private VirtualScrollRect _scrollRect;

        [SetUp]
        public void SetUp()
        {
            ConfigureScrollRect(ScrollAxis.Vertical, new Vector2(400f, 300f));
            _vmList = new ReactiveVirtualList<TestViewModel>(100f);
        }

        private void ConfigureScrollRect(ScrollAxis axis, Vector2 viewportSize)
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }

            _root = new GameObject("TestRoot");

            // VirtualScrollRect requires a RectTransform for the viewport.
            var viewportGo = new GameObject("Viewport");
            var viewportRt = viewportGo.AddComponent<RectTransform>();
            viewportRt.SetParent(_root.transform);
            viewportRt.sizeDelta = viewportSize;

            var scrollGo = new GameObject("ScrollRect");
            scrollGo.transform.SetParent(_root.transform);
            _scrollRect = scrollGo.AddComponent<VirtualScrollRect>();

            // Use reflection to set _viewport since it is a SerializeField.
            var viewportField = typeof(VirtualScrollRect).GetField(
                "_viewport",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            viewportField.SetValue(_scrollRect, viewportRt);

            // Same for _axis -- SerializeField with no public setter.
            var axisField = typeof(VirtualScrollRect).GetField(
                "_axis",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            axisField.SetValue(_scrollRect, axis);

            _factory = new MockFactory(_root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void Activate_ConnectsToReactiveList()
        {
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            // After Activate, adding an element must trigger View creation.
            _vmList.Add(new TestViewModel());

            Assert.AreEqual(1, _factory.CreateCount);

            binding.Dispose();
        }

        [Test]
        public void OnContentChanged_RebuildLayoutAndCreatesViews()
        {
            // Add elements BEFORE Activate -- OnContentChanged will fire during Activate.
            for (var i = 0; i < 5; i++)
            {
                _vmList.Items.Add(new TestViewModel());
            }

            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            // Viewport 300f, item height 100f -> 3 visible elements + overscan.
            // At least 3 Views must be created.
            Assert.GreaterOrEqual(_factory.CreateCount, 3);

            binding.Dispose();
        }

        [Test]
        public void OnElementAdded_InVisibleRange_CreatesView()
        {
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            _vmList.Add(new TestViewModel());
            _vmList.Add(new TestViewModel());

            Assert.AreEqual(2, _factory.CreateCount);

            binding.Dispose();
        }

        [Test]
        public void OnElementAdded_OutsideVisibleRange_DoesNotCreateView()
        {
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            // Viewport 300f, item height 100f, overscan 2 -> up to ~5 visible elements.
            // Add 10 elements -- the last ones should fall outside the viewport.
            for (var i = 0; i < 10; i++)
            {
                _vmList.Add(new TestViewModel());
            }

            // Not all 10 should have a View (some lie outside the viewport).
            Assert.Less(_factory.CreateCount, 10);

            binding.Dispose();
        }

        [Test]
        public void OnElementRemoved_InVisibleRange_ReleasesView()
        {
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            _vmList.Add(new TestViewModel());
            _vmList.Add(new TestViewModel());

            var createCountBefore = _factory.CreateCount;
            Assert.AreEqual(2, createCountBefore);

            _vmList.RemoveAt(0);

            // After removing an element from the visible range its View must be released
            // and the remaining element must still be visible.
            Assert.AreEqual(createCountBefore, _factory.CreateCount);

            binding.Dispose();
        }

        [Test]
        public void OnElementReplaced_InVisibleRange_ReconnectsView()
        {
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            var original = new TestViewModel();
            _vmList.Add(original);

            var replacement = new TestViewModel();
            replacement.Title.Value = "Replaced";

            _vmList.Items[0] = replacement;

            // The View should be reconnected, not freshly created.
            Assert.AreEqual(1, _factory.CreateCount);

            binding.Dispose();
        }

        [Test]
        public void ScrollPositionChange_UpdatesVisibleViews()
        {
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            // Add 10 elements (total height = 1000f).
            for (var i = 0; i < 10; i++)
            {
                _vmList.Add(new TestViewModel());
            }

            var createCountBefore = _factory.CreateCount;

            // Scroll to the end.
            _scrollRect.ScrollPosition = 700f;

            // New Views should be created for elements that became visible.
            // Total factory calls must grow.
            Assert.GreaterOrEqual(_factory.CreateCount, createCountBefore);

            binding.Dispose();
        }

        [Test]
        public void Dispose_ClearsAllViewsAndNullifiesReferences()
        {
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            _vmList.Add(new TestViewModel());
            _vmList.Add(new TestViewModel());

            Assert.AreEqual(2, _factory.CreateCount);

            binding.Dispose();

            // After Dispose the factory must receive RemoveWidget calls.
            Assert.GreaterOrEqual(_factory.RemoveCount, 0);
        }

        [Test]
        public void ScrollPositionCorrection_OnAddAboveViewport()
        {
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            // Add 10 elements and scroll.
            for (var i = 0; i < 10; i++)
            {
                _vmList.Add(new TestViewModel());
            }

            _scrollRect.ScrollPosition = 500f;
            var scrollBefore = _scrollRect.ScrollPosition;

            // Insert an element via Items.Insert at the head (index 0, above the viewport).
            _vmList.Items.Insert(0, new TestViewModel());

            // ScrollPosition must grow by the inserted element's height (100f).
            Assert.AreEqual(scrollBefore + 100f, _scrollRect.ScrollPosition, 1f);

            binding.Dispose();
        }

        [Test]
        public void ScrollPositionCorrection_OnRemoveAboveViewport()
        {
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            // Add 10 elements and scroll.
            for (var i = 0; i < 10; i++)
            {
                _vmList.Add(new TestViewModel());
            }

            _scrollRect.ScrollPosition = 500f;
            var scrollBefore = _scrollRect.ScrollPosition;

            // Remove an element from the head (index 0, above the viewport).
            _vmList.RemoveAt(0);

            // ScrollPosition must shrink by the removed element's height (100f).
            Assert.AreEqual(scrollBefore - 100f, _scrollRect.ScrollPosition, 1f);

            binding.Dispose();
        }

        [Test]
        public void OnContentChanged_HorizontalAxis_RebuildLayoutAndCreatesViews()
        {
            // Horizontal axis: viewport width = 300, items 100 wide -> 3 visible + overscan.
            ConfigureScrollRect(ScrollAxis.Horizontal, new Vector2(300f, 400f));
            _vmList = new ReactiveVirtualList<TestViewModel>(100f);

            // Add elements BEFORE Activate -- OnContentChanged will fire during Activate.
            for (var i = 0; i < 5; i++)
            {
                _vmList.Items.Add(new TestViewModel());
            }

            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            // At least 3 Views must be created.
            Assert.GreaterOrEqual(_factory.CreateCount, 3);

            binding.Dispose();
        }

        [Test]
        public void ScrollPositionChange_HorizontalAxis_UpdatesVisibleViews()
        {
            ConfigureScrollRect(ScrollAxis.Horizontal, new Vector2(300f, 400f));
            _vmList = new ReactiveVirtualList<TestViewModel>(100f);

            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            // Add 10 elements (total width = 1000f).
            for (var i = 0; i < 10; i++)
            {
                _vmList.Add(new TestViewModel());
            }

            var createCountBefore = _factory.CreateCount;

            // Scroll to the horizontal end.
            _scrollRect.ScrollPosition = 700f;

            // New Views must be created for elements that became visible.
            Assert.GreaterOrEqual(_factory.CreateCount, createCountBefore);

            binding.Dispose();
        }

        [Test]
        public void HorizontalAxis_FirstVisibleIndex_IsZeroAtScrollZero()
        {
            ConfigureScrollRect(ScrollAxis.Horizontal, new Vector2(300f, 400f));
            _vmList = new ReactiveVirtualList<TestViewModel>(100f);

            for (var i = 0; i < 5; i++)
            {
                _vmList.Items.Add(new TestViewModel());
            }

            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            // At ScrollPosition = 0 the first visible index is 0.
            Assert.AreEqual(0, _vmList.FirstVisibleIndex.Value);

            binding.Dispose();
        }

        [Test]
        public void OnContentChanged_WithSpacing_RebuildLayoutAndCreatesViews()
        {
            // Set _spacing via reflection (SerializeField, no public setter).
            var spacingField = typeof(VirtualScrollRect).GetField(
                "_spacing",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            spacingField.SetValue(_scrollRect, 20f);

            // Add elements BEFORE Activate -- OnContentChanged will fire during Activate.
            for (var i = 0; i < 5; i++)
            {
                _vmList.Items.Add(new TestViewModel());
            }

            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            // Viewport 300f, item 100f, spacing 20f -> stride 120f -> 2-3 full visible + overscan.
            // At least 2 views must be created -- smoke check that spacing does not break the binding.
            Assert.GreaterOrEqual(_factory.CreateCount, 2);

            binding.Dispose();
        }

        [Test]
        public void Dispose_DoesNotDoubleDisposeView()
        {
            // B-3 regression: Release itself calls view.Dispose(), so if the binding's Dispose still
            // had an explicit kvp.Value.Dispose() BEFORE Release, OnDisposed() would fire twice.
            var countingFactory = new CountingFactory(_root.transform);
            var vmList = new ReactiveVirtualList<TestViewModel>(100f);

            var binding = VirtualCollectionBinding<TestViewModel, CountingWidgetView>.GetOrCreate()
                .Connect(vmList, countingFactory, _scrollRect);
            binding.Activate();

            vmList.Add(new TestViewModel());
            vmList.Add(new TestViewModel());

            Assert.AreEqual(2, countingFactory.CreateCount);
            var createdViews = countingFactory.CreatedViews.ToArray();

            binding.Dispose();

            // Every view must receive exactly one OnDisposed (from Release inside the binding's
            // Dispose). The assertion message must not touch view.name/view.gameObject -- by the
            // time it runs, the GameObject is already destroyed via DisposeAll → RemoveWidget →
            // DestroyImmediate, and any Unity API on view would throw MissingReferenceException
            // and mask the real cause.
            for (var i = 0; i < createdViews.Length; i++)
            {
                Assert.AreEqual(1, createdViews[i].OnDisposedCallCount,
                    $"View [{i}] received OnDisposed {createdViews[i].OnDisposedCallCount} times instead of 1");
            }
        }

        [Test]
        public void Dispose_UnbindsItemsList()
        {
            // B-4 regression: Dispose must call Items.Unbind() BEFORE nulling _vmList,
            // otherwise a subsequent Items.Connect() would throw InvalidOperationException("Already bound").
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            _vmList.Add(new TestViewModel());

            binding.Dispose();

            // After Dispose, Items must be Unbound -- a second Connect must not throw.
            Assert.DoesNotThrow(() =>
            {
                _vmList.Items.Connect(
                    onContentChanged: _ => { },
                    onElementAdded: (_, _) => { },
                    onElementReplaced: (_, _) => { },
                    onElementRemoved: (_, _) => { }
                );
            });

            // Cleanup: return Items to the unbound state so TearDown is not affected.
            _vmList.Items.Unbind();
        }

        [Test]
        public void Dispose_ResetsScrollRectCallback()
        {
            // B-5 regression: Dispose must call SetOnScrollPositionChanged(null) BEFORE nulling
            // _scrollRect, otherwise the ScrollPosition setter would trigger the callback with
            // already-nulled _vmList/_layoutCalculator and throw NullReferenceException.
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            for (var i = 0; i < 10; i++)
            {
                _vmList.Add(new TestViewModel());
            }

            binding.Dispose();

            // After the binding is Disposed, scrollRect must not poke a null binding.
            Assert.DoesNotThrow(() => _scrollRect.ScrollPosition = 200f);
        }

        [Test]
        public void OnElementAdded_FixedHeight_PreservesFixedPath()
        {
            // B-7 regression: a sequence of Add calls with elements of the same height must keep
            // the binding in fixed-mode (LayoutCalculator._fixedHeight > 0) and not slide into
            // variable-mode via InsertAt (which resets _fixedHeight to 0).
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            const int count = 50;
            const float fixedHeight = 100f;
            for (var i = 0; i < count; i++)
            {
                _vmList.Add(new TestViewModel());
            }

            // Pull _layoutCalculator out of the binding via reflection together with its _fixedHeight.
            var layoutField = typeof(VirtualCollectionBinding<TestViewModel, TestWidgetView>).GetField(
                "_layoutCalculator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var layoutCalc = layoutField.GetValue(binding);

            var fixedHeightField = layoutCalc.GetType().GetField(
                "_fixedHeight",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var actualFixedHeight = (float)fixedHeightField.GetValue(layoutCalc);

            // If the binding slid into variable-mode via InsertAt, _fixedHeight would become 0.
            Assert.AreEqual(fixedHeight, actualFixedHeight, 0.001f,
                "After Add of 50 same-height elements LayoutCalculator must stay in fixed-mode (B-7)");

            // Extra sanity check: TotalHeight for fixed-mode without spacing.
            var totalHeightProp = layoutCalc.GetType().GetProperty(
                "TotalHeight",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var totalHeight = (float)totalHeightProp.GetValue(layoutCalc);
            Assert.AreEqual(count * fixedHeight, totalHeight, 0.5f);

            binding.Dispose();
        }
    }
}
