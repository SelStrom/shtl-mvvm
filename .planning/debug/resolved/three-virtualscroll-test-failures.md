---
status: resolved
trigger: "Headless test run: 65 passed / 3 failed / 68 total — three independent failures in LayoutCalculatorTests and VirtualCollectionBindingTests"
created: 2026-05-01
updated: 2026-05-01
---

## Current Focus

hypothesis: all three root causes confirmed and fixed
test: full headless EditMode run
expecting: 68/68 pass
next_action: commit atomically

## Symptoms

expected: all 68 EditMode tests pass
actual: 3 fail
errors: |
  F1: LayoutCalculatorTests.FindVisibleRange_WithOverscan_ExpandsRange — Expected 9, got 10
  F2: VirtualCollectionBindingTests.Dispose_DoesNotDoubleDisposeView — MissingReferenceException on view.get_name() after dispose
  F3: VirtualCollectionBindingTests.OnElementAdded_FixedHeight_PreservesFixedPath — Expected 100.0, got 0.0 (fixed-mode broken after Add)
reproduction: run headless EditMode tests via Unity batchmode
started: unknown — current branch state

## Eliminated

(none yet)

## Evidence

(populated during investigation)

## Resolution

root_cause: |
  F1 (LayoutCalculator.FindVisibleRange fixed-path): отсутствовала boundary-correction для случая,
  когда элемент начинается ровно на нижней границе viewport (offset == endPos). Variable-path делал
  это через `lastBoundary - 1` при `prefixHeights[lastBoundary] >= endPos`, fixed-path -- нет.
  Тест scrollPos=500, viewport=300, fixed=100, count=20, overscan=2 ожидал last=9 (visible 5..7,
  +overscan=2), но получал last=10 (visible 5..8 — element 8 ошибочно включён).

  F2 (VirtualCollectionBinding.OnElementAdded/OnElementRemoved): порядок операций приводил к
  double-dispose при последовательном Add. SetContentSize() триггерит callback → UpdateVisibleRange,
  но к этому моменту _activeViews ещё содержит СТАРЫЕ ключи (до ShiftActiveViewIndices, который
  вызывался ПОСЛЕ SetContentSize). При Add(idx=1): UpdateVisibleRange #1 создаёт viewB по новому
  индексу 1; затем ShiftActiveViewIndices(1,+1) сдвигает viewB → 2; UpdateVisibleRange #2 видит
  viewB на out-of-range индексе 2 → Release(viewB) → OnDisposed#1; затем выдаёт его обратно из
  пула для idx=1. На binding.Dispose: OnDisposed#2.

  F3 (VirtualCollectionBinding.OnElementAdded): _isFixedLayout инициализировалось ТОЛЬКО внутри
  RebuildLayout (вызывается из OnContentChanged). При построении списка через последовательные
  Add'ы с пустого старта OnContentChanged не срабатывает (Items.Connect не дёргает callback при
  _list==null), и _isFixedLayout оставался false навсегда → InsertAt-ветка → _layoutCalculator.
  _fixedHeight=0 (variable mode) даже при одинаковой высоте всех элементов.

fix: |
  F1: в LayoutCalculator.FindVisibleRange fixed-path добавлена коррекция: если
  `lastVisible * stride >= endPos`, то lastVisible-- (симметрично binary-search ветке).

  F2: в OnElementAdded и OnElementRemoved — ShiftActiveViewIndices перенесён ДО SetContentSize
  (и до возможной модификации ScrollPosition), чтобы UpdateVisibleRange, который триггерится
  через OnScrollPositionChanged callback, видел уже корректные ключи _activeViews.

  F3: в OnElementAdded при count==1 (первый элемент в пустой список) явно
  устанавливается _isFixedLayout=true, _fixedItemHeight=newHeight. Дальше существующая
  логика сама проверяет совпадение высот и сохраняет fixed-mode при последующих Add'ах.

  Бонус: исправлено диагностическое сообщение в Dispose_DoesNotDoubleDisposeView — оно
  обращалось к view.name на уже DestroyImmediate'нутом GameObject, что маскировало реальную
  причину (count != 1) MissingReferenceException'ом.

verification: 68/68 EditMode tests pass (final XML /tmp/shtl-test-results-final.xml)
files_changed:
  - Runtime/Core/VirtualScroll/LayoutCalculator.cs
  - Runtime/Core/Bindings/VirtualCollectionBinding.cs
  - Tests/Runtime/VirtualCollectionBindingTests.cs
