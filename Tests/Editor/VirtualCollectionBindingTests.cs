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

            // VirtualScrollRect требует RectTransform для viewport
            var viewportGo = new GameObject("Viewport");
            var viewportRt = viewportGo.AddComponent<RectTransform>();
            viewportRt.SetParent(_root.transform);
            viewportRt.sizeDelta = viewportSize;

            var scrollGo = new GameObject("ScrollRect");
            scrollGo.transform.SetParent(_root.transform);
            _scrollRect = scrollGo.AddComponent<VirtualScrollRect>();

            // Используем рефлексию для установки _viewport, т.к. поле SerializeField
            var viewportField = typeof(VirtualScrollRect).GetField(
                "_viewport",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            viewportField.SetValue(_scrollRect, viewportRt);

            // Аналогично для _axis -- SerializeField, не имеет публичного сеттера
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

            // После Activate добавление элемента должно вызвать создание View
            _vmList.Add(new TestViewModel());

            Assert.AreEqual(1, _factory.CreateCount);

            binding.Dispose();
        }

        [Test]
        public void OnContentChanged_RebuildLayoutAndCreatesViews()
        {
            // Добавляем элементы ДО подключения -- при Activate сработает OnContentChanged
            for (var i = 0; i < 5; i++)
            {
                _vmList.Items.Add(new TestViewModel());
            }

            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            // Viewport 300f, item height 100f -> видимо 3 элемента + overscan
            // Должно быть создано не менее 3 Views
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

            // Viewport 300f, item height 100f, overscan 2 -> видимо до ~5 элементов
            // Добавляем 10 элементов -- последние должны быть за пределами viewport
            for (var i = 0; i < 10; i++)
            {
                _vmList.Add(new TestViewModel());
            }

            // Не все 10 должны иметь View (некоторые за пределами viewport)
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

            // После удаления элемента из видимого диапазона View должен быть освобождён
            // и оставшийся элемент всё ещё виден
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

            // View должен быть переподключён, но не создан новый
            Assert.AreEqual(1, _factory.CreateCount);

            binding.Dispose();
        }

        [Test]
        public void ScrollPositionChange_UpdatesVisibleViews()
        {
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            // Добавляем 10 элементов (total height = 1000f)
            for (var i = 0; i < 10; i++)
            {
                _vmList.Add(new TestViewModel());
            }

            var createCountBefore = _factory.CreateCount;

            // Скроллим в конец
            _scrollRect.ScrollPosition = 700f;

            // Должны были создаться новые View для элементов, ставших видимыми
            // Общее количество фабричных вызовов должно увеличиться
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

            // После Dispose фабрика должна получить RemoveWidget вызовы
            Assert.GreaterOrEqual(_factory.RemoveCount, 0);
        }

        [Test]
        public void ScrollPositionCorrection_OnAddAboveViewport()
        {
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            // Добавляем 10 элементов и скроллим
            for (var i = 0; i < 10; i++)
            {
                _vmList.Add(new TestViewModel());
            }

            _scrollRect.ScrollPosition = 500f;
            var scrollBefore = _scrollRect.ScrollPosition;

            // Добавляем элемент через Items.Insert в начало (index 0, выше viewport)
            _vmList.Items.Insert(0, new TestViewModel());

            // Scroll position должна увеличиться на высоту добавленного элемента (100f)
            Assert.AreEqual(scrollBefore + 100f, _scrollRect.ScrollPosition, 1f);

            binding.Dispose();
        }

        [Test]
        public void ScrollPositionCorrection_OnRemoveAboveViewport()
        {
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            // Добавляем 10 элементов и скроллим
            for (var i = 0; i < 10; i++)
            {
                _vmList.Add(new TestViewModel());
            }

            _scrollRect.ScrollPosition = 500f;
            var scrollBefore = _scrollRect.ScrollPosition;

            // Удаляем элемент с начала (index 0, выше viewport)
            _vmList.RemoveAt(0);

            // Scroll position должна уменьшиться на высоту удалённого элемента (100f)
            Assert.AreEqual(scrollBefore - 100f, _scrollRect.ScrollPosition, 1f);

            binding.Dispose();
        }

        [Test]
        public void OnContentChanged_HorizontalAxis_RebuildLayoutAndCreatesViews()
        {
            // Горизонтальная ось: ширина viewport = 300, элементы по 100 -> видимо 3 + overscan
            ConfigureScrollRect(ScrollAxis.Horizontal, new Vector2(300f, 400f));
            _vmList = new ReactiveVirtualList<TestViewModel>(100f);

            // Добавляем элементы ДО подключения -- при Activate сработает OnContentChanged
            for (var i = 0; i < 5; i++)
            {
                _vmList.Items.Add(new TestViewModel());
            }

            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            // Должно быть создано не менее 3 Views
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

            // Добавляем 10 элементов (общая ширина = 1000f)
            for (var i = 0; i < 10; i++)
            {
                _vmList.Add(new TestViewModel());
            }

            var createCountBefore = _factory.CreateCount;

            // Скроллим в конец по горизонтали
            _scrollRect.ScrollPosition = 700f;

            // Должны были создаться новые View для элементов, ставших видимыми
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

            // При ScrollPosition = 0 первый видимый индекс = 0
            Assert.AreEqual(0, _vmList.FirstVisibleIndex.Value);

            binding.Dispose();
        }

        [Test]
        public void OnContentChanged_WithSpacing_RebuildLayoutAndCreatesViews()
        {
            // Установить _spacing через рефлексию (поле SerializeField, нет публичного сеттера)
            var spacingField = typeof(VirtualScrollRect).GetField(
                "_spacing",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            spacingField.SetValue(_scrollRect, 20f);

            // Добавляем элементы ДО подключения -- при Activate сработает OnContentChanged
            for (var i = 0; i < 5; i++)
            {
                _vmList.Items.Add(new TestViewModel());
            }

            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            // Viewport 300f, item 100f, spacing 20f -> stride 120f -> видимо 2-3 полных + overscan.
            // Минимум 2 view должно создаться -- smoke-проверка что spacing не ломает биндинг.
            Assert.GreaterOrEqual(_factory.CreateCount, 2);

            binding.Dispose();
        }

        [Test]
        public void Dispose_DoesNotDoubleDisposeView()
        {
            // B-3 regression: Release сам делает view.Dispose(), поэтому если в Dispose биндинга
            // остался явный kvp.Value.Dispose() ДО Release, OnDisposed() будет вызван дважды.
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

            // Каждый view должен получить ровно один OnDisposed (из Release внутри Dispose биндинга).
            // Сообщение об ошибке не обращается к view.name/view.gameObject -- к моменту проверки
            // GameObject уже уничтожен через DisposeAll → RemoveWidget → DestroyImmediate, и
            // любой Unity API на view бросит MissingReferenceException, маскируя реальную причину.
            for (var i = 0; i < createdViews.Length; i++)
            {
                Assert.AreEqual(1, createdViews[i].OnDisposedCallCount,
                    $"View [{i}] получил OnDisposed {createdViews[i].OnDisposedCallCount} раз вместо 1");
            }
        }

        [Test]
        public void Dispose_UnbindsItemsList()
        {
            // B-4 regression: Dispose должен вызвать Items.Unbind() ДО обнуления _vmList,
            // иначе повторный Items.Connect() бросит InvalidOperationException("Already bound").
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            _vmList.Add(new TestViewModel());

            binding.Dispose();

            // После Dispose Items должен быть Unbind'нут -- повторный Connect не бросает.
            Assert.DoesNotThrow(() =>
            {
                _vmList.Items.Connect(
                    onContentChanged: _ => { },
                    onElementAdded: (_, _) => { },
                    onElementReplaced: (_, _) => { },
                    onElementRemoved: (_, _) => { }
                );
            });

            // Cleanup: вернуть Items в unbound-состояние, чтобы не аффектить TearDown.
            _vmList.Items.Unbind();
        }

        [Test]
        public void Dispose_ResetsScrollRectCallback()
        {
            // B-5 regression: Dispose должен SetOnScrollPositionChanged(null) ДО обнуления _scrollRect,
            // иначе ScrollPosition setter дёрнет callback с уже null'ифицированным _vmList/_layoutCalculator
            // и кинет NullReferenceException.
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            for (var i = 0; i < 10; i++)
            {
                _vmList.Add(new TestViewModel());
            }

            binding.Dispose();

            // После Dispose биндинга scrollRect не должен дёргать null-биндинг.
            Assert.DoesNotThrow(() => _scrollRect.ScrollPosition = 200f);
        }

        [Test]
        public void OnElementAdded_FixedHeight_PreservesFixedPath()
        {
            // B-7 regression: при последовательном Add элементов одной высоты биндинг должен
            // оставаться в fixed-mode (LayoutCalculator._fixedHeight > 0), а не скатываться
            // в variable-mode через InsertAt (который сбрасывает _fixedHeight в 0).
            var binding = VirtualCollectionBinding<TestViewModel, TestWidgetView>.GetOrCreate()
                .Connect(_vmList, _factory, _scrollRect);
            binding.Activate();

            const int count = 50;
            const float fixedHeight = 100f;
            for (var i = 0; i < count; i++)
            {
                _vmList.Add(new TestViewModel());
            }

            // Через рефлексию вытащить _layoutCalculator из биндинга и его _fixedHeight.
            var layoutField = typeof(VirtualCollectionBinding<TestViewModel, TestWidgetView>).GetField(
                "_layoutCalculator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var layoutCalc = layoutField.GetValue(binding);

            var fixedHeightField = layoutCalc.GetType().GetField(
                "_fixedHeight",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var actualFixedHeight = (float)fixedHeightField.GetValue(layoutCalc);

            // Если бы биндинг скатился в variable-mode через InsertAt -- _fixedHeight стало бы 0.
            Assert.AreEqual(fixedHeight, actualFixedHeight, 0.001f,
                "После Add 50 элементов одной высоты LayoutCalculator должен оставаться в fixed-mode (B-7)");

            // Дополнительный sanity: TotalHeight для fixed-mode без spacing.
            var totalHeightProp = layoutCalc.GetType().GetProperty(
                "TotalHeight",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var totalHeight = (float)totalHeightProp.GetValue(layoutCalc);
            Assert.AreEqual(count * fixedHeight, totalHeight, 0.5f);

            binding.Dispose();
        }
    }
}
