---
status: complete
phase: 01-virtualized-list
source:
  - .planning/phases/01-virtualized-list/01-01-SUMMARY.md
  - .planning/phases/01-virtualized-list/01-02-SUMMARY.md
  - .planning/phases/01-virtualized-list/01-03-SUMMARY.md
  - .planning/phases/01-virtualized-list/01-04-PLAN.md
  - .planning/phases/01-virtualized-list/01-05-SUMMARY.md
  - .planning/phases/01-virtualized-list/01-06-SUMMARY.md
started: 2026-04-30T12:00:00Z
updated: 2026-05-01T15:30:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Файлы артефактов фазы созданы в правильных директориях
expected: 11 файлов на местах согласно Plan 04 (Runtime/Core/Types, Runtime/Core/VirtualScroll, Runtime/Core/Bindings, Runtime/Utils, Tests/Runtime).
result: pass
note: auto-verified -- ls показал все 11 файлов на диске

### 2. Zero-alloc grep в hot path (Runtime/Core/VirtualScroll/, VirtualCollectionBinding.cs)
expected: Нет LINQ-вызовов (.Select/.Where/.ToList/.ToArray/.OrderBy/.GroupBy) и нет неожиданных `new` (помимо предаллоцированных буферов).
result: pass
note: auto-verified -- grep не нашёл нарушений

### 3. Unity Editor: проект компилируется без ошибок
expected: Открыть Samples~/Sample в Unity Editor (2020.3+). После domain reload в Console нет ошибок компиляции; индикатор ошибок внизу Editor отсутствует.
result: pass

### 4. Unity Test Runner: все EditMode-тесты зелёные
expected: Window → General → Test Runner → EditMode → Run All. Тесты ReactiveVirtualListTests (13), LayoutCalculatorTests (19), ViewRecyclingPoolTests (7), VirtualCollectionBindingTests (10) — все ✓. Никаких красных или жёлтых.
result: pass
note: |
  Изначально reported "тесты не проинициализированы в проекте sample" (severity: major).
  Resolved through Plan 06 (testables) → refactored в commit bd47a93: тесты перенесены в
  Tests/Editor/ (includePlatforms=Editor, defineConstraints=UNITY_INCLUDE_TESTS), создан
  выделенный TestProject~/ для headless-прогона. Sample-проект очищен от testables —
  стал чистым demo. Headless run: 68/68 passed (зафиксировано в commit message bd47a93).
  Дополнительно: VirtualScrollRectWheelTests добавлен (commits 8d4e264, 1ef4d36, 11b179c).

### 5. Sample-сцена VirtualListEntryScreen: плавный скролл с инерцией
expected: |
  Открыть сцену VirtualListEntryScreen (или открывающую её через VirtualListSampleWidget).
  Запустить Play Mode. Список рендерит видимые элементы (~видимый_размер_viewport / 80px + overscan 2 сверху/снизу).
  Drag вверх/вниз — прокрутка следует за указателем.
  Отпустить drag — список продолжает движение по инерции и плавно затухает (decelerationRate=0.135).
  При перетягивании за границы — резиновая натяжка (rubber band), отпускание возвращает в допустимый диапазон.
  Mouse wheel — мгновенный сдвиг без инерции.
result: pass
note: |
  Изначально reported два дефекта (severity: major):
    1) скролл в крайних позициях не возвращает viewport в крайнюю позицию (drag/wheel/touchpad)
    2) скролл не блокируется когда количество элементов меньше viewport
  Resolved through Plan 06 (commits 0bd4f25, 87124b3) + последующие итерации VLIST-03:
    - LateUpdate gate: VirtualScrollRect.cs:312-337 — Elastic SmoothDamp запускается при
      offset != 0f даже если velocity == 0f, остановка только при offset == 0f
      (VelocityStopThreshold по line 333)
    - OnScroll wheel rubber-band: VirtualScrollRect.cs:276-301 — Elastic применяет RubberDelta
      для over-bound части, judder-fix invariant (velocity безусловно зануляется),
      _lastWheelTime guard для активного wheel-input (commits 31ddee1, aee6503, 7aa5312)
    - Short-content guards: VirtualScrollRect.cs:204, 215, 247, 323 — _contentHeight <= ViewportSize
      даёт ранний return в OnBeginDrag/OnDrag/OnScroll и в LateUpdate для Unrestricted
  Покрытие регрессии: VirtualScrollRectWheelTests (commits 8d4e264, 1ef4d36, 11b179c).
  Финальная human-verify: commit cf502b0 docs(debug): resolve wheel-judder-rubber-still.

### 6. Sample-сцена: динамические Add/Remove не ломают позицию (B-01, B-02 регрессия)
expected: |
  В Play Mode прокрутить список к середине.
  Через UI/Inspector добавить/удалить элементы выше viewport (если есть кнопка) либо вызвать Add/RemoveAt из дебаг-команды.
  Визуальная позиция элементов в viewport сохраняется (нет рывка), нет дублирования, нет пропавших элементов.
  На границе viewport (когда край элемента ровно совпадает с краем экрана) лишних элементов не отрисовано.
result: pass
note: |
  Изначально дублировал дефекты test 5 в сценарии Add/Remove. После Plan 06
  + VLIST-03 wheel-fix корневые причины устранены, что покрывает Add/Remove-сценарии.
  Регрессия Add/Remove дополнительно покрыта VirtualCollectionBindingTests (10 тестов)
  и боковыми фиксами 187fd48 (preserve fixed-mode в OnElementAdded, double-dispose ordering),
  3667686 (boundary handling в LayoutCalculator fixed-path FindVisibleRange).

### 7. Sample-сцена: Scrollbar синхронизирован с позицией (W-03, W-07 регрессия)
expected: |
  Если в сцене есть Scrollbar — его thumb движется вместе со скроллом, размер пропорционален viewportHeight / contentHeight.
  Перетаскивание thumb меняет позицию списка (bidirectional sync).
  При перетягивании за границу (Elastic) thumb не уходит за [0, 1] — остаётся прижатым к краю.
  Отключение/включение Scrollbar GameObject (OnEnable) — после включения thumb сразу показывает корректную позицию, а не нулевую.
result: pass

### 8. (VLIST-07) Unity Profiler: GC.Alloc = 0 при прокрутке
expected: |
  Window → Analysis → Profiler. В Play Mode записать сэмпл при активном скролле.
  В колонке GC.Alloc для кадров скролла — 0 байт (или close to 0, не растёт от кадра к кадру).
  Допустимо: однократная аллокация при первом Connect (захват делегатов).
  Per Plan 04: если sample-сцена недостаточна для воспроизведения — отметить как skipped, перенесено в фазу 2.
result: skipped
reason: "User reported: плохой тест, в редакторе нерепрезентативные данные по аллокациям. Замер откладывается до Development Build / автоматической PlayMode-проверки через GC.GetTotalAllocatedBytes; покрытие zero-alloc обеспечено auto-grep'ом из test 2 + code review (Plan 04 task 1)."

## Summary

total: 8
passed: 7
issues: 0
pending: 0
skipped: 1

## Gaps

- truth: "Все EditMode-тесты фазы (ReactiveVirtualListTests, LayoutCalculatorTests, ViewRecyclingPoolTests, VirtualCollectionBindingTests) запускаются и проходят зелёными в Unity Test Runner Sample-проекта"
  status: resolved
  reason: "User reported: тесты не проинициализированы в проекте sample"
  severity: major
  test: 4
  root_cause: "Samples~/Sample/Packages/manifest.json не содержит поле 'testables: [\"com.shtl.mvvm\"]'. Unity Test Runner не дискаверит тесты из UPM-пакета, подключённого через file:-ссылку, без явного opt-in через testables."
  resolution: "Plan 06 (testables) был отрефакторен в commit bd47a93: тесты перенесены из Tests/Runtime в Tests/Editor (Editor-only includePlatforms), создан выделенный TestProject~/ для headless-прогона. testables из Sample manifest удалён намеренно — Sample стал чистым demo. Headless run: 68/68 passed."
  artifacts:
    - path: "Tests/Editor/Shtl.Mvvm.Tests.Editor.asmdef"
      issue: "includePlatforms=[\"Editor\"], defineConstraints=[\"UNITY_INCLUDE_TESTS\"] — корректно"
    - path: "TestProject~/"
      issue: "выделенный Unity-проект для запуска тестов пакета"
  debug_session: .planning/debug/tests-not-discovered-in-sample.md

- truth: "При перетягивании drag / прокрутке колесом / тачпадом за границы (Elastic mode) после отпускания viewport SmoothDamp-возвращается в допустимый диапазон [0, maxScroll] и фиксируется в крайней позиции"
  status: resolved
  reason: "User reported: при попытке драга или скролла колесиком мыши/двумя пальцами на тачпаде скролл в крайних положениях не возвращает вьюпорт в крайнюю позицию"
  severity: major
  test: 5
  root_cause: "VirtualScrollRect.LateUpdate делал early-return при '_isDragging || _velocity == 0f' без учёта CalculateOffset(). Mouse wheel сдвигал _scrollPosition без выставления velocity → out-of-range + velocity==0 → следующий LateUpdate выходил, Elastic-возврат не запускался. Аналогично порог остановки velocity обнулял _velocity при <1f без проверки offset, прерывая Elastic-цикл до достижения границы."
  resolution: "Plan 06 commit 0bd4f25 переписал LateUpdate gate (Elastic SmoothDamp запускается при offset != 0f даже если velocity == 0f) и velocity stop threshold (обнуление только при offset == 0f). Дополнительно VLIST-03: OnScroll применяет RubberDelta в Elastic для wheel-rubber-band feel (commits 31ddee1, aee6503), wheel-judder fix через _lastWheelTime guard в LateUpdate (commit 7aa5312), регрессионные тесты VirtualScrollRectWheelTests (commits 8d4e264, 1ef4d36, 11b179c). Final human-verify: commit cf502b0."
  artifacts:
    - path: "Runtime/Core/VirtualScroll/VirtualScrollRect.cs"
      issue: "LateUpdate gate (line 312-337), OnScroll RubberDelta+velocity-reset (line 276-307)"
  debug_session: .planning/debug/elastic-no-return-to-bounds.md, .planning/debug/wheel-judder-at-bounds.md, .planning/debug/wheel-judder-at-bounds-2.md

- truth: "Если суммарная высота элементов меньше высоты viewport (contentHeight < viewportHeight, maxScroll == 0), drag и mouse wheel не должны менять _scrollPosition (либо позиция всегда clamp в 0)"
  status: resolved
  reason: "User reported: Если количество элементов в списке меньше вьюпорта то скролл должен быть заблокирован"
  severity: major
  test: 5
  root_cause: "OnDrag и OnScroll безусловно модифицировали _scrollPosition, не проверяя _contentHeight <= ViewportSize. В Elastic режиме ClampScrollPosition — no-op, поэтому позиция сдвигалась, а LateUpdate SmoothDamp возвращал её в 0 → видимый rubber-band вместо требуемой UI-SPEC контрактом полной блокировки взаимодействия."
  resolution: "Plan 06 commit 0bd4f25 добавил guard '_contentHeight <= ViewportSize' с ранним return в OnBeginDrag (line 204), OnDrag (line 215), OnScroll (line 247) + специализированный guard для Unrestricted в LateUpdate (line 323). UI-SPEC обновлён разделом 'Короткий список' (commit 87124b3)."
  artifacts:
    - path: "Runtime/Core/VirtualScroll/VirtualScrollRect.cs"
      issue: "Guards добавлены в OnBeginDrag/OnDrag/OnScroll/LateUpdate"
    - path: ".planning/phases/01-virtualized-list/01-UI-SPEC.md"
      issue: "Раздел 'Короткий список (contentHeight ≤ viewportHeight)' добавлен"
  debug_session: .planning/debug/scroll-not-locked-when-content-smaller-than-viewport.md
