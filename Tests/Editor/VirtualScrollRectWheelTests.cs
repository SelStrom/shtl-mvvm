using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Shtl.Mvvm.Tests
{
    /// <summary>
    /// Регрессионные тесты на VLIST-03 (wheel rubber-band semantics).
    ///
    /// Контракт:
    /// - MovementType.Elastic: wheel за границу применяет RubberDelta к over-bound части
    ///   (асимптотическое сжатие, ограниченное ViewportSize), затем LateUpdate
    ///   возвращает к границе через SmoothDamp когда wheel-ввод прекращается.
    /// - MovementType.Clamped: hard-clamp в [0, maxScroll], без overshoot.
    /// - MovementType.Unrestricted: raw delta, без ограничений.
    /// - Judder-fix invariant: velocity всегда зануляется в OnScroll, не накапливается
    ///   между wheel events, LateUpdate elastic SmoothDamp стартует с velocity=0.
    /// </summary>
    [TestFixture]
    public class VirtualScrollRectWheelTests
    {
        // Поля и методы зачастую internal/private — используется reflection (как и в
        // существующем VirtualCollectionBindingTests). Сами тесты — на наблюдаемое
        // поведение через `internal` API (ScrollPosition / Velocity / SetContentSize).
        private const float ViewportHeight = 300f;
        private const float ContentHeight = 1000f;
        private const float Sensitivity = 35f;

        private GameObject _root;
        private VirtualScrollRect _scrollRect;
        private GameObject _eventSystemGo;

        [SetUp]
        public void SetUp()
        {
            // EventSystem нужен для конструктора PointerEventData.
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
            // Arrange: позиция в начале (top), wheel пытается уйти "выше" нуля.
            // В Elastic должен быть rubber-band overshoot, ограниченный ViewportSize.
            _scrollRect.ScrollPosition = 0f;
            // scrollDelta.y > 0 у Unity = wheel вверх. В OnScroll: newPos = pos - delta * sensitivity.
            // Положительный delta.y → newPos = -35f → за верхней границей.
            var eventData = MakeScrollEvent(scrollDeltaY: 1f);

            // Act
            _scrollRect.OnScroll(eventData);

            // Assert: позиция уходит в отрицательную область (rubber-band), но overshoot
            // ограничен ViewportSize (RubberDelta асимптотически приближается к viewSize).
            Assert.Less(_scrollRect.ScrollPosition, 0f,
                "Elastic wheel у верхней границы должен дать negative rubber-band overshoot.");
            Assert.GreaterOrEqual(_scrollRect.ScrollPosition, -ViewportHeight,
                "RubberDelta overshoot ограничен ViewportSize.");
            Assert.AreEqual(0f, _scrollRect.Velocity,
                "Wheel не должен оставлять velocity (judder-fix invariant).");
        }

        [Test]
        public void OnScroll_Elastic_AtBottomBound_AppliesRubberDeltaForPositiveOvershoot()
        {
            // Arrange: позиция у нижней границы, wheel вниз пытается уйти за maxScroll.
            // В Elastic должен быть rubber-band overshoot, ограниченный ViewportSize.
            var maxScroll = ContentHeight - ViewportHeight;
            _scrollRect.ScrollPosition = maxScroll;
            // scrollDelta.y < 0 = wheel вниз → newPos = maxScroll + 35f → за нижней границей.
            var eventData = MakeScrollEvent(scrollDeltaY: -1f);

            // Act
            _scrollRect.OnScroll(eventData);

            // Assert: позиция уходит за maxScroll (rubber-band), overshoot ≤ ViewportSize.
            Assert.Greater(_scrollRect.ScrollPosition, maxScroll,
                "Elastic wheel у нижней границы должен дать positive rubber-band overshoot.");
            Assert.LessOrEqual(_scrollRect.ScrollPosition - maxScroll, ViewportHeight,
                "RubberDelta overshoot ограничен ViewportSize.");
            Assert.AreEqual(0f, _scrollRect.Velocity,
                "Wheel не должен оставлять velocity (judder-fix invariant).");
        }

        [Test]
        public void OnScroll_Elastic_ContinuousWheelAtBound_OvershootBoundedByViewport()
        {
            // Регрессионный тест на judder + новая rubber-band семантика:
            // длительный непрерывный wheel вниз у нижней границы НЕ должен накапливать
            // velocity (judder-fix), но overshoot допустим — он ограничен ViewportSize
            // через RubberDelta. Velocity на каждом шаге == 0.
            var maxScroll = ContentHeight - ViewportHeight;
            _scrollRect.ScrollPosition = maxScroll - 50f;

            for (var i = 0; i < 20; i++)
            {
                var eventData = MakeScrollEvent(scrollDeltaY: -1f);
                _scrollRect.OnScroll(eventData);

                // Overshoot bounded: rubber-band асимптотически приближается к ViewportSize,
                // никогда его не превышая.
                Assert.LessOrEqual(_scrollRect.ScrollPosition - maxScroll, ViewportHeight,
                    $"Iteration {i}: rubber-band overshoot превысил ViewportSize.");
                Assert.GreaterOrEqual(_scrollRect.ScrollPosition, 0f,
                    $"Iteration {i}: позиция ушла ниже 0.");
                // Judder-fix invariant: velocity не накапливается между wheel events.
                Assert.AreEqual(0f, _scrollRect.Velocity,
                    $"Iteration {i}: velocity накопилась — judder через carryover.");
            }
        }

        [Test]
        public void OnScroll_Elastic_LateUpdateReturnsToMaxScrollAfterWheelStops()
        {
            // После rubber-band overshoot и прекращения wheel-ввода LateUpdate elastic-ветка
            // должна возвращать позицию к maxScroll через SmoothDamp.
            var maxScroll = ContentHeight - ViewportHeight;
            _scrollRect.ScrollPosition = maxScroll;

            // Несколько wheel events за границу — создаём rubber-band overshoot.
            for (var i = 0; i < 5; i++)
            {
                _scrollRect.OnScroll(MakeScrollEvent(scrollDeltaY: -1f));
            }

            Assert.Greater(_scrollRect.ScrollPosition, maxScroll,
                "Sanity: должен быть rubber-band overshoot до прогона LateUpdate.");

            // VLIST-03 wheel-active guard: имитируем «прошло достаточно времени с
            // последнего wheel event». Сбрасываем _lastWheelTime в NegativeInfinity,
            // иначе LateUpdate elastic-ветка пропускает pull-back пока ввод активен.
            // В EditMode unit-тесте Time.unscaledTime между OnScroll и LateUpdate
            // не «течёт», поэтому без сброса guard был бы вечно активен.
            SetPrivateField("_lastWheelTime", float.NegativeInfinity);

            // Прогоняем LateUpdate через рефлексию — имитируем кадры без wheel-ввода.
            var lateUpdate = typeof(VirtualScrollRect).GetMethod(
                "LateUpdate",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(lateUpdate, "LateUpdate должен быть доступен через рефлексию.");

            // 200 кадров заведомо достаточно: SmoothDamp с _elasticity=0.1 при unscaledDeltaTime≈0.02
            // сходится к target за единицы кадров. Берём запас.
            for (var frame = 0; frame < 200; frame++)
            {
                lateUpdate.Invoke(_scrollRect, null);
                if (Mathf.Approximately(_scrollRect.ScrollPosition, maxScroll))
                {
                    break;
                }
            }

            // После завершения SmoothDamp позиция должна быть == maxScroll (с учётом
            // VelocityStopThreshold=1 px/s LateUpdate выйдет когда velocity мала и offset==0).
            Assert.AreEqual(maxScroll, _scrollRect.ScrollPosition, 1f,
                "LateUpdate elastic-ветка должна вернуть позицию к maxScroll после wheel.");
        }

        [Test]
        public void OnScroll_Elastic_DuringActiveWheelInput_LateUpdateDoesNotPullBack()
        {
            // VLIST-03 root cause regression: между discrete wheel events LateUpdate
            // elastic-ветка БЕЗ guard'а активно тянет позицию обратно к maxScroll
            // через SmoothDamp, а следующий OnScroll re-rubber-сжимает overshoot
            // на уже-возвращённой позиции → frame-by-frame push/pull oscillation
            // (visible judder). Guard по _lastWheelTime/WheelActiveDuration должен
            // блокировать pull-back пока ввод активен (симметрия с _isDragging-путём).
            var maxScroll = ContentHeight - ViewportHeight;
            _scrollRect.ScrollPosition = maxScroll;

            // Первый wheel event — создаём rubber-band overshoot и маркируем
            // _lastWheelTime=Time.unscaledTime (значит guard активен).
            _scrollRect.OnScroll(MakeScrollEvent(scrollDeltaY: -1f));
            var overshootAfterFirstEvent = _scrollRect.ScrollPosition;
            Assert.Greater(overshootAfterFirstEvent, maxScroll,
                "Sanity: первый wheel event должен дать positive overshoot.");

            // Один LateUpdate-tick между wheel events. Guard должен заблокировать
            // SmoothDamp pull-back: позиция НЕ должна уменьшиться к maxScroll.
            var lateUpdate = typeof(VirtualScrollRect).GetMethod(
                "LateUpdate",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(lateUpdate);
            lateUpdate.Invoke(_scrollRect, null);

            Assert.AreEqual(overshootAfterFirstEvent, _scrollRect.ScrollPosition, 0.0001f,
                "Во время активного wheel-input LateUpdate НЕ должен тянуть позицию " +
                "обратно к maxScroll — иначе judder.");
            // Velocity явно обнулена в guard-ветке — следующий SmoothDamp при отпускании
            // ввода должен стартовать с velocity=0 (чистый rubber-release feel).
            Assert.AreEqual(0f, _scrollRect.Velocity,
                "Guard-ветка должна явно зануулить velocity для чистого старта SmoothDamp.");
        }

        [Test]
        public void OnScroll_Elastic_ResetsVelocityFromPreviousFrame()
        {
            // Judder-fix invariant: если в предыдущий кадр LateUpdate Elastic-ветка оставила
            // _velocity != 0 (drag-release / SmoothDamp у границы), следующий wheel event
            // должен сбросить velocity → SmoothDamp на следующем LateUpdate стартует
            // с velocity=0 → нет judder через carryover.
            SetPrivateField("_velocity", -500f);
            _scrollRect.ScrollPosition = 200f; // в пределах bounds

            var eventData = MakeScrollEvent(scrollDeltaY: -1f);
            _scrollRect.OnScroll(eventData);

            Assert.AreEqual(0f, _scrollRect.Velocity,
                "Wheel должен сбрасывать стэйл velocity carryover из предыдущего кадра.");
        }

        [Test]
        public void OnScroll_Clamped_HardClampsAtTopBound()
        {
            // В Clamped-режиме wheel у границы клампится жёстко, без rubber-band.
            SetPrivateField("_movementType", MovementType.Clamped);
            _scrollRect.ScrollPosition = 0f;

            var eventData = MakeScrollEvent(scrollDeltaY: 1f); // wheel "выше" нуля
            _scrollRect.OnScroll(eventData);

            Assert.AreEqual(0f, _scrollRect.ScrollPosition,
                "Clamped wheel у верхней границы должен hard-clamp в 0 без overshoot.");
            Assert.AreEqual(0f, _scrollRect.Velocity);
        }

        [Test]
        public void OnScroll_Clamped_HardClampsAtBottomBound()
        {
            // Симметричная проверка: Clamped у нижней границы тоже hard-clamp.
            SetPrivateField("_movementType", MovementType.Clamped);
            var maxScroll = ContentHeight - ViewportHeight;
            _scrollRect.ScrollPosition = maxScroll;

            var eventData = MakeScrollEvent(scrollDeltaY: -1f); // wheel вниз
            _scrollRect.OnScroll(eventData);

            Assert.AreEqual(maxScroll, _scrollRect.ScrollPosition,
                "Clamped wheel у нижней границы должен hard-clamp в maxScroll без overshoot.");
            Assert.AreEqual(0f, _scrollRect.Velocity);
        }

        [Test]
        public void OnScroll_Unrestricted_AppliesRawDelta()
        {
            // В Unrestricted-режиме wheel применяет raw delta, без clamp и без rubber-band.
            // Позиция может уходить за любые границы.
            SetPrivateField("_movementType", MovementType.Unrestricted);
            var maxScroll = ContentHeight - ViewportHeight;
            _scrollRect.ScrollPosition = maxScroll;

            var eventData = MakeScrollEvent(scrollDeltaY: -1f); // wheel вниз → +35f
            _scrollRect.OnScroll(eventData);

            // Точное равенство (raw delta, без любых преобразований).
            Assert.AreEqual(maxScroll + Sensitivity, _scrollRect.ScrollPosition, 0.0001f,
                "Unrestricted wheel должен применить raw delta без clamp/rubber-band.");
            Assert.AreEqual(0f, _scrollRect.Velocity);
        }

        [Test]
        public void OnScroll_InMiddle_ShiftsByDeltaTimesSensitivity()
        {
            // Нормальная середина — wheel должен двигать позицию ровно на delta * sensitivity
            // во всех режимах. Гарантирует, что новый branch-by-MovementType не ломает
            // корректное движение в bounds.
            const float startPos = 200f;
            _scrollRect.ScrollPosition = startPos;

            var eventData = MakeScrollEvent(scrollDeltaY: -1f); // wheel вниз — позиция растёт
            _scrollRect.OnScroll(eventData);

            // _scrollPosition -= (-1f) * 35f = +35f.
            Assert.AreEqual(startPos + Sensitivity, _scrollRect.ScrollPosition, 0.0001f);
            Assert.AreEqual(0f, _scrollRect.Velocity);
        }

        [Test]
        public void OnScroll_ContentSmallerThanViewport_IsIgnored()
        {
            // Sanity-check guard'а из 01-06: при contentHeight <= viewportSize wheel
            // не изменяет _scrollPosition (полная блокировка scroll'а).
            _scrollRect.SetContentSize(ViewportHeight - 50f); // меньше viewport
            _scrollRect.ScrollPosition = 0f;

            var eventData = MakeScrollEvent(scrollDeltaY: -1f);
            _scrollRect.OnScroll(eventData);

            Assert.AreEqual(0f, _scrollRect.ScrollPosition,
                "При contentHeight <= viewportHeight wheel должен полностью игнорироваться.");
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
            Assert.IsNotNull(field, $"Поле '{name}' не найдено на VirtualScrollRect.");
            field.SetValue(_scrollRect, value);
        }
    }
}
