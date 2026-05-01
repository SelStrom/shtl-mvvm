using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Shtl.Mvvm.Tests
{
    [TestFixture]
    public class ViewRecyclingPoolTests
    {
        private class TestViewModel : AbstractViewModel
        {
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
            public List<TestWidgetView> RemovedViews { get; } = new();

            private readonly Transform _parent;

            public MockFactory(Transform parent)
            {
                _parent = parent;
            }

            public TestWidgetView CreateWidget(TestViewModel viewModel)
            {
                CreateCount++;
                var go = new GameObject("TestView");
                go.transform.SetParent(_parent);
                return go.AddComponent<TestWidgetView>();
            }

            public void RemoveWidget(TestWidgetView view)
            {
                RemoveCount++;
                RemovedViews.Add(view);
                Object.DestroyImmediate(view.gameObject);
            }
        }

        private GameObject _root;
        private MockFactory _factory;
        private ViewRecyclingPool<TestViewModel, TestWidgetView> _pool;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TestRoot");
            _factory = new MockFactory(_root.transform);
            _pool = new ViewRecyclingPool<TestViewModel, TestWidgetView>(_factory);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void Get_EmptyPool_CallsFactoryCreateWidget()
        {
            var view = _pool.Get();

            Assert.IsNotNull(view);
            Assert.AreEqual(1, _factory.CreateCount);
        }

        [Test]
        public void Get_AfterRelease_ReturnsSameViewWithoutFactoryCall()
        {
            var view = _pool.Get();
            _pool.Release(view);

            var reusedView = _pool.Get();

            Assert.AreSame(view, reusedView);
            Assert.AreEqual(1, _factory.CreateCount);
        }

        [Test]
        public void Release_DeactivatesGameObject()
        {
            var view = _pool.Get();
            Assert.IsTrue(view.gameObject.activeSelf);

            _pool.Release(view);

            Assert.IsFalse(view.gameObject.activeSelf);
        }

        [Test]
        public void Get_FromNonEmptyPool_ActivatesGameObject()
        {
            var view = _pool.Get();
            _pool.Release(view);

            Assert.IsFalse(view.gameObject.activeSelf);

            var reusedView = _pool.Get();

            Assert.IsTrue(reusedView.gameObject.activeSelf);
        }

        [Test]
        public void MultipleReleaseAndGet_ReturnsInLifoOrder()
        {
            var view1 = _pool.Get();
            var view2 = _pool.Get();
            var view3 = _pool.Get();

            _pool.Release(view1);
            _pool.Release(view2);
            _pool.Release(view3);

            var got1 = _pool.Get();
            var got2 = _pool.Get();
            var got3 = _pool.Get();

            Assert.AreSame(view3, got1);
            Assert.AreSame(view2, got2);
            Assert.AreSame(view1, got3);
        }

        [Test]
        public void Count_ReflectsPoolSize()
        {
            Assert.AreEqual(0, _pool.Count);

            var view1 = _pool.Get();
            var view2 = _pool.Get();

            _pool.Release(view1);
            Assert.AreEqual(1, _pool.Count);

            _pool.Release(view2);
            Assert.AreEqual(2, _pool.Count);

            _pool.Get();
            Assert.AreEqual(1, _pool.Count);
        }

        [Test]
        public void DisposeAll_CallsFactoryRemoveWidgetAndClearsPool()
        {
            var view1 = _pool.Get();
            var view2 = _pool.Get();

            _pool.Release(view1);
            _pool.Release(view2);

            Assert.AreEqual(2, _pool.Count);

            _pool.DisposeAll();

            Assert.AreEqual(0, _pool.Count);
            Assert.AreEqual(2, _factory.RemoveCount);
        }
    }
}
