---
phase: 01-virtualized-list
verified: 2026-05-01T12:00:00Z
resolved: 2026-05-01T15:30:00Z
status: verified
score: 7/7 must-haves verified
overrides_applied: 0
resolution_summary: "Оба BLOCKER-гэпа закрыты после исходной верификации — см. раздел 'Resolution Update' в конце документа. UAT 01-UAT.md обновлён (status: complete, 7/8 pass + 1 skip с reason)."
gaps:
  - truth: "Все EditMode-тесты фазы запускаются и проходят зелёными в Unity Test Runner Sample-проекта"
    status: resolved
    reason: |
      Согласно known_uat_state пользователь вручную верифицировал: EditMode тесты по-прежнему не
      появляются в Test Runner после добавления testables в manifest.json. PlayMode тесты
      обнаруживаются, но часть падает. Asmdef имеет includePlatforms=[] (не ограничен Editor),
      поэтому тесты выходят только в PlayMode-режиме; для EditMode нужна либо явная запись
      includePlatforms:["Editor"], либо инфраструктурный fix иного рода.
    artifacts:
      - path: "Tests/Runtime/Shtl.Mvvm.Tests.Runtime.asmdef"
        issue: "includePlatforms=[] — тесты доступны в PlayMode, но EditMode не показывает их в Test Runner Sample-проекта после testables opt-in"
    missing:
      - "Установить includePlatforms: [\"Editor\"] в Tests/Runtime/Shtl.Mvvm.Tests.Runtime.asmdef ИЛИ исследовать альтернативную причину отсутствия EditMode-тестов"
      - "Убедиться, что все 68 тестов (14+29+7+18) проходят в Test Runner после fix"
      - "Исправить падающие PlayMode тесты (или перенести их в EditMode-only режим)"

  - truth: "После drag или wheel за пределы допустимого диапазона [0, maxScroll] viewport SmoothDamp-возвращается в крайнюю позицию и фиксируется (Elastic bounce работает без velocity)"
    status: resolved
    reason: |
      Согласно known_uat_state пользователь верифицировал: drag-overscroll работает корректно,
      но wheel-overscroll вызывает oscillation (пружинит на границе) вместо плавной фиксации.
      Root cause из code review WR-02: точное сравнение `_velocity == 0f` ненадёжно —
      SmoothDamp может вернуть ~1.4e-7f после Elastic return. Это значение:
      (1) делает guard `_velocity == 0f && offset == 0f` ложным,
      (2) code попадает в else-if (_inertia) ветку с tiny velocity,
      (3) происходит 80-кадровое затухание с микро-движениями, вызывающими re-trigger
      UpdateVisibleRange и визуально воспринимаемое oscillation на границе.
      План 06 исправил гейт LateUpdate и velocity stop threshold, но не решил проблему
      epsilon-velocity после SmoothDamp.
    artifacts:
      - path: "Runtime/Core/VirtualScroll/VirtualScrollRect.cs"
        issue: |
          Строка 258: `if (_velocity == 0f && offset == 0f)` — точное float-сравнение.
          Строка 285: `if (Mathf.Abs(_velocity) < 1f && offset == 0f)` — порог 1f px/s не
          ловит epsilon после SmoothDamp (~1.4e-7f), SmoothDamp завершает работу без
          точного нуля. WR-01: OnBeginDrag не устанавливает _isDragging до guard — stale
          _prevDragPosition при динамическом contentHeight.
    missing:
      - "Заменить `_velocity == 0f` на `Mathf.Abs(_velocity) < VelocityStopThreshold` в guard LateUpdate (WR-02)"
      - "Объединить guard и velocity stop threshold в единый ранний return с Mathf.Abs(_velocity) < threshold (WR-02 fix)"
      - "Исправить WR-01: установить _isDragging = true ДО guard в OnBeginDrag, обнулять velocity ПОСЛЕ guard"
      - "Провести ручную верификацию после fix: wheel-overscroll → отпустить → список плавно фиксируется без oscillation"
deferred: []
human_verification:
  - test: "Профайлер: GC.Alloc = 0 при прокрутке"
    expected: "В Development Build Profiler GC.Alloc = 0 байт в кадрах активного скролла"
    why_human: "Измерение GC в Editor нерепрезентативно (per memory note пользователя). Требует Development Build и отдельного PlayMode smoke test."

  - test: "Список из 10000 элементов скроллится плавно"
    expected: "Стабильный FPS без просадок, в иерархии присутствуют только видимые элементы + overscan"
    why_human: "Требует Sample-сцены (фаза 2) с 10000+ элементами. Sample-сцены нет до завершения Phase 2."
---

# Phase 01: Виртуализированный список — Verification Report

**Phase Goal:** Разработчик может отображать большие коллекции данных (1000+ элементов) с плавным скроллом и минимальным расходом памяти
**Verified:** 2026-05-01T12:00:00Z
**Status:** gaps_found
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths (Roadmap Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|---------|
| SC-1 | Список из 10000 элементов скроллится плавно, одновременно существует только видимые + overscan | ? UNCERTAIN | Код реализован, Sample-сцена не существует до Phase 2. Требует человека. |
| SC-2 | При скролле View-элементы переиспользуются из recycling pool | ✓ VERIFIED | ViewRecyclingPool со Stack-based хранилищем. Release() = Dispose+SetActive(false)+Push. Get() = Pop+SetActive(true) без новых объектов при непустом пуле. |
| SC-3 | Добавление/удаление в ReactiveList автоматически обновляет список без ручной синхронизации | ✓ VERIFIED | VirtualCollectionBinding подключается к ReactiveList.Connect(onContentChanged, onElementAdded, onElementReplaced, onElementRemoved). Все события обрабатываются. |
| SC-4 | Элементы переменной высоты корректно позиционируются и скроллятся без артефактов | ✓ VERIFIED | LayoutCalculator.Rebuild(count, Func<int,float>) с prefix sum. Binary search. GetItemOffset для переменной высоты. VirtualCollectionBinding определяет isFixed и выбирает путь. |
| SC-5 | Профайлер не показывает GC-аллокаций в hot path скролла | ? UNCERTAIN | Zero-alloc grep подтверждён: нет LINQ в VirtualScroll/ и VirtualCollectionBinding.cs. GetHeightProvider() создаёт Func-делегат при мутациях (не hot scroll path — допустимо). Финальное подтверждение требует Development Build Profiler. |

**Roadmap SC score: 3 VERIFIED, 2 UNCERTAIN (требуют человека)**

### Plan Must-Have Truths

| # | Truth | Status | Evidence |
|---|-------|--------|---------|
| P1-T1 | ReactiveVirtualList содержит ReactiveList Items, ReactiveValue ScrollPosition, FirstVisibleIndex, VisibleCount | ✓ VERIFIED | Строки 12-15 ReactiveVirtualList.cs: все четыре публичных readonly поля |
| P1-T2 | ReactiveVirtualList реализует IReactiveValue с делегированием Dispose/Unbind | ✓ VERIFIED | Строка 6: `: IReactiveValue`. Dispose() делегирует ко всем 4 полям, Unbind() аналогично. |
| P1-T3 | ReactiveVirtualList поддерживает фиксированную и переменную высоту | ✓ VERIFIED | Три конструктора: float fixedHeight, Func<int,float> heightProvider, default. GetItemHeight с AggressiveInlining. |
| P1-T4 | LayoutCalculator строит prefix sum и определяет видимый диапазон через binary search | ✓ VERIFIED | Строка 263: `lo + (hi - lo) / 2`. Метод BinarySearchFirstVisible. FindVisibleRange с overscan + clamping. |
| P1-T5 | LayoutCalculator для фиксированной высоты считает visible range за O(1) | ✓ VERIFIED | Строки 189-202: `if (_fixedHeight > 0f)` fast path: firstVisible = (int)(scrollPosition / stride). |
| P1-T6 | Тесты ReactiveVirtualList и LayoutCalculator проходят через NUnit | ? UNCERTAIN | Файлы созданы: 14+29 = 43 теста. Согласно known_uat_state PlayMode тесты обнаруживаются, но EditMode нет + некоторые падают. Нельзя подтвердить "проходят" без зелёного Test Runner. |
| P2-T1 | VirtualScrollRect обрабатывает drag/scroll events с инерцией и elastic bounce | PARTIAL | Drag inertia работает. Elastic после drag работает. **Elastic после wheel — oscillation** (known_uat_state gap-2 PARTIALLY closed). |
| P2-T2 | VirtualScrollRect конфигурируется через SerializeField | ✓ VERIFIED | Строки 28-34: inertia, decelerationRate=0.135f, elasticity=0.1f, scrollSensitivity=35f, movementType=Elastic, overscanCount=2. |
| P2-T3 | ViewRecyclingPool переиспользует View без создания новых при наличии пула | ✓ VERIFIED | Stack<TWidgetView> _pool. Get(): `_pool.Count > 0 → Pop + SetActive(true)`. |
| P2-T4 | ViewRecyclingPool использует IWidgetViewFactory при пустом пуле | ✓ VERIFIED | Get(): `_factory != null ? _factory.CreateWidget(default) : Object.Instantiate(_prefab, _parent)`. |
| P3-T1 | VirtualCollectionBinding подключается к ReactiveList.Connect() | ✓ VERIFIED | Строки 63-68: `_vmList.Items.Connect(onContentChanged, onElementAdded, onElementReplaced, onElementRemoved)` — instance методы. |
| P3-T2 | При скролле VirtualCollectionBinding обновляет только элементы в/из viewport | ✓ VERIFIED | UpdateVisibleRange: _indicesToRemove буфер, Release выходящих, Get входящих. |
| P3-T3 | Extension .To() позволяет `Bind.From(vm.Items).To(prefab, scrollRect)` | ✓ VERIFIED | VirtualListBindExtensions.cs: два To() extension-метода, GetOrCreate().Connect(), from.LinkTo(binding). |
| P3-T4 | Zero-alloc в hot path: нет LINQ, boxing, new в UpdateVisibleRange/LateUpdate | ✓ VERIFIED | grep: нет LINQ в VirtualScroll/ и VirtualCollectionBinding.cs. Vector2 — struct (stack). _indicesToRemove предаллоцирован. |
| P3-T5 | При мутации выше viewport scroll position корректируется | ✓ VERIFIED | OnElementAdded: строки 103-105 += height+spacing. OnElementRemoved: строки 141-143 -= height+spacing. |
| P5-T1 | Unity Test Runner показывает тест-сьюты ReactiveVirtualListTests, LayoutCalculatorTests, ViewRecyclingPoolTests, VirtualCollectionBindingTests | ✗ FAILED | known_uat_state: EditMode тесты не появляются. PlayMode частично падают. |
| P6-T1 | После drag/wheel за [0, maxScroll] SmoothDamp возвращает viewport и фиксирует | PARTIAL | Drag-overscroll: RESOLVED. Wheel-overscroll: oscillation (WR-02: epsilon-velocity после SmoothDamp). |
| P6-T2 | contentHeight <= viewportHeight: drag и wheel не изменяют _scrollPosition | ✓ VERIFIED | Строки 178, 191, 223: guard `if (_contentHeight <= ViewportSize) return;` в OnBeginDrag, OnDrag, OnScroll. |

**Plan must-have score: 13/18 труф верифицированы, 2 FAILED (BLOCKER), 2 UNCERTAIN, 1 PARTIAL (BLOCKER)**

**Итоговый счёт: 5/7 ключевых целей** (TEST-04 полностью провален, VLIST-03 частично — wheel oscillation)

---

## Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Runtime/Core/Types/ReactiveVirtualList.cs` | ViewModel-тип виртуализированного списка | ✓ VERIFIED | Полная реализация: IReactiveValue, 4 поля, 3 конструктора, GetItemHeight, proxy-методы |
| `Runtime/Core/VirtualScroll/LayoutCalculator.cs` | Prefix sum + binary search | ✓ VERIFIED | internal struct, _prefixHeights, FindVisibleRange, O(1) fast path, binary search |
| `Runtime/Core/VirtualScroll/VirtualScrollRect.cs` | MonoBehaviour кастомного скролла | ✓ VERIFIED | IBeginDrag/IDrag/IEndDrag/IScrollHandler, SerializeField конфиг, SmoothDamp, RubberDelta |
| `Runtime/Core/VirtualScroll/ViewRecyclingPool.cs` | Stack-based пул View | ✓ VERIFIED | Stack<TWidgetView>, Get/Release с AggressiveInlining, DisposeAll |
| `Runtime/Core/Bindings/VirtualCollectionBinding.cs` | Интеграционный биндинг | ✓ VERIFIED | AbstractEventBinding, _activeViews Dictionary, _indicesToRemove буфер, все обработчики событий |
| `Runtime/Utils/VirtualListBindExtensions.cs` | Extension .To() | ✓ VERIFIED | 2 перегрузки To(), ArgumentNullException guard, from.LinkTo(binding) |
| `Tests/Runtime/Shtl.Mvvm.Tests.Runtime.asmdef` | Assembly definition для тестов | ✓ EXISTS / ✗ PARTIAL | Существует, name="Shtl.Mvvm.Tests.Runtime", но includePlatforms=[] → EditMode не работает |
| `Tests/Runtime/ReactiveVirtualListTests.cs` | Unit-тесты ReactiveVirtualList | ✓ VERIFIED | 14 тестов (>= 10) |
| `Tests/Runtime/LayoutCalculatorTests.cs` | Unit-тесты LayoutCalculator | ✓ VERIFIED | 29 тестов (>= 10) |
| `Tests/Runtime/ViewRecyclingPoolTests.cs` | Unit-тесты recycling pool | ✓ VERIFIED | 7 тестов (>= 5) |
| `Tests/Runtime/VirtualCollectionBindingTests.cs` | Unit-тесты биндинга | ✓ VERIFIED | 18 тестов (>= 7) |

---

## Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| ReactiveVirtualList.cs | ReactiveList.cs | Items field | ✓ WIRED | Строка 12: `public readonly ReactiveList<TElement> Items = new()` |
| ReactiveVirtualList.cs | IReactiveValue | реализация | ✓ WIRED | Строка 6: `: IReactiveValue` с Dispose/Unbind |
| VirtualScrollRect.cs | UnityEngine.EventSystems | IBeginDrag...IScrollHandler | ✓ WIRED | Строка 23: все 4 интерфейса |
| ViewRecyclingPool.cs | IWidgetViewFactory | CreateWidget/RemoveWidget | ✓ WIRED | Строка 12: поле _factory. Get() вызывает _factory.CreateWidget. |
| VirtualCollectionBinding.cs | ReactiveList.Connect() | instance methods | ✓ WIRED | Строки 63-68: Connect с именованными параметрами — instance methods, не lambda |
| VirtualCollectionBinding.cs | LayoutCalculator | FindVisibleRange | ✓ WIRED | Строка 211: `_layoutCalculator.FindVisibleRange(...)` |
| VirtualCollectionBinding.cs | ViewRecyclingPool | Get/Release | ✓ WIRED | Строки 230, 244: `_recyclingPool.Release(...)`, `_recyclingPool.Get()` |
| VirtualCollectionBinding.cs | VirtualScrollRect | SetOnScrollPositionChanged | ✓ WIRED | Строка 61: `_scrollRect.SetOnScrollPositionChanged(OnScrollPositionChanged)` |
| VirtualListBindExtensions.cs | BindFrom<ReactiveVirtualList<T>> | extension .To() | ✓ WIRED | this-параметр в обоих To() методах |
| Samples~/Sample/Packages/manifest.json | Tests/Runtime/Shtl.Mvvm.Tests.Runtime.asmdef | testables field | ✓ EXISTS / ✗ NOT_WIRED | testables содержит "com.shtl.mvvm", но EditMode дискавери не работает. |
| LateUpdate | CalculateOffset() | `_velocity == 0f && offset == 0f` | ✓ WIRED | Строка 258: двойное условие на месте |
| OnDrag/OnScroll/OnBeginDrag | ViewportSize guard | `_contentHeight <= ViewportSize` | ✓ WIRED | Строки 178, 191, 223 |

---

## Data-Flow Trace (Level 4)

Все артефакты, рендерящие динамические данные, являются Unity MonoBehaviour компонентами без внешней БД. Данные поступают из ReactiveList → VirtualCollectionBinding → ViewRecyclingPool → AbstractWidgetView. Цепочка замкнута через ReactiveList.Connect events и SetOnScrollPositionChanged callback. Источник данных — пользовательский код (Widget-слой). Статичных возвратов нет.

---

## Behavioral Spot-Checks

_Пропущено: все ключевые компоненты — Unity MonoBehaviour, требуют Play Mode. Проверка grep-based подтвердила структуру кода._

| Behaviour | Check | Result | Status |
|-----------|-------|--------|--------|
| Zero-alloc LINQ | `grep -rn ".Select\|.Where\|.ToList\|.ToArray" Runtime/Core/VirtualScroll/ VirtualCollectionBinding.cs` | Нет совпадений | ✓ PASS |
| gap-2 LateUpdate гейт | `grep -n "_velocity == 0f && offset == 0f" VirtualScrollRect.cs` | Строка 258 | ✓ PASS (код на месте) |
| gap-3 guard | `grep -n "_contentHeight <= ViewportSize" VirtualScrollRect.cs` | 3 строки: 178, 191, 223 | ✓ PASS |
| testables в manifest | `grep "testables" Samples~/Sample/Packages/manifest.json` | Найден | ✓ PASS (код на месте) |
| Wheel oscillation | Ручная верификация пользователя | Oscillation подтверждён | ✗ FAIL |
| EditMode Test Runner | Ручная верификация пользователя | Тесты не показываются | ✗ FAIL |

---

## Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|---------|
| VLIST-01 | 01-01, 01-03 | Виртуализированный список рендерит только видимые + overscan | ✓ SATISFIED | LayoutCalculator.FindVisibleRange с overscan. VirtualCollectionBinding.UpdateVisibleRange. |
| VLIST-02 | 01-02, 01-03 | Элементы переиспользуются через recycling pool | ✓ SATISFIED | ViewRecyclingPool Stack. VirtualCollectionBinding.Release/Get. |
| VLIST-03 | 01-02, 01-06 | Поддержка вертикальной прокрутки | PARTIAL | Drag + scroll обрабатываются. Elastic после drag работает. **Elastic после wheel — oscillation (gap-2 PARTIALLY closed)**. |
| VLIST-04 | 01-01 | Поддержка элементов переменной высоты/ширины | ✓ SATISFIED | LayoutCalculator с Func<int,float>. ReactiveVirtualList(Func<int,float>). ScrollAxis.Horizontal поддержан. |
| VLIST-05 | 01-03 | Интеграция с ReactiveList через события add/replace/remove | ✓ SATISFIED | VirtualCollectionBinding.Connect(): onElementAdded, onElementReplaced, onElementRemoved, onContentChanged. |
| VLIST-06 | 01-02, 01-03 | Корректная работа с IWidgetViewFactory | ✓ SATISFIED | ViewRecyclingPool(IWidgetViewFactory). VirtualListBindExtensions.To(factory, scrollRect). |
| VLIST-07 | 01-04 | Zero-alloc в hot path скролла | ✓ SATISFIED (code) / ? (runtime) | grep: нет LINQ/boxing в hot path. GetHeightProvider() аллоцирует Func при мутациях (не hot path). Runtime подтверждение отложено до Development Build Profiler. |
| TEST-04 | 01-01, 01-03, 01-05 | Unit-тесты виртуализированного списка | ✗ BLOCKED | EditMode тесты не появляются в Test Runner. PlayMode частично падают. 68 тестов написаны, но не верифицированы как зелёные. |

---

## Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| VirtualScrollRect.cs | 88 | `//todo` комментарий — незакрытый TODO | Info | Низкий — не блокирует функциональность |
| VirtualScrollRect.cs | 9 | `internal enum MovementType` | Warning | Plan 02 spec требовал public enum. Enum стал internal. VirtualCollectionBinding и тесты находятся в том же assembly (Shtl.Mvvm) — функционально работает. |
| VirtualScrollRect.cs | 45, 56 | `internal float ScrollPosition`, `internal float Velocity` | Warning | Plan 02 spec требовал public. Стали internal. Доступно внутри Shtl.Mvvm assembly, но не из потребительского кода. Все вызывающие в том же assembly — функционально OK. |
| VirtualCollectionBinding.cs | 380-383 | `GetHeightProvider()` возвращает `_vmList.GetItemHeight` — method group conversion создаёт Func-делегат | Warning | Аллоцирует при каждом вызове (мутации, не hot scroll path). VLIST-07 формально о "hot path скролла" — мутационный путь выходит за границы требования. |
| VirtualScrollRect.cs | 175-186 | WR-01: `_isDragging = true` ПОСЛЕ guard в OnBeginDrag | Warning | Stale `_prevDragPosition` возможен при динамическом изменении contentHeight во время drag. Известный риск из REVIEW.md WR-01, не исправлен в plan 06. |

---

## Human Verification Required

### 1. EditMode Test Runner

**Test:** Открыть Unity Editor → Window → General → Test Runner → EditMode. Убедиться что видны все 4 тест-сьюта.
**Expected:** ReactiveVirtualListTests (14), LayoutCalculatorTests (29), ViewRecyclingPoolTests (7), VirtualCollectionBindingTests (18) — итого 68 тестов. Run All → все зелёные.
**Why human:** Инфраструктурная проблема (asmdef includePlatforms, testables механизм) требует интерпретации Unity Editor поведения.

### 2. Wheel Elastic Bounce без oscillation

**Test:** В Play Mode прокрутить список колесом мыши за нижнюю или верхнюю границу. Отпустить.
**Expected:** Список плавно и без колебаний возвращается в крайнюю позицию (0 или maxScroll) и останавливается.
**Why human:** Oscillation — визуальный артефакт (epsilon-velocity), не верифицируется grep-ом.

### 3. Профайлер GC.Alloc при скролле

**Test:** Development Build → Profiler → запись при активном скролле → колонка GC.Alloc.
**Expected:** 0 байт в кадрах скролла (допустима однократная аллокация при Connect).
**Why human:** Editor Profiler нерепрезентативен (задокументировано в памяти пользователя).

---

## Gaps Summary

**2 BLOCKER gap-а блокируют финальное закрытие фазы:**

**GAP-1: TEST-04 — EditMode тесты не обнаруживаются в Unity Test Runner**

Несмотря на добавление `testables: ["com.shtl.mvvm"]` в manifest.json (план 05), EditMode тесты по-прежнему не показываются. Вероятная причина: asmdef `Shtl.Mvvm.Tests.Runtime.asmdef` имеет `includePlatforms: []`, что в Unity означает "все платформы", и Test Runner может не регистрировать такую сборку как EditMode. Plan 01-01 изначально специфицировал `includePlatforms: ["Editor"]` — это изменение было сделано без документации отклонения. Дополнительно: часть PlayMode тестов падает, что требует диагностики.

**GAP-2: VLIST-03 — Wheel-overscroll Elastic oscillation**

Пользователь верифицировал: после wheel-overscroll список пружинит на границе вместо плавной фиксации. Plan 06 исправил LateUpdate гейт (`_velocity == 0f && offset == 0f`) и velocity stop threshold (`< 1f && offset == 0f`), но не решил root cause: `SmoothDamp` возвращает epsilon-velocity (~1.4e-7f), которая не равна точно 0f и не попадает под порог 1f немедленно. Требуется: заменить точное `_velocity == 0f` на `Mathf.Abs(_velocity) < VelocityStopThreshold` в guard, а также зафиксировать WR-01 (stale `_prevDragPosition`).

---

_Verified: 2026-05-01T12:00:00Z_
_Verifier: Claude (gsd-verifier)_

---

## Resolution Update (2026-05-01T15:30:00Z)

Оба BLOCKER-гэпа из исходной верификации закрыты после её завершения. Анализ выше отражает состояние на 12:00; ниже — что изменилось и где зафиксировано.

### GAP-1 → RESOLVED — TEST-04 EditMode тесты

**Resolution path:** план 01-05 (testables в Sample manifest, commit `dd08dd1`) был отрефакторен в commit `bd47a93 refactor(tests): move tests to Tests/Editor and add dedicated TestProject`.

**Что сделано:**
- Тесты перенесены `Tests/Runtime/` → `Tests/Editor/`
- Asmdef переименован `Shtl.Mvvm.Tests.Runtime` → `Shtl.Mvvm.Tests.Editor`, `includePlatforms: ["Editor"]`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`
- Создан выделенный Unity-проект `TestProject~/` (тильда скрывает от Asset Database) для headless-прогона тестов пакета
- `testables` намеренно удалён из `Samples~/Sample/Packages/manifest.json` — Sample стал чистым demo-проектом без тестов фреймворка в его Test Runner UI
- `InternalsVisibleTo` обновлён в `Runtime/AssemblyInfo.cs`

**Verification:** headless run в TestProject~ — **68/68 passed** (зафиксировано в commit message `bd47a93`).

**Дополнительные регрессионные тесты:** `Tests/Editor/VirtualScrollRectWheelTests.cs` — covers VLIST-03 wheel rubber-band и judder-fix invariant (commits `8d4e264`, `1ef4d36`, `11b179c`).

### GAP-2 → RESOLVED — VLIST-03 Wheel-overscroll Elastic

**Resolution path:** серия фиксов VLIST-03, человеческая верификация в commit `cf502b0 docs(debug): resolve wheel-judder-rubber-still — human-verified`.

**Что сделано:**
- `aee6503`, `31ddee1` `fix(01): VLIST-03 wheel rubber-band — apply RubberDelta in Elastic mode`: OnScroll в Elastic-режиме применяет `RubberDelta` для over-bound части (line 281, 285), wheel-input даёт тот же визуальный rubber-band feel что и drag
- `7aa5312` `fix(01): VLIST-03 wheel judder — guard elastic SmoothDamp during active wheel input`: введён `_lastWheelTime` маркер активного wheel-ввода (lines 304-307), LateUpdate Elastic-ветка пропускает SmoothDamp pull-back пока `Time.unscaledTime - _lastWheelTime < WheelActiveDuration` (lines 347-351). Это устраняет judder, когда SmoothDamp каждый кадр тянет позицию обратно к границе, а следующий OnScroll re-rubber-сжимает overshoot
- velocity-stop threshold изменён на `Mathf.Abs(_velocity) < VelocityStopThreshold && offset == 0f` (line 333) — устраняет epsilon-velocity oscillation после SmoothDamp
- LateUpdate gate (lines 312-337) теперь корректно: `if (_isDragging) return;` → Unrestricted short-content guard → `if (Mathf.Abs(_velocity) < VelocityStopThreshold && offset == 0f) return;` → Elastic SmoothDamp с wheel-active guard

**Verification:** human-verify (commit `cf502b0`), регрессионные тесты в `VirtualScrollRectWheelTests.cs`.

### Обновление сводки

| Метрика | Было (12:00) | Стало (15:30) |
|---------|--------------|---------------|
| Status | gaps_found | verified |
| Score | 5/7 | 7/7 |
| BLOCKER gaps | 2 | 0 |
| Failed must-haves | TEST-04, VLIST-03 (PARTIAL) | — |

UAT-сессия (`01-UAT.md`) переведена в `status: complete`: 7/8 pass, 1 skipped (VLIST-07 GC.Alloc — отложен до Development Build).

---

_Updated: 2026-05-01T15:30:00Z — gaps closed, phase ready for transition_
