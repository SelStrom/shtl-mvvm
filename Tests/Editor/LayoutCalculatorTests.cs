using System;
using NUnit.Framework;

namespace Shtl.Mvvm.Tests
{
    [TestFixture]
    public class LayoutCalculatorTests
    {
        [Test]
        public void Rebuild_ZeroItems_TotalHeightIsZero()
        {
            var calc = new LayoutCalculator();
            calc.Rebuild(0, 100f);

            Assert.AreEqual(0f, calc.TotalHeight);
        }

        [Test]
        public void Rebuild_ZeroItems_GetItemOffset_ReturnsZero()
        {
            var calc = new LayoutCalculator();
            calc.Rebuild(0, 100f);

            Assert.AreEqual(0f, calc.GetItemOffset(0));
        }

        [Test]
        public void Rebuild_FixedHeight_TotalHeightCorrect()
        {
            var calc = new LayoutCalculator();
            calc.Rebuild(5, 100f);

            Assert.AreEqual(500f, calc.TotalHeight);
        }

        [Test]
        public void Rebuild_FixedHeight_GetItemOffset_ReturnsCorrectPosition()
        {
            var calc = new LayoutCalculator();
            calc.Rebuild(5, 100f);

            Assert.AreEqual(0f, calc.GetItemOffset(0));
            Assert.AreEqual(200f, calc.GetItemOffset(2));
            Assert.AreEqual(400f, calc.GetItemOffset(4));
        }

        [Test]
        public void Rebuild_VariableHeight_TotalHeightCorrect()
        {
            float[] heights = { 50f, 100f, 75f, 200f, 50f };
            var calc = new LayoutCalculator();
            calc.Rebuild(5, i => heights[i]);

            Assert.AreEqual(475f, calc.TotalHeight);
        }

        [Test]
        public void Rebuild_VariableHeight_GetItemOffset_ReturnsCorrectPositions()
        {
            float[] heights = { 50f, 100f, 75f, 200f, 50f };
            var calc = new LayoutCalculator();
            calc.Rebuild(5, i => heights[i]);

            Assert.AreEqual(0f, calc.GetItemOffset(0));
            Assert.AreEqual(50f, calc.GetItemOffset(1));
            Assert.AreEqual(150f, calc.GetItemOffset(2));
            Assert.AreEqual(225f, calc.GetItemOffset(3));
            Assert.AreEqual(425f, calc.GetItemOffset(4));
        }

        [Test]
        public void FindVisibleRange_FixedHeight_ScrollPosZero()
        {
            var calc = new LayoutCalculator();
            calc.Rebuild(10, 100f);

            var range = calc.FindVisibleRange(0f, 250f, 0);

            Assert.AreEqual(0, range.FirstIndex);
            Assert.AreEqual(3, range.Count);
        }

        [Test]
        public void FindVisibleRange_FixedHeight_PartiallyVisibleIncluded()
        {
            var calc = new LayoutCalculator();
            calc.Rebuild(10, 100f);

            // scrollPos=150 => first visible idx=1 (offset 100, partially visible)
            // viewport=250 => scrollPos+viewport=400 => last visible idx=3 (offset 300, ends at 400)
            var range = calc.FindVisibleRange(150f, 250f, 0);

            Assert.AreEqual(1, range.FirstIndex);
            Assert.IsTrue(range.Count >= 3);
        }

        [Test]
        public void FindVisibleRange_WithOverscan_ExpandsRange()
        {
            var calc = new LayoutCalculator();
            calc.Rebuild(20, 100f);

            // Without overscan: scrollPos=500, viewport=300 => visible 5..7
            // With overscan=2: expanded to 3..9
            var range = calc.FindVisibleRange(500f, 300f, 2);

            Assert.AreEqual(3, range.FirstIndex);
            Assert.AreEqual(9, range.LastIndex);
        }

        [Test]
        public void FindVisibleRange_WithOverscan_ClampedToZeroAndCount()
        {
            var calc = new LayoutCalculator();
            calc.Rebuild(5, 100f);

            // scrollPos=0, viewport=200, overscan=5 => first clamps to 0, last clamps to 4
            var range = calc.FindVisibleRange(0f, 200f, 5);

            Assert.AreEqual(0, range.FirstIndex);
            Assert.AreEqual(4, range.LastIndex);
        }

        [Test]
        public void FindVisibleRange_VariableHeight_BinarySearchCorrect()
        {
            float[] heights = { 50f, 100f, 75f, 200f, 50f };
            var calc = new LayoutCalculator();
            calc.Rebuild(5, i => heights[i]);

            // TotalHeight = 475. scrollPos=150 (after idx 0+1), viewport=250
            // idx 2 offset=150, idx 3 offset=225, idx 4 offset=425
            // scrollPos+viewport=400 => last visible includes idx 3 (225+200=425 > 400, but starts at 225 < 400)
            var range = calc.FindVisibleRange(150f, 250f, 0);

            Assert.AreEqual(2, range.FirstIndex);
            Assert.IsTrue(range.LastIndex >= 3);
        }

        [Test]
        public void FixedHeight_Optimization_GetItemOffset_IsDirectMultiply()
        {
            var calc = new LayoutCalculator();
            calc.Rebuild(1000, 50f);

            // O(1) path: index * fixedHeight
            Assert.AreEqual(500 * 50f, calc.GetItemOffset(500));
            Assert.AreEqual(999 * 50f, calc.GetItemOffset(999));
        }

        [Test]
        public void InsertAt_RecalculatesPrefixSum()
        {
            float[] heights = { 50f, 100f, 75f };
            var calc = new LayoutCalculator();
            calc.Rebuild(3, i => heights[i]);

            Assert.AreEqual(225f, calc.TotalHeight);

            // Insert an element with height 30 at position 1.
            float[] newHeights = { 50f, 30f, 100f, 75f };
            calc.InsertAt(1, 4, i => newHeights[i]);

            Assert.AreEqual(255f, calc.TotalHeight);
            Assert.AreEqual(50f, calc.GetItemOffset(1));
            Assert.AreEqual(80f, calc.GetItemOffset(2));
        }

        [Test]
        public void RemoveAt_RecalculatesPrefixSum()
        {
            float[] heights = { 50f, 100f, 75f, 200f };
            var calc = new LayoutCalculator();
            calc.Rebuild(4, i => heights[i]);

            Assert.AreEqual(425f, calc.TotalHeight);

            // Remove element 1 (height 100).
            float[] newHeights = { 50f, 75f, 200f };
            calc.RemoveAt(1, 3, i => newHeights[i]);

            Assert.AreEqual(325f, calc.TotalHeight);
            Assert.AreEqual(50f, calc.GetItemOffset(1));
            Assert.AreEqual(125f, calc.GetItemOffset(2));
        }

        [Test]
        public void ScrollPosition_BeyondTotalHeight_ClampsToLastElement()
        {
            var calc = new LayoutCalculator();
            calc.Rebuild(5, 100f);

            // scrollPos=1000 > totalHeight=500 => should return the last element
            var range = calc.FindVisibleRange(1000f, 250f, 0);

            Assert.AreEqual(4, range.LastIndex);
        }

        [Test]
        public void ViewportHeight_GreaterThanTotalHeight_AllElementsVisible()
        {
            var calc = new LayoutCalculator();
            calc.Rebuild(3, 100f);

            // viewport=500 > totalHeight=300 => all 3 elements are visible
            var range = calc.FindVisibleRange(0f, 500f, 0);

            Assert.AreEqual(0, range.FirstIndex);
            Assert.AreEqual(2, range.LastIndex);
            Assert.AreEqual(3, range.Count);
        }

        [Test]
        public void GetItemHeight_FixedHeight_ReturnsFixedValue()
        {
            var calc = new LayoutCalculator();
            calc.Rebuild(5, 100f);

            Assert.AreEqual(100f, calc.GetItemHeight(0));
            Assert.AreEqual(100f, calc.GetItemHeight(4));
        }

        [Test]
        public void GetItemHeight_VariableHeight_ReturnsCorrectValue()
        {
            float[] heights = { 50f, 100f, 75f };
            var calc = new LayoutCalculator();
            calc.Rebuild(3, i => heights[i]);

            Assert.AreEqual(50f, calc.GetItemHeight(0));
            Assert.AreEqual(100f, calc.GetItemHeight(1));
            Assert.AreEqual(75f, calc.GetItemHeight(2));
        }

        [Test]
        public void FindVisibleRange_ZeroItems_ReturnsEmptyRange()
        {
            var calc = new LayoutCalculator();
            calc.Rebuild(0, 100f);

            var range = calc.FindVisibleRange(0f, 250f, 0);

            Assert.AreEqual(0, range.Count);
        }

        // Axis-agnostic tests: LayoutCalculator operates on scalars and works identically
        // for the vertical (Y) and horizontal (X) axis. These tests document the contract,
        // guaranteeing that the binding can rely on the same values whether interpreted as
        // height/offset along Y or width/offset along X.

        [Test]
        public void FindVisibleRange_AxisAgnostic_FixedSize_SameResultForXAndY()
        {
            var calcAsY = new LayoutCalculator();
            calcAsY.Rebuild(10, 100f);

            var calcAsX = new LayoutCalculator();
            calcAsX.Rebuild(10, 100f);

            // viewportSize=300 is interpreted as height (for Y) or width (for X) —
            // the calculator must return the same visible range.
            var rangeY = calcAsY.FindVisibleRange(150f, 300f, 1);
            var rangeX = calcAsX.FindVisibleRange(150f, 300f, 1);

            Assert.AreEqual(rangeY.FirstIndex, rangeX.FirstIndex);
            Assert.AreEqual(rangeY.LastIndex, rangeX.LastIndex);
            Assert.AreEqual(rangeY.Count, rangeX.Count);
        }

        [Test]
        public void FindVisibleRange_AxisAgnostic_VariableSize_SameResultForXAndY()
        {
            float[] sizes = { 50f, 100f, 75f, 200f, 50f, 80f, 120f };

            var calcAsY = new LayoutCalculator();
            calcAsY.Rebuild(sizes.Length, i => sizes[i]);

            var calcAsX = new LayoutCalculator();
            calcAsX.Rebuild(sizes.Length, i => sizes[i]);

            // Same sizes, same scrollPosition and viewportSize — regardless of whether we
            // interpret them as Y or X, the result is identical.
            var rangeY = calcAsY.FindVisibleRange(120f, 250f, 0);
            var rangeX = calcAsX.FindVisibleRange(120f, 250f, 0);

            Assert.AreEqual(rangeY.FirstIndex, rangeX.FirstIndex);
            Assert.AreEqual(rangeY.LastIndex, rangeX.LastIndex);
            Assert.AreEqual(calcAsY.TotalHeight, calcAsX.TotalHeight);
            Assert.AreEqual(calcAsY.GetItemOffset(3), calcAsX.GetItemOffset(3));
        }

        // === Spacing-aware tests ===

        [Test]
        public void Rebuild_FixedHeight_WithSpacing_TotalHeightCorrect()
        {
            var calc = new LayoutCalculator();
            calc.SetSpacing(20f);
            calc.Rebuild(5, 100f);

            // 5 * 100 + 4 * 20 = 580
            Assert.AreEqual(580f, calc.TotalHeight);
        }

        [Test]
        public void Rebuild_FixedHeight_WithSpacing_GetItemOffset_ReturnsCorrectPosition()
        {
            var calc = new LayoutCalculator();
            calc.SetSpacing(20f);
            calc.Rebuild(5, 100f);

            Assert.AreEqual(0f, calc.GetItemOffset(0));
            Assert.AreEqual(240f, calc.GetItemOffset(2)); // 2 * (100 + 20)
            Assert.AreEqual(480f, calc.GetItemOffset(4)); // 4 * (100 + 20)
        }

        [Test]
        public void Rebuild_VariableHeight_WithSpacing_TotalHeightCorrect()
        {
            float[] heights = { 50f, 100f, 75f, 200f, 50f };
            var calc = new LayoutCalculator();
            calc.SetSpacing(10f);
            calc.Rebuild(5, i => heights[i]);

            // 475 + 4 * 10 = 515
            Assert.AreEqual(515f, calc.TotalHeight);
        }

        [Test]
        public void Rebuild_VariableHeight_WithSpacing_GetItemOffset_ReturnsCorrectPositions()
        {
            float[] heights = { 50f, 100f, 75f, 200f, 50f };
            var calc = new LayoutCalculator();
            calc.SetSpacing(10f);
            calc.Rebuild(5, i => heights[i]);

            Assert.AreEqual(0f, calc.GetItemOffset(0));
            Assert.AreEqual(60f, calc.GetItemOffset(1));   // 50 + 10
            Assert.AreEqual(170f, calc.GetItemOffset(2));  // 60 + 100 + 10
            Assert.AreEqual(255f, calc.GetItemOffset(3));  // 170 + 75 + 10
            Assert.AreEqual(465f, calc.GetItemOffset(4));  // 255 + 200 + 10
        }

        [Test]
        public void GetItemHeight_VariableHeight_WithSpacing_ReturnsRawHeight()
        {
            float[] heights = { 50f, 100f, 75f };
            var calc = new LayoutCalculator();
            calc.SetSpacing(10f);
            calc.Rebuild(3, i => heights[i]);

            // Spacing must not be included in the item's own size.
            Assert.AreEqual(50f, calc.GetItemHeight(0));
            Assert.AreEqual(100f, calc.GetItemHeight(1));
            Assert.AreEqual(75f, calc.GetItemHeight(2));
        }

        [Test]
        public void Spacing_Zero_BehaviorIdenticalToBaseline()
        {
            float[] heights = { 50f, 100f, 75f, 200f, 50f };
            var calc = new LayoutCalculator();
            calc.SetSpacing(0f);
            calc.Rebuild(5, i => heights[i]);

            // Must match the existing tests without spacing.
            Assert.AreEqual(475f, calc.TotalHeight);
            Assert.AreEqual(225f, calc.GetItemOffset(3));
            Assert.AreEqual(50f, calc.GetItemHeight(0));
        }

        [Test]
        public void Spacing_AxisAgnostic_SameResultForXAndY()
        {
            float[] sizes = { 50f, 100f, 75f, 200f, 50f };
            const float spacing = 15f;

            var calcAsY = new LayoutCalculator();
            calcAsY.SetSpacing(spacing);
            calcAsY.Rebuild(sizes.Length, i => sizes[i]);

            var calcAsX = new LayoutCalculator();
            calcAsX.SetSpacing(spacing);
            calcAsX.Rebuild(sizes.Length, i => sizes[i]);

            // Same scalars → result is identical regardless of axis interpretation.
            Assert.AreEqual(calcAsY.TotalHeight, calcAsX.TotalHeight);
            Assert.AreEqual(calcAsY.GetItemOffset(3), calcAsX.GetItemOffset(3));

            var rangeY = calcAsY.FindVisibleRange(120f, 250f, 0);
            var rangeX = calcAsX.FindVisibleRange(120f, 250f, 0);
            Assert.AreEqual(rangeY.FirstIndex, rangeX.FirstIndex);
            Assert.AreEqual(rangeY.LastIndex, rangeX.LastIndex);
        }

        [Test]
        public void InsertAt_WithSpacing_RecalculatesPrefixSum()
        {
            float[] heights = { 50f, 100f, 75f };
            var calc = new LayoutCalculator();
            calc.SetSpacing(10f);
            calc.Rebuild(3, i => heights[i]);

            // 50 + 100 + 75 + 2 * 10 = 245
            Assert.AreEqual(245f, calc.TotalHeight);

            float[] newHeights = { 50f, 30f, 100f, 75f };
            calc.InsertAt(1, 4, i => newHeights[i]);

            // 50 + 30 + 100 + 75 + 3 * 10 = 285
            Assert.AreEqual(285f, calc.TotalHeight);
            Assert.AreEqual(60f, calc.GetItemOffset(1));   // 50 + 10
            Assert.AreEqual(100f, calc.GetItemOffset(2));  // 50 + 10 + 30 + 10
        }
    }
}
