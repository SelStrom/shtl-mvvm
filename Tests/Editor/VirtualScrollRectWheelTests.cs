using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Shtl.Mvvm.Tests
{
    /// <summary>
    /// Regression tests for VLIST-03 (wheel rubber-band semantics).
    ///
    /// Contract:
    /// - MovementType.Elastic: wheel past the boundary applies RubberDelta to the over-bound part
    ///   (asymptotic compression bounded by ViewportSize), then LateUpdate returns to the
    ///   boundary via SmoothDamp once wheel input stops.
    /// - MovementType.Clamped: hard-clamp into [0, maxScroll], no overshoot.
    /// - MovementType.Unrestricted: raw delta, no constraints.
    /// - Judder-fix invariant: velocity is always zeroed in OnScroll, does not accumulate
    ///   between wheel events; LateUpdate elastic SmoothDamp starts from velocity=0.
    /// </summary>
    [TestFixture]
    public class VirtualScrollRectWheelTests
    {
        // Fields and methods are often internal/private — reflection is used (same approach as
        // the existing VirtualCollectionBindingTests). Tests assert observable behaviour through
        // the `internal` API (ScrollPosition / Velocity / SetContentSize).
        private const float ViewportHeight = 300f;
        private const float ContentHeight = 1000f;
        private const float Sensitivity = 35f;

        private GameObject _root;
        private VirtualScrollRect _scrollRect;
        private GameObject _eventSystemGo;

        [SetUp]
        public void SetUp()
        {
            // EventSystem is required by the PointerEventData constructor.
            _eventSystemGo = new GameObject("EventSystem");
            _eventSystemGo.AddComponent<EventSystem>();

            _root = new GameObject("TestRoot");

            var viewportGo = new GameObject("Viewport");
            var viewportRt = viewportGo.AddComponent<RectTransform>();
            viewportRt.SetParent(_root.transform);
            viewportRt.sizeDelta = new Vector2(400f, ViewportHeight);

            var scrollGo = new GameObject("ScrollRect");
            scrollGo.transform.SetParent(_root.transform);
            _scrollRect = scrollGo.AddComponent<VirtualScrollRect>();

            SetPrivateField("_viewport", viewportRt);
            SetPrivateField("_axis", ScrollAxis.Vertical);
            SetPrivateField("_scrollSensitivity", Sensitivity);
            SetPrivateField("_movementType", MovementType.Elastic);
            SetPrivateField("_inertia", true);
            SetPrivateField("_decelerationRate", 0.135f);
            SetPrivateField("_elasticity", 0.1f);

            _scrollRect.SetContentSize(ContentHeight);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_eventSystemGo);
        }

        [Test]
        public void OnScroll_Elastic_AtTopBound_AppliesRubberDeltaForNegativeOvershoot()
        {
            // Arrange: position at the start (top), wheel tries to go "above" zero.
            // In Elastic mode there must be a rubber-band overshoot bounded by ViewportSize.
            _scrollRect.ScrollPosition = 0f;
            // Unity convention: scrollDelta.y > 0 = wheel up. In OnScroll: newPos = pos - delta * sensitivity.
            // Positive delta.y → newPos = -35f → past the top boundary.
            var eventData = MakeScrollEvent(scrollDeltaY: 1f);

            // Act
            _scrollRect.OnScroll(eventData);

            // Assert: position goes negative (rubber-band), but the overshoot is bounded by
            // ViewportSize (RubberDelta asymptotically approaches viewSize).
            Assert.Less(_scrollRect.ScrollPosition, 0f,
                "Elastic wheel at the top boundary must produce a negative rubber-band overshoot.");
            Assert.GreaterOrEqual(_scrollRect.ScrollPosition, -ViewportHeight,
                "RubberDelta overshoot is bounded by ViewportSize.");
            Assert.AreEqual(0f, _scrollRect.Velocity,
                "Wheel must not leave residual velocity (judder-fix invariant).");
        }

        [Test]
        public void OnScroll_Elastic_AtBottomBound_AppliesRubberDeltaForPositiveOvershoot()
        {
            // Arrange: position at the bottom boundary, wheel down tries to go past maxScroll.
            // In Elastic mode there must be a rubber-band overshoot bounded by ViewportSize.
            var maxScroll = ContentHeight - ViewportHeight;
            _scrollRect.ScrollPosition = maxScroll;
            // scrollDelta.y < 0 = wheel down → newPos = maxScroll + 35f → past the bottom boundary.
            var eventData = MakeScrollEvent(scrollDeltaY: -1f);

            // Act
            _scrollRect.OnScroll(eventData);

            // Assert: position goes past maxScroll (rubber-band), overshoot ≤ ViewportSize.
            Assert.Greater(_scrollRect.ScrollPosition, maxScroll,
                "Elastic wheel at the bottom boundary must produce a positive rubber-band overshoot.");
            Assert.LessOrEqual(_scrollRect.ScrollPosition - maxScroll, ViewportHeight,
                "RubberDelta overshoot is bounded by ViewportSize.");
            Assert.AreEqual(0f, _scrollRect.Velocity,
                "Wheel must not leave residual velocity (judder-fix invariant).");
        }

        [Test]
        public void OnScroll_Elastic_ContinuousWheelAtBound_OvershootBoundedByViewport()
        {
            // Regression test for judder + new rubber-band semantics:
            // a long continuous wheel-down at the bottom boundary must NOT accumulate
            // velocity (judder-fix), but overshoot is allowed — bounded by ViewportSize
            // through RubberDelta. Velocity must be 0 at every step.
            var maxScroll = ContentHeight - ViewportHeight;
            _scrollRect.ScrollPosition = maxScroll - 50f;

            for (var i = 0; i < 20; i++)
            {
                var eventData = MakeScrollEvent(scrollDeltaY: -1f);
                _scrollRect.OnScroll(eventData);

                // Overshoot bounded: rubber-band asymptotically approaches ViewportSize,
                // never exceeding it.
                Assert.LessOrEqual(_scrollRect.ScrollPosition - maxScroll, ViewportHeight,
                    $"Iteration {i}: rubber-band overshoot exceeded ViewportSize.");
                Assert.GreaterOrEqual(_scrollRect.ScrollPosition, 0f,
                    $"Iteration {i}: position fell below 0.");
                // Judder-fix invariant: velocity must not accumulate between wheel events.
                Assert.AreEqual(0f, _scrollRect.Velocity,
                    $"Iteration {i}: velocity accumulated — judder via carryover.");
            }
        }

        [Test]
        public void OnScroll_Elastic_LateUpdateReturnsToMaxScrollAfterWheelStops()
        {
            // After rubber-band overshoot and the wheel input stops, the LateUpdate
            // elastic branch must return the position to maxScroll via SmoothDamp.
            var maxScroll = ContentHeight - ViewportHeight;
            _scrollRect.ScrollPosition = maxScroll;

            // Several wheel events past the boundary — produce a rubber-band overshoot.
            for (var i = 0; i < 5; i++)
            {
                _scrollRect.OnScroll(MakeScrollEvent(scrollDeltaY: -1f));
            }

            Assert.Greater(_scrollRect.ScrollPosition, maxScroll,
                "Sanity: rubber-band overshoot must be present before running LateUpdate.");

            // VLIST-03 wheel-active guard: simulate "enough time has passed since the last
            // wheel event". Reset _lastWheelTime to NegativeInfinity, otherwise the LateUpdate
            // elastic branch skips pull-back while input is active. In an EditMode unit test
            // Time.unscaledTime does not advance between OnScroll and LateUpdate, so without
            // this reset the guard would stay permanently active.
            SetPrivateField("_lastWheelTime", float.NegativeInfinity);

            // Run LateUpdate via reflection — simulate frames without wheel input.
            var lateUpdate = typeof(VirtualScrollRect).GetMethod(
                "LateUpdate",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(lateUpdate, "LateUpdate must be reachable via reflection.");

            // 200 frames is more than enough: SmoothDamp with _elasticity=0.1 at
            // unscaledDeltaTime≈0.02 converges to the target in a handful of frames. Headroom kept.
            for (var frame = 0; frame < 200; frame++)
            {
                lateUpdate.Invoke(_scrollRect, null);
                if (Mathf.Approximately(_scrollRect.ScrollPosition, maxScroll))
                {
                    break;
                }
            }

            // After SmoothDamp finishes the position must equal maxScroll (taking into account
            // VelocityStopThreshold=1 px/s LateUpdate exits when velocity is small and offset==0).
            Assert.AreEqual(maxScroll, _scrollRect.ScrollPosition, 1f,
                "LateUpdate elastic branch must return the position to maxScroll after wheel input.");
        }

        [Test]
        public void OnScroll_Elastic_DuringActiveWheelInput_LateUpdateDoesNotPullBack()
        {
            // VLIST-03 root cause regression: between discrete wheel events, the LateUpdate
            // elastic branch WITHOUT a guard actively yanks the position back to maxScroll
            // through SmoothDamp, while the next OnScroll re-rubber-compresses the overshoot
            // on the already-returned position → frame-by-frame push/pull oscillation
            // (visible judder). The _lastWheelTime/WheelActiveDuration guard must block
            // pull-back while input is active (mirrors the _isDragging path).
            var maxScroll = ContentHeight - ViewportHeight;
            _scrollRect.ScrollPosition = maxScroll;

            // First wheel event — produce rubber-band overshoot and mark
            // _lastWheelTime=Time.unscaledTime (so the guard becomes active).
            _scrollRect.OnScroll(MakeScrollEvent(scrollDeltaY: -1f));
            var overshootAfterFirstEvent = _scrollRect.ScrollPosition;
            Assert.Greater(overshootAfterFirstEvent, maxScroll,
                "Sanity: first wheel event must produce a positive overshoot.");

            // One LateUpdate tick between wheel events. The guard must block SmoothDamp
            // pull-back: position must NOT drop towards maxScroll.
            var lateUpdate = typeof(VirtualScrollRect).GetMethod(
                "LateUpdate",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(lateUpdate);
            lateUpdate.Invoke(_scrollRect, null);

            Assert.AreEqual(overshootAfterFirstEvent, _scrollRect.ScrollPosition, 0.0001f,
                "While wheel input is active, LateUpdate must NOT pull the position back to " +
                "maxScroll — otherwise judder.");
            // Velocity is explicitly zeroed in the guard branch — the next SmoothDamp on input
            // release must start from velocity=0 (clean rubber-release feel).
            Assert.AreEqual(0f, _scrollRect.Velocity,
                "The guard branch must explicitly zero velocity for a clean SmoothDamp start.");
        }

        [Test]
        public void OnScroll_Elastic_ResetsVelocityFromPreviousFrame()
        {
            // Judder-fix invariant: if on a previous frame the LateUpdate Elastic branch left
            // _velocity != 0 (drag-release / SmoothDamp at the boundary), the next wheel event
            // must reset velocity → SmoothDamp on the next LateUpdate starts from velocity=0
            // → no judder via carryover.
            SetPrivateField("_velocity", -500f);
            _scrollRect.ScrollPosition = 200f; // within bounds

            var eventData = MakeScrollEvent(scrollDeltaY: -1f);
            _scrollRect.OnScroll(eventData);

            Assert.AreEqual(0f, _scrollRect.Velocity,
                "Wheel must reset stale velocity carryover from the previous frame.");
        }

        [Test]
        public void OnScroll_Clamped_HardClampsAtTopBound()
        {
            // In Clamped mode wheel at the boundary is hard-clamped, no rubber-band.
            SetPrivateField("_movementType", MovementType.Clamped);
            _scrollRect.ScrollPosition = 0f;

            var eventData = MakeScrollEvent(scrollDeltaY: 1f); // wheel "above" zero
            _scrollRect.OnScroll(eventData);

            Assert.AreEqual(0f, _scrollRect.ScrollPosition,
                "Clamped wheel at the top boundary must hard-clamp to 0 with no overshoot.");
            Assert.AreEqual(0f, _scrollRect.Velocity);
        }

        [Test]
        public void OnScroll_Clamped_HardClampsAtBottomBound()
        {
            // Symmetric check: Clamped at the bottom boundary also hard-clamps.
            SetPrivateField("_movementType", MovementType.Clamped);
            var maxScroll = ContentHeight - ViewportHeight;
            _scrollRect.ScrollPosition = maxScroll;

            var eventData = MakeScrollEvent(scrollDeltaY: -1f); // wheel down
            _scrollRect.OnScroll(eventData);

            Assert.AreEqual(maxScroll, _scrollRect.ScrollPosition,
                "Clamped wheel at the bottom boundary must hard-clamp to maxScroll with no overshoot.");
            Assert.AreEqual(0f, _scrollRect.Velocity);
        }

        [Test]
        public void OnScroll_Unrestricted_AppliesRawDelta()
        {
            // In Unrestricted mode wheel applies the raw delta with no clamp and no rubber-band.
            // The position is allowed to go past any boundary.
            SetPrivateField("_movementType", MovementType.Unrestricted);
            var maxScroll = ContentHeight - ViewportHeight;
            _scrollRect.ScrollPosition = maxScroll;

            var eventData = MakeScrollEvent(scrollDeltaY: -1f); // wheel down → +35f
            _scrollRect.OnScroll(eventData);

            // Strict equality (raw delta, no transformations).
            Assert.AreEqual(maxScroll + Sensitivity, _scrollRect.ScrollPosition, 0.0001f,
                "Unrestricted wheel must apply the raw delta without clamp/rubber-band.");
            Assert.AreEqual(0f, _scrollRect.Velocity);
        }

        [Test]
        public void OnScroll_InMiddle_ShiftsByDeltaTimesSensitivity()
        {
            // Normal mid-content position — wheel must move the position by exactly
            // delta * sensitivity in every mode. Guarantees the new branch-by-MovementType
            // logic does not break correct in-bounds movement.
            const float startPos = 200f;
            _scrollRect.ScrollPosition = startPos;

            var eventData = MakeScrollEvent(scrollDeltaY: -1f); // wheel down — position grows
            _scrollRect.OnScroll(eventData);

            // _scrollPosition -= (-1f) * 35f = +35f.
            Assert.AreEqual(startPos + Sensitivity, _scrollRect.ScrollPosition, 0.0001f);
            Assert.AreEqual(0f, _scrollRect.Velocity);
        }

        [Test]
        public void OnScroll_ContentSmallerThanViewport_IsIgnored()
        {
            // Sanity check for the guard from 01-06: when contentHeight <= viewportSize
            // wheel must not change _scrollPosition (full scroll lock).
            _scrollRect.SetContentSize(ViewportHeight - 50f); // smaller than viewport
            _scrollRect.ScrollPosition = 0f;

            var eventData = MakeScrollEvent(scrollDeltaY: -1f);
            _scrollRect.OnScroll(eventData);

            Assert.AreEqual(0f, _scrollRect.ScrollPosition,
                "When contentHeight <= viewportHeight wheel must be fully ignored.");
        }

        // ---- helpers -------------------------------------------------------

        private PointerEventData MakeScrollEvent(float scrollDeltaY)
        {
            return new PointerEventData(EventSystem.current)
            {
                scrollDelta = new Vector2(0f, scrollDeltaY)
            };
        }

        private void SetPrivateField(string name, object value)
        {
            var field = typeof(VirtualScrollRect).GetField(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{name}' not found on VirtualScrollRect.");
            field.SetValue(_scrollRect, value);
        }
    }
}
