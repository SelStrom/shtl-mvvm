using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shtl.Mvvm
{
    internal enum MovementType
    {
        Elastic,
        Clamped,
        Unrestricted
    }

    internal enum ScrollAxis
    {
        Vertical,
        Horizontal
    }

    public class VirtualScrollRect : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        // Inertia stop threshold: below 1 px/s motion is visually imperceptible.
        // Scale relative to ViewportSize if needed.
        private const float VelocityStopThreshold = 1f;

        // VLIST-03: "active" wheel-input window. If the last OnScroll happened more
        // recently than this, the LateUpdate elastic branch does NOT run the SmoothDamp
        // pull-back to the boundary — otherwise SmoothDamp keeps yanking the position
        // back between discrete wheel events while the next OnScroll re-rubber-compresses
        // the overshoot on the already-pulled-back position → frame-by-frame push/pull
        // oscillation (visible judder). Mirrors the _isDragging guard on the drag path.
        // 0.08s ≈ 5 frames at 60fps — covers the typical interval between wheel events
        // on mac touchpad/wheel (8-16ms); when input is released it feels like an
        // instant spring-back.
        private const float WheelActiveDuration = 0.08f;

        [SerializeField] private RectTransform _viewport;
        [SerializeField] private Scrollbar _scrollbar;
        [SerializeField] private ScrollAxis _axis = ScrollAxis.Vertical;
        [SerializeField] private bool _inertia = true;
        [SerializeField] private float _decelerationRate = 0.135f;
        [SerializeField] private float _elasticity = 0.1f;
        [SerializeField] private float _scrollSensitivity = 35f;
        [SerializeField] private MovementType _movementType = MovementType.Elastic;
        [SerializeField] private int _overscanCount = 2;
        [SerializeField] private float _spacing = 0f;

        private float _scrollPosition;
        private float _velocity;
        private float _contentHeight;
        private bool _isDragging;
        private float _prevDragPosition;
        private bool _updatingScrollbar;
        // VLIST-03: timestamp of the last wheel-input. NegativeInfinity = "never happened",
        // guarantees that the guard in LateUpdate is false by default (without active input
        // the pull-back must run immediately).
        private float _lastWheelTime = float.NegativeInfinity;

        private Action<float> _onScrollPositionChanged;

        internal float ScrollPosition
        {
            get => _scrollPosition;
            set
            {
                _scrollPosition = value;
                ClampScrollPosition();
                OnScrollPositionChanged();
            }
        }

        internal float Velocity => _velocity;

        internal int OverscanCount
        {
            get => _overscanCount;
            set => _overscanCount = value;
        }

        internal float Spacing => _spacing;

        internal ScrollAxis Axis => _axis;

        internal float ViewportSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_viewport == null)
                {
                    return 0f;
                }

                return _axis == ScrollAxis.Vertical
                    ? _viewport.rect.height
                    : _viewport.rect.width;
            }
        }

        internal RectTransform Viewport => _viewport;

        internal void SetContentSize(float size)
        {
            //todo content size should be invariant across scroll types
            _contentHeight = size;
            // Content shrank enough that the current position fell outside the valid range --
            // reset inertia so the Elastic return-to-bounds is clean and we avoid jolts from
            // residual velocity in the old direction (e.g. on rapid Add/Clear).
            var maxScroll = MaxScrollPosition();
            if (_scrollPosition > maxScroll || _scrollPosition < 0f)
            {
                _velocity = 0f;
            }

            ClampScrollPosition();
            OnScrollPositionChanged();
        }

        internal void ScrollTo(float position)
        {
            _velocity = 0f;
            _scrollPosition = position;
            ClampScrollPosition();
            OnScrollPositionChanged();
        }

        internal void ScrollToIndex(int index, Func<int, float> getOffset)
        {
            if (getOffset == null)
            {
                return;
            }

            var offset = getOffset(index);
            ScrollTo(offset);
        }

        internal void SetOnScrollPositionChanged(Action<float> callback)
        {
            _onScrollPositionChanged = callback;
        }

        // W-07: this method currently has no callers in Runtime/Editor/Samples. It is kept in the
        // API as an explicit hook for the "reset position on ViewModel switch" use case from
        // external code (binding or consumer). Whether to expose it to tests via InternalsVisibleTo
        // or to delete it should be decided when an actual caller appears.
        internal void ResetScroll()
        {
            _scrollPosition = 0f;
            _velocity = 0f;
            OnScrollPositionChanged();
        }

        private void OnEnable()
        {
            if (_scrollbar != null)
            {
                _scrollbar.onValueChanged.AddListener(OnScrollbarValueChanged);
                // W-07: sync scrollbar with the current _scrollPosition. If the scrollbar is
                // enabled after the position has already changed (ViewModel switch, component
                // re-enable), without this call the scrollbar would stay at value=0 until the
                // first OnScrollPositionChanged.
                UpdateScrollbar();
            }
        }

        private void OnDisable()
        {
            if (_scrollbar != null)
            {
                _scrollbar.onValueChanged.RemoveListener(OnScrollbarValueChanged);
            }
        }

        private void OnScrollbarValueChanged(float value)
        {
            if (_updatingScrollbar)
            {
                return;
            }

            var maxScroll = MaxScrollPosition();
            if (maxScroll > 0f)
            {
                var normalized = IsScrollbarInverted() ? 1f - value : value;
                _scrollPosition = normalized * maxScroll;
                OnScrollPositionChanged();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // _isDragging and _prevDragPosition are set BEFORE the guard so the Begin/End pair
            // stays symmetric and _prevDragPosition does not hold a stale value from a previous
            // drag session (important when content size changes mid-drag — see WR-01).
            _isDragging = true;
            _prevDragPosition = GetLocalPosition(eventData);

            // Scroll is unavailable — content fits inside the viewport, nothing to scroll.
            if (_contentHeight <= ViewportSize)
            {
                return;
            }

            _velocity = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Scroll is unavailable — content fits inside the viewport, nothing to scroll.
            if (_contentHeight <= ViewportSize)
            {
                return;
            }

            var pos = GetLocalPosition(eventData);
            var delta = pos - _prevDragPosition;
            _prevDragPosition = pos;

            var offset = CalculateOffset();
            if (_movementType == MovementType.Elastic && offset != 0f)
            {
                delta = RubberDelta(delta, ViewportSize);
            }

            // The scrollPosition shift is opposite to the drag direction (scrollbar-drag convention),
            // identical for both axes: dragging towards the positive local viewport coordinate
            // decreases scrollPosition (content visually moves with the finger).
            _scrollPosition -= delta;
            _velocity = -delta / Time.unscaledDeltaTime;

            OnScrollPositionChanged();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            // Scroll is unavailable — content fits inside the viewport, nothing to scroll.
            if (_contentHeight <= ViewportSize)
            {
                return;
            }

            float rawDelta;
            if (_axis == ScrollAxis.Vertical)
            {
                rawDelta = eventData.scrollDelta.y;
            }
            else
            {
                // Unity ScrollRect behaviour: in horizontal mode use .x, and if .x == 0
                // (standard mouse wheel without shift) fall back to .y.
                rawDelta = eventData.scrollDelta.x != 0f
                    ? eventData.scrollDelta.x
                    : eventData.scrollDelta.y;
            }

            var delta = rawDelta * _scrollSensitivity;
            var newPosition = _scrollPosition - delta;
            var maxScroll = MaxScrollPosition();

            // VLIST-03: wheel/touchpad at the boundary must give the same visual response as a
            // finger drag — asymptotic compression through RubberDelta for the over-bound part
            // in Elastic, hard-clamp in Clamped, no constraints in Unrestricted.
            // Velocity is unconditionally zeroed (judder-fix invariant): wheel does not accumulate
            // inertia between events; the LateUpdate elastic branch starts SmoothDamp from
            // velocity=0 every time — a clean spring-back without carryover.
            switch (_movementType)
            {
                case MovementType.Elastic:
                    if (newPosition < 0f)
                    {
                        _scrollPosition = -RubberDelta(-newPosition, ViewportSize);
                    }
                    else if (newPosition > maxScroll)
                    {
                        _scrollPosition = maxScroll + RubberDelta(newPosition - maxScroll, ViewportSize);
                    }
                    else
                    {
                        _scrollPosition = newPosition;
                    }
                    break;

                case MovementType.Clamped:
                    _scrollPosition = Mathf.Clamp(newPosition, 0f, maxScroll);
                    break;

                case MovementType.Unrestricted:
                default:
                    _scrollPosition = newPosition;
                    break;
            }

            _velocity = 0f;
            // VLIST-03: mark wheel-input as active. The LateUpdate elastic branch sees this
            // timestamp and skips SmoothDamp pull-back while input continues (see
            // WheelActiveDuration). Mirrors the _isDragging guard.
            _lastWheelTime = Time.unscaledTime;

            OnScrollPositionChanged();
        }

        private void LateUpdate()
        {
            if (_isDragging)
            {
                return;
            }

            // Unrestricted-mode with content that fits inside the viewport has no natural return
            // (ClampScrollPosition does nothing for Unrestricted, drag is blocked by the guard
            // in OnBeginDrag). Without this check residual velocity would drift _scrollPosition
            // forever — see WR-04.
            if (_movementType == MovementType.Unrestricted && _contentHeight <= ViewportSize)
            {
                _velocity = 0f;
                return;
            }

            var offset = CalculateOffset();
            // Break the loop only when there is no meaningful inertia and no offset for Elastic
            // return. SmoothDamp does not guarantee exact velocity zero (numerical integrator),
            // so compare against VelocityStopThreshold instead of strict equality with 0f.
            if (Mathf.Abs(_velocity) < VelocityStopThreshold && offset == 0f)
            {
                _velocity = 0f;
                return;
            }

            if (offset != 0f && _movementType == MovementType.Elastic)
            {
                // VLIST-03: while wheel-input is "active" (last event less than
                // WheelActiveDuration ago), skip SmoothDamp pull-back. Otherwise SmoothDamp
                // yanks the position back towards the boundary every frame while the next
                // OnScroll re-rubber-compresses the overshoot on the already-returned position
                // → judder. Velocity must be explicitly zeroed so that when the active window
                // ends SmoothDamp starts from a clean zero (rubber-release feel as in drag).
                if (Time.unscaledTime - _lastWheelTime < WheelActiveDuration)
                {
                    _velocity = 0f;
                    return;
                }

                var target = _scrollPosition - offset;
                _scrollPosition = Mathf.SmoothDamp(
                    _scrollPosition,
                    target,
                    ref _velocity,
                    _elasticity,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime
                );
            }
            else if (_inertia)
            {
                _velocity *= Mathf.Pow(_decelerationRate, Time.unscaledDeltaTime);
                _scrollPosition += _velocity * Time.unscaledDeltaTime;
            }
            else
            {
                _velocity = 0f;
            }

            ClampScrollPosition();
            OnScrollPositionChanged();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float CalculateOffset()
        {
            var maxScroll = MaxScrollPosition();
            if (_scrollPosition < 0f)
            {
                return _scrollPosition;
            }

            if (_scrollPosition > maxScroll)
            {
                return _scrollPosition - maxScroll;
            }

            return 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float RubberDelta(float overStretching, float viewSize)
        {
            if (viewSize <= 0f)
            {
                return 0f;
            }

            return (1f - 1f / (Mathf.Abs(overStretching) * 0.55f / viewSize + 1f))
                   * viewSize
                   * Mathf.Sign(overStretching);
        }

        private void ClampScrollPosition()
        {
            if (_movementType == MovementType.Clamped)
            {
                var maxScroll = MaxScrollPosition();
                _scrollPosition = Mathf.Clamp(_scrollPosition, 0f, maxScroll);
            }
        }

        private float GetLocalPosition(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _viewport,
                eventData.position,
                eventData.pressEventCamera,
                out var localPoint
            );
            return _axis == ScrollAxis.Vertical ? localPoint.y : localPoint.x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OnScrollPositionChanged()
        {
            _onScrollPositionChanged?.Invoke(_scrollPosition);
            UpdateScrollbar();
        }

        private void UpdateScrollbar()
        {
            if (_scrollbar == null)
            {
                return;
            }

            var viewportSize = ViewportSize;
            var needsScroll = _contentHeight > viewportSize + 0.001f;
            var scrollbarGo = _scrollbar.gameObject;
            if (scrollbarGo.activeSelf != needsScroll)
            {
                scrollbarGo.SetActive(needsScroll);
            }

            if (!needsScroll)
            {
                return;
            }

            var maxScroll = MaxScrollPosition();
            _updatingScrollbar = true;
            _scrollbar.size = viewportSize / _contentHeight;
            // For Elastic/Unrestricted _scrollPosition may go outside [0, maxScroll] (overshoot
            // during bounce). Clamp only for scrollbar normalization, so the indicator does not
            // wander beyond bounds or "stick" during Elastic return.
            var clamped = Mathf.Clamp(_scrollPosition, 0f, maxScroll);
            var normalized = maxScroll > 0f ? clamped / maxScroll : 0f;
            _scrollbar.value = IsScrollbarInverted() ? 1f - normalized : normalized;
            _updatingScrollbar = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsScrollbarInverted()
        {
            if (_scrollbar == null)
            {
                return false;
            }

            // value=0 in BottomToTop means the bottom, in RightToLeft means the right side;
            // _scrollPosition=0 corresponds to the start of the content, so inversion is required.
            return _scrollbar.direction == Scrollbar.Direction.BottomToTop
                   || _scrollbar.direction == Scrollbar.Direction.RightToLeft;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float MaxScrollPosition()
        {
            return Mathf.Max(0f, _contentHeight - ViewportSize);
        }
    }
}
