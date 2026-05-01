using System;
using NUnit.Framework;

namespace Shtl.Mvvm.Tests
{
    [TestFixture]
    public class ReactiveVirtualListTests
    {
        private class TestViewModel : AbstractViewModel
        {
            public readonly ReactiveValue<string> Name = new("default");

            public TestViewModel() { }
        }

        [Test]
        public void FixedHeight_GetItemHeight_ReturnsFixedValue()
        {
            var list = new ReactiveVirtualList<TestViewModel>(50f);

            Assert.AreEqual(50f, list.GetItemHeight(0));
            Assert.AreEqual(50f, list.GetItemHeight(99));
        }

        [Test]
        public void VariableHeight_GetItemHeight_ReturnsCallbackResult()
        {
            var list = new ReactiveVirtualList<TestViewModel>(i => (i + 1) * 10f);

            Assert.AreEqual(10f, list.GetItemHeight(0));
            Assert.AreEqual(50f, list.GetItemHeight(4));
            Assert.AreEqual(100f, list.GetItemHeight(9));
        }

        [Test]
        public void Add_IncreasesCount_AndItemAccessibleByIndex()
        {
            var list = new ReactiveVirtualList<TestViewModel>(50f);
            var vm = new TestViewModel();

            list.Add(vm);

            Assert.AreEqual(1, list.Count);
            Assert.AreSame(vm, list[0]);
        }

        [Test]
        public void RemoveAt_DecreasesCount()
        {
            var list = new ReactiveVirtualList<TestViewModel>(50f);
            list.Add(new TestViewModel());
            list.Add(new TestViewModel());

            list.RemoveAt(0);

            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public void Clear_SetsCountToZero()
        {
            var list = new ReactiveVirtualList<TestViewModel>(50f);
            list.Add(new TestViewModel());
            list.Add(new TestViewModel());
            list.Add(new TestViewModel());

            list.Clear();

            Assert.AreEqual(0, list.Count);
        }

        [Test]
        public void Items_IsSameReactiveList_UsedByProxyMethods()
        {
            var list = new ReactiveVirtualList<TestViewModel>(50f);
            var vm = new TestViewModel();

            list.Add(vm);

            Assert.AreEqual(1, list.Items.Count);
            Assert.AreSame(vm, list.Items[0]);
        }

        [Test]
        public void Dispose_DisposesAllInternalFields()
        {
            var list = new ReactiveVirtualList<TestViewModel>(50f);

            var scrollChanged = false;
            var firstIndexChanged = false;
            var visibleCountChanged = false;

            list.ScrollPosition.Connect(_ => scrollChanged = true);
            list.FirstVisibleIndex.Connect(_ => firstIndexChanged = true);
            list.VisibleCount.Connect(_ => visibleCountChanged = true);

            list.Dispose();

            // After Dispose the internal ReactiveValues must be reset.
            // A second Connect must not throw (Unbind was called).
            Assert.DoesNotThrow(() => list.ScrollPosition.Connect(_ => { }));
            Assert.DoesNotThrow(() => list.FirstVisibleIndex.Connect(_ => { }));
            Assert.DoesNotThrow(() => list.VisibleCount.Connect(_ => { }));
        }

        [Test]
        public void Unbind_UnbindsAllInternalFields()
        {
            var list = new ReactiveVirtualList<TestViewModel>(50f);

            list.ScrollPosition.Connect(_ => { });
            list.FirstVisibleIndex.Connect(_ => { });
            list.VisibleCount.Connect(_ => { });

            list.Unbind();

            // After Unbind a second Connect must not throw.
            Assert.DoesNotThrow(() => list.ScrollPosition.Connect(_ => { }));
            Assert.DoesNotThrow(() => list.FirstVisibleIndex.Connect(_ => { }));
            Assert.DoesNotThrow(() => list.VisibleCount.Connect(_ => { }));
        }

        [Test]
        public void ScrollPosition_InitialValueIsZero()
        {
            var list = new ReactiveVirtualList<TestViewModel>(50f);

            Assert.AreEqual(0f, list.ScrollPosition.Value);
        }

        [Test]
        public void FirstVisibleIndex_InitialValueIsZero()
        {
            var list = new ReactiveVirtualList<TestViewModel>(50f);

            Assert.AreEqual(0, list.FirstVisibleIndex.Value);
        }

        [Test]
        public void VisibleCount_InitialValueIsZero()
        {
            var list = new ReactiveVirtualList<TestViewModel>(50f);

            Assert.AreEqual(0, list.VisibleCount.Value);
        }

        [Test]
        public void GetItemHeight_HeightProviderReturnsNonPositive_ThrowsInvalidOperationException()
        {
            var list = new ReactiveVirtualList<TestViewModel>(_ => -5f);

            Assert.Throws<InvalidOperationException>(() => list.GetItemHeight(0));
        }

        [Test]
        public void GetItemHeight_HeightProviderReturnsZero_ThrowsInvalidOperationException()
        {
            var list = new ReactiveVirtualList<TestViewModel>(_ => 0f);

            Assert.Throws<InvalidOperationException>(() => list.GetItemHeight(0));
        }

        [Test]
        public void DefaultConstructor_CreatesListWithoutHeightConfiguration()
        {
            var list = new ReactiveVirtualList<TestViewModel>();

            Assert.AreEqual(0, list.Count);
            Assert.AreEqual(0f, list.ScrollPosition.Value);
            Assert.AreEqual(0, list.FirstVisibleIndex.Value);
            Assert.AreEqual(0, list.VisibleCount.Value);
        }
    }
}
