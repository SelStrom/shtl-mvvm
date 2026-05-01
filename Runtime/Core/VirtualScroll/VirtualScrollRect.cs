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
        // Порог остановки инерции: ниже 1 px/s движение визуально незаметно.
        // При необходимости масштабировать относительно ViewportSize.
        private const float VelocityStopThreshold = 1f;

        // VLIST-03: окно «активного» wheel-ввода. Если последний OnScroll был
        // ближе по времени, LateUpdate elastic-ветка НЕ запускает SmoothDamp
        // pull-back к границе — иначе между discrete wheel events SmoothDamp
        // активно возвращает позицию, а следующий OnScroll re-сжимает overshoot
        // на уже-возвращённой позиции → frame-by-frame push/pull oscillation
        // (visible judder). Симметрия с _isDragging guard'ом для drag-пути.
        // 0.08s ≈ 5 кадров при 60fps — покрывает типичный интервал между
        // wheel events на mac touchpad/wheel (8-16ms), при отпускании ввода
        // воспринимается как мгновенный spring-back.
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
        // VLIST-03: timestamp последнего wheel-input. NegativeInfinity = «никогда не было»,
        // гарантирует что guard в LateUpdate ложный по умолчанию (без активного ввода
        // pull-back должен работать сразу).
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
            //todo размер контента должен быть инвариантным для разных типов скролла
            _contentHeight = size;
            // Контент сжался настолько, что текущая позиция вышла за допустимые границы --
            // сбрасываем инерцию, чтобы Elastic-возврат был чистым и не происходило рывков
            // от продолжающейся velocity старого направления (например, при быстром Add/Clear).
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

        // W-07: метод сейчас не имеет вызывающих в Runtime/Editor/Samples. Оставлен в API
        // как явная точка для use-case "сбросить позицию при смене ViewModel" из внешнего кода
        // (binding или потребитель). При экспорте в тесты через InternalsVisibleTo или удалении
        // -- решать по факту появления вызывающего.
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
                // W-07: синхронизируем scrollbar с текущим _scrollPosition. Если scrollbar
                // включается после того, как позиция уже изменилась (смена ViewModel,
                // re-enable компонента), без этого вызова scrollbar остался бы в value=0
                // до первого OnScrollPositionChanged.
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
            // _isDragging и _prevDragPosition выставляем ДО guard'а, чтобы пара Begin/End
            // оставалась симметричной и _prevDragPosition не содержал stale-значения
            // из предыдущей drag-сессии (важно при динамическом изменении контента
            // во время drag — см. WR-01).
            _isDragging = true;
            _prevDragPosition = GetLocalPosition(eventData);

            // Скролл недоступен — контент помещается во viewport без прокрутки
            if (_contentHeight <= ViewportSize)
            {
                return;
            }

            _velocity = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Скролл недоступен — контент помещается во viewport без прокрутки
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

            // Сдвиг scrollPosition противоположен направлению drag (scrollbar-drag convention),
            // одинаково для обеих осей: drag в положительную сторону локальной координаты viewport
            // уменьшает scrollPosition (контент визуально сдвигается за пальцем).
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
            // Скролл недоступен — контент помещается во viewport без прокрутки
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
                // Поведение Unity ScrollRect: для горизонтального режима использовать .x,
                // а если .x == 0 (стандартное колесо мыши без shift) — fallback на .y.
                rawDelta = eventData.scrollDelta.x != 0f
                    ? eventData.scrollDelta.x
                    : eventData.scrollDelta.y;
            }

            var delta = rawDelta * _scrollSensitivity;
            var newPosition = _scrollPosition - delta;
            var maxScroll = MaxScrollPosition();

            // VLIST-03: wheel/touchpad на границе должен давать тот же визуальный отклик,
            // что и drag пальцем — асимптотическое сжатие через RubberDelta для over-bound
            // части в Elastic, hard-clamp в Clamped, без ограничений в Unrestricted.
            // Velocity безусловно зануляется (judder-fix invariant): wheel не накапливает
            // инерцию между событиями, LateUpdate elastic-ветка стартует SmoothDamp
            // от velocity=0 каждый раз — чистый spring-back без carryover.
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
            // VLIST-03: маркируем активный wheel-input. LateUpdate elastic-ветка
            // увидит этот timestamp и пропустит SmoothDamp pull-back пока ввод
            // продолжается (см. WheelActiveDuration). Симметрия с _isDragging guard.
            _lastWheelTime = Time.unscaledTime;

            OnScrollPositionChanged();
        }

        private void LateUpdate()
        {
            if (_isDragging)
            {
                return;
            }

            // Unrestricted-mode при контенте, помещающемся во viewport, не имеет естественного
            // возврата (ClampScrollPosition не клампит для Unrestricted, drag заблокирован
            // guard'ом в OnBeginDrag). Без этой проверки остаточная velocity будет уплывать
            // _scrollPosition бесконечно — см. WR-04.
            if (_movementType == MovementType.Unrestricted && _contentHeight <= ViewportSize)
            {
                _velocity = 0f;
                return;
            }

            var offset = CalculateOffset();
            // Прерываем цикл только если нет ни значимой инерции, ни offset для Elastic-возврата.
            // SmoothDamp не гарантирует точного обнуления velocity (численный интегратор),
            // поэтому сравниваем по порогу VelocityStopThreshold, а не на точное равенство 0f.
            if (Mathf.Abs(_velocity) < VelocityStopThreshold && offset == 0f)
            {
                _velocity = 0f;
                return;
            }

            if (offset != 0f && _movementType == MovementType.Elastic)
            {
                // VLIST-03: пока wheel-ввод «активен» (последнее событие < WheelActiveDuration
                // назад), пропускаем SmoothDamp pull-back. Иначе SmoothDamp каждый кадр
                // тянет позицию обратно к границе, а следующий OnScroll re-rubber-сжимает
                // overshoot на уже-возвращённой позиции → judder. Velocity нужно явно
                // обнулить, чтобы при выходе из окна активности SmoothDamp стартовал
                // с чистого нуля (rubber-release feel как у drag).
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
            // Для Elastic/Unrestricted _scrollPosition может выходить за [0, maxScroll]
            // (overshoot во время отскока). Кламним только для нормализации scrollbar,
            // чтобы индикатор не уходил за границы и не "застревал" во время Elastic-возврата.
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

            // value=0 у BottomToTop означает низ, у RightToLeft — правую сторону;
            // _scrollPosition=0 = начало контента, поэтому требуется инверсия.
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
