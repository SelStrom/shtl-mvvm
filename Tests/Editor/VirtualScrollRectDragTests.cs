using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Shtl.Mvvm.Tests
{
    /// <summary>
    /// Regression tests for the vertical-drag direction (natural-scroll convention).
    ///
    /// Contract:
    /// - Finger moves UP inside the viewport (local-y grows) → _scrollPosition grows
    ///   → list scrolls towards the bottom.
    /// - Finger moves DOWN (local-y shrinks) → _scrollPosition shrinks
    ///   → list scrolls towards the top (content visually follows the finger).
    /// - _velocity sign matches the drag delta (so LateUpdate inertia continues the
    ///   user gesture, not reverses it).
    /// </summary>
    [TestFixture]
    public class VirtualScrollRectDragTests
    {
        private const float ViewportHeight = 300f;
        private const float ContentHeight = 1000f;

        private GameObject _root;
        private VirtualScrollRect _scrollRect;
        private RectTransform _viewportRt;
        private GameObject _eventSystemGo;

        [SetUp]
        public void SetUp()
        {
            // EventSystem is required by the PointerEventData constructor.
            _eventSystemGo = new GameObject("EventSystem");
            _eventSystemGo.AddComponent<EventSystem>();

            _root = new GameObject("TestRoot");

            var viewportGo = new GameObject("Viewport");
            _viewportRt = viewportGo.AddComponent<RectTransform>();
            _viewportRt.SetParent(_root.transform);
            // Centred pivot/anchors at world origin. With pressEventCamera = null
            // RectTransformUtility.ScreenPointToLocalPointInRectangle treats screen
            // coordinates as world coordinates, so a screen point (0, localY) maps
            // deterministically to local (0, localY) inside _viewportRt.
            _viewportRt.anchorMin = new Vector2(0.5f, 0.5f);
            _viewportRt.anchorMax = new Vector2(0.5f, 0.5f);
            _viewportRt.pivot = new Vector2(0.5f, 0.5f);
            _viewportRt.sizeDelta = new Vector2(400f, ViewportHeight);
            _viewportRt.position = Vector3.zero;

            var scrollGo = new GameObject("ScrollRect");
            scrollGo.transform.SetParent(_root.transform);
            _scrollRect = scrollGo.AddComponent<VirtualScrollRect>();

            SetPrivateField("_viewport", _viewportRt);
            SetPrivateField("_axis", ScrollAxis.Vertical);
            SetPrivateField("_movementType", MovementType.Clamped);
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
        public void OnDrag_VerticalFingerDown_DecreasesScrollPosition()
        {
            // Natural-scroll: finger moves DOWN → content moves DOWN → _scrollPosition shrinks.
            _scrollRect.ScrollPosition = 200f;
            var startPos = _scrollRect.ScrollPosition;

            _scrollRect.OnBeginDrag(MakeDragEvent(localY: 0f));
            _scrollRect.OnDrag(MakeDragEvent(localY: -50f)); // finger moved 50 units DOWN

            Assert.Less(_scrollRect.ScrollPosition, startPos,
                "Finger moving down must decrease _scrollPosition (natural scroll).");
        }

        [Test]
        public void OnDrag_VerticalFingerUp_IncreasesScrollPosition()
        {
            // Natural-scroll: finger moves UP → content moves UP → _scrollPosition grows.
            _scrollRect.ScrollPosition = 200f;
            var startPos = _scrollRect.ScrollPosition;

            _scrollRect.OnBeginDrag(MakeDragEvent(localY: 0f));
            _scrollRect.OnDrag(MakeDragEvent(localY: 50f)); // finger moved 50 units UP

            Assert.Greater(_scrollRect.ScrollPosition, startPos,
                "Finger moving up must increase _scrollPosition (natural scroll).");
        }

        [Test]
        public void OnDrag_VerticalFingerDown_VelocitySignMatchesDelta()
        {
            // _velocity sign must follow the drag delta so LateUpdate inertia keeps
            // moving the content in the same direction as the gesture.
            _scrollRect.ScrollPosition = 200f;

            _scrollRect.OnBeginDrag(MakeDragEvent(localY: 0f));
            _scrollRect.OnDrag(MakeDragEvent(localY: -50f));

            Assert.Less(_scrollRect.Velocity, 0f,
                "Finger moving down must produce negative _velocity (matches delta sign).");
        }

        [Test]
        public void OnDrag_VerticalFingerUp_VelocitySignMatchesDelta()
        {
            _scrollRect.ScrollPosition = 200f;

            _scrollRect.OnBeginDrag(MakeDragEvent(localY: 0f));
            _scrollRect.OnDrag(MakeDragEvent(localY: 50f));

            Assert.Greater(_scrollRect.Velocity, 0f,
                "Finger moving up must produce positive _velocity (matches delta sign).");
        }

        // ---- helpers -------------------------------------------------------

        // Builds a PointerEventData whose `position` (screen space) maps to the
        // requested local-y inside _viewportRt. The viewport sits at world origin
        // with a centred pivot and pressEventCamera = null, so screen-y == local-y.
        private PointerEventData MakeDragEvent(float localY)
        {
            return new PointerEventData(EventSystem.current)
            {
                position = new Vector2(0f, localY),
                pressEventCamera = null
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
