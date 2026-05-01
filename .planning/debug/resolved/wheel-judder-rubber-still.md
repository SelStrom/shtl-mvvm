---
status: resolved
trigger: "нет все еще дергается (после rubber-band fix 31ddee1)"
created: 2026-05-01T00:00:00Z
updated: 2026-05-01T15:30:00Z
human_verified: true
---

## Current Focus

reasoning_checkpoint:
  hypothesis: |
    LateUpdate elastic-ветка SmoothDamp активно тянет _scrollPosition обратно
    к maxScroll между discrete wheel events. На следующем frame OnScroll берёт
    уже частично возвращённую позицию, добавляет delta, снова rubber-сжимает.
    Получается фрейм-за-фреймом push/pull oscillation → visible judder.
    Drag не страдает потому что LateUpdate early-return-ит при _isDragging;
    у wheel такого guard'а нет.
  confirming_evidence:
    - VirtualScrollRect.cs:295-298 — LateUpdate имеет guard ТОЛЬКО для _isDragging.
    - VirtualScrollRect.cs:320-330 — elastic-ветка SmoothDamp(_scrollPosition,
      target=_scrollPosition-offset, ref _velocity, _elasticity=0.1, ...) безусловно
      запускается если offset != 0, без проверки на «активный» wheel-input.
    - VirtualScrollRect.cs:288 — OnScroll зануляет _velocity, но НЕ выставляет
      никакого «active wheel» маркера, поэтому LateUpdate не знает что wheel-ввод
      ещё идёт.
    - Drag-путь (lines 197-222) обновляет _scrollPosition напрямую без отскока,
      потому что LateUpdate в этот момент return-ает на _isDragging guard.
  falsification_test: |
    Если в bound state выполнить OnScroll → LateUpdate → проверить ScrollPosition,
    она не должна откатиться к maxScroll, если защита по wheel-active работает.
    Без защиты ScrollPosition возвращается к maxScroll за 5-10 кадров SmoothDamp.
  fix_rationale: |
    Симметрия с drag: drag блокирует LateUpdate-возврат через _isDragging флаг,
    wheel должен делать то же самое через time-based флаг (нет аналога OnEndScroll).
    Threshold ~0.08s = ≈5 кадров при 60fps — достаточно чтобы покрыть промежуток
    между frame-by-frame wheel events типичного devices, но визуально мгновенно
    на отпускание (пользователь воспринимает spring-back как немедленный).
    Velocity по-прежнему = 0 — judder-fix invariant сохраняется.
  blind_spots:
    - Точное значение threshold (0.08s) — эвристика, может потребоваться tuning
      на реальном железе если у пользователя touchpad с большим интервалом между
      событиями (>80ms между кадрами scroll). Можно вынести в SerializeField если
      потребуется.
    - Time.unscaledTime в EditMode тестах течёт нормально, но через рефлексию
      LateUpdate вызывается синхронно — проверить что guard работает с _lastWheelTime
      установленным синхронно перед прогоном LateUpdate.

next_action: |
  1) Добавить _lastWheelTime + WheelActiveDuration const.
  2) Обновлять _lastWheelTime в OnScroll (после _velocity = 0).
  3) В LateUpdate elastic-ветке проверить
     Time.unscaledTime - _lastWheelTime < WheelActiveDuration → skip SmoothDamp.
  4) Обновить существующий LateUpdateReturnsToMaxScroll тест — выставить
     _lastWheelTime = -∞ перед LateUpdate.
  5) Добавить новый тест: DuringActiveWheelInput_LateUpdateDoesNotPullBack.

## Symptoms

expected: |
  При длительном wheel/touchpad scroll-вводе у boundary должен быть rubber-band
  feel (как у drag): плавное сжатие при overshoot, плавный возврат после
  прекращения ввода. Никакого видимого дрожания во время активного ввода.
actual: |
  «нет все еще дергается». Дрожание сохраняется после rubber-band fix (31ddee1).
  Симптом ВОСПРОИЗВОДИТСЯ: continuous wheel/touchpad scroll возле края.
  Симптом НЕ воспроизводится: drag.

errors: визуальный artifact

reproduction: |
  1. Sample, виртуальный список, MovementType=Elastic.
  2. Скроллить колесом непрерывно к краю и продолжать после maxScroll.
  3. Дрожание сохраняется даже на коммите 31ddee1 (rubber-band).

started: |
  После rubber-band fix (commit 31ddee1). User: «нет все еще дергается».

## Files of interest (initial scope)

- Runtime/Core/VirtualScroll/VirtualScrollRect.cs (OnScroll/LateUpdate)
- Tests/Editor/VirtualScrollRectWheelTests.cs (regression suite)

## Eliminated

- hypothesis: Stale DLL (как в wheel-judder-at-bounds-2)
  evidence: |
    User уже делал refresh — rubber-band feel появился (значит коммит 31ddee1
    активен). Если бы DLL была старая, не было бы rubber compression в принципе.
    Сейчас judder ОСТАЁТСЯ при наличии rubber feel — другая причина.
  timestamp: 2026-05-01T00:00:00Z

- hypothesis: Sensor noise / двойной callback от EventSystem.
  evidence: |
    OnScroll получает discrete события, пользователь видит judder ДАЖЕ при
    устойчивом cont. scroll. Если бы это был noise — дрожание было бы random,
    а не systematic frame-to-frame oscillation. Также drag (использует ту же
    EventSystem) не дрожит — это исключает источник на уровне input pipeline.
  timestamp: 2026-05-01T00:00:00Z

- hypothesis: Layout/recycling reentrancy (OnScrollPositionChanged → callback
    обратно вызывает OnScroll).
  evidence: |
    OnScrollPositionChanged вызывает только _onScrollPositionChanged?.Invoke
    и UpdateScrollbar. UpdateScrollbar защищён _updatingScrollbar guard от
    OnScrollbarValueChanged reentrancy. Внешний callback в Sample обновляет
    geometry, но не дёргает OnScroll. Изоляция: тот же Sample не имеет judder
    при drag → проблема НЕ в downstream pipeline, а именно в pull-back loop
    LateUpdate vs OnScroll.
  timestamp: 2026-05-01T00:00:00Z

## Evidence

- timestamp: 2026-05-01T00:00:00Z
  checked: VirtualScrollRect.cs:293-344 (LateUpdate)
  found: |
    LateUpdate имеет early-return ТОЛЬКО на _isDragging (line 295). Между wheel
    events этот guard ложный (Drag handlers не вовлечены), поэтому elastic-ветка
    выполняется на каждом кадре. SmoothDamp(_scrollPosition, target=pos-offset,
    ref velocity, elasticity=0.1) — типично сходится за 5-15 кадров, что больше
    чем длительность одного wheel event interval (≈8-16ms на mac touchpad/wheel).
  implication: |
    Между wheel events позиция активно движется обратно к bound. Следующий
    OnScroll берёт эту обновлённую позицию (а не original maxScroll+rubberDelta),
    добавляет новую delta, снова rubber-сжимает overshoot. Результат: на каждом
    кадре позиция «прыгает» — judder.

- timestamp: 2026-05-01T00:00:00Z
  checked: VirtualScrollRect.cs:255-291 (OnScroll Elastic branch)
  found: |
    OnScroll корректно применяет RubberDelta к overshoot и зануляет _velocity.
    Но не выставляет никакого маркера времени/состояния, который позволил бы
    LateUpdate понять что wheel-input ещё идёт. Аналог _isDragging для wheel
    отсутствует.
  implication: |
    Нужен time-based маркер (`_lastWheelTime`) обновляемый в OnScroll, и
    проверка в LateUpdate elastic-ветке: если прошло < threshold с последнего
    wheel event — пропустить SmoothDamp pull-back.

## Resolution

root_cause: |
  Между discrete wheel events LateUpdate elastic-ветка беспрепятственно запускает
  Mathf.SmoothDamp, который активно тянет _scrollPosition обратно к maxScroll.
  На следующем frame новый OnScroll читает уже-возвращённую позицию, добавляет
  wheel delta, заново rubber-сжимает overshoot. Результат: за один кадр позиция
  делает «шаг назад» (SmoothDamp) и «шаг вперёд» (OnScroll) — visible judder.
  Drag симптомов не имеет потому что LateUpdate early-return-ит при _isDragging,
  но для wheel такого guard'а до этого фикса не было.
fix: |
  Добавлен _lastWheelTime (init float.NegativeInfinity), обновляется в OnScroll
  после _velocity=0. В LateUpdate elastic-ветке pull-back через SmoothDamp
  пропускается если Time.unscaledTime - _lastWheelTime < WheelActiveDuration
  (0.08s). Это симметрия с drag-guard через _isDragging — оба пути блокируют
  LateUpdate-возврат во время активного ввода, чистый rubber-release играет
  только после паузы.
verification: |
  Headless EditMode tests на TestProject~ (Unity 2022.3.60f1):
  /tmp/wheel-judder-3.xml — 79/79 passed, 0 failed.
  Wheel-suite: 11/11 passed, включая новый OnScroll_Elastic_DuringActiveWheelInput_LateUpdateDoesNotPullBack.
  Существующий OnScroll_Elastic_LateUpdateReturnsToMaxScrollAfterWheelStops обновлён
  (сброс _lastWheelTime=NegativeInfinity перед прогоном LateUpdate-петли) и зелёный.
  Rubber-band semantic тесты (TopBound/BottomBound/ContinuousWheel) — без изменений, зелёные.
files_changed:
  - Runtime/Core/VirtualScroll/VirtualScrollRect.cs
  - Tests/Editor/VirtualScrollRectWheelTests.cs
