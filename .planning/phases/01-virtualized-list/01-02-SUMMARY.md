---
phase: 01-virtualized-list
plan: 02
subsystem: VirtualScroll
tags: [virtual-scroll, recycling-pool, ui-component, tdd]
dependency_graph:
  requires: []
  provides: [VirtualScrollRect, ViewRecyclingPool]
  affects: [VirtualCollectionBinding]
tech_stack:
  added: []
  patterns: [Stack-based object pool, Unity EventSystem handlers, SmoothDamp inertia]
key_files:
  created:
    - Runtime/Core/VirtualScroll/VirtualScrollRect.cs
    - Runtime/Core/VirtualScroll/ViewRecyclingPool.cs
    - Tests/Runtime/ViewRecyclingPoolTests.cs
    - Tests/Runtime/Shtl.Mvvm.Tests.Runtime.asmdef
  modified: []
decisions:
  - MovementType enum вынесен на уровень namespace (не вложенный в VirtualScrollRect) для удобства доступа
  - _updatingScrollbar guard flag для предотвращения бесконечного цикла scrollbar sync (T-02-02)
  - ViewRecyclingPool.Get() не вызывает Connect -- ответственность вызывающего кода
metrics:
  duration: 2m
  completed: "2026-04-16T23:11:33Z"
  tasks_completed: 2
  tasks_total: 2
  files_created: 4
  files_modified: 0
---

# Phase 01 Plan 02: VirtualScrollRect и ViewRecyclingPool Summary

Stack-based пул переиспользования View-элементов и MonoBehaviour кастомного скролла с инерцией, elastic bounce и bidirectional scrollbar sync.

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1 | ViewRecyclingPool (TDD) | 37a2756 (RED), 11f1831 (GREEN) | ViewRecyclingPool.cs, ViewRecyclingPoolTests.cs |
| 2 | VirtualScrollRect | 7d90c47 | VirtualScrollRect.cs |

## Implementation Details

### ViewRecyclingPool (Task 1)

Реализован `internal class ViewRecyclingPool<TViewModel, TWidgetView>` со Stack-based хранилищем. Два конструктора: через `IWidgetViewFactory` (основной путь) и через prefab+Transform (fallback аналогично `ElementCollectionBinding`).

- `Get()` -- возвращает View из пула (SetActive(true)) или создает через factory/Instantiate
- `Release()` -- вызывает Dispose(), деактивирует GameObject, помещает в пул
- `DisposeAll()` -- уничтожает все View через factory.RemoveWidget или Object.Destroy
- `AggressiveInlining` на Get() и Release() для минимизации overhead в hot path

7 EditMode-тестов покрывают: создание через factory, переиспользование без factory, деактивацию/активацию GameObject, LIFO порядок, Count property, DisposeAll cleanup.

### VirtualScrollRect (Task 2)

Реализован `public class VirtualScrollRect : MonoBehaviour` с 4 интерфейсами EventSystem (IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler).

Параметры из UI-SPEC: decelerationRate=0.135f, elasticity=0.1f, scrollSensitivity=35f, MovementType.Elastic, overscanCount=2.

- Инерция через `Mathf.Pow(decelerationRate, deltaTime)` (формула Unity ScrollRect)
- Elastic bounce через `Mathf.SmoothDamp` к target-позиции
- RubberDelta для перетягивания за границы (`(1 - 1/(abs*0.55/viewSize + 1)) * viewSize * sign`)
- Bidirectional scrollbar sync с guard flag `_updatingScrollbar` (mitigated T-02-02)
- Zero allocations в LateUpdate, OnDrag, OnScroll
- Public API: ScrollTo, ScrollToIndex, SetContentHeight, SetOnScrollPositionChanged, ResetScroll

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Test infrastructure setup**
- **Found during:** Task 1
- **Issue:** Tests/Runtime/ directory did not exist (only .meta file), no asmdef for runtime tests
- **Fix:** Created Shtl.Mvvm.Tests.Runtime.asmdef with proper references to Shtl.Mvvm, TestRunner, nunit
- **Files created:** Tests/Runtime/Shtl.Mvvm.Tests.Runtime.asmdef

**2. [Rule 3 - Blocking] Branch base mismatch**
- **Found during:** Init
- **Issue:** Worktree branch was based on old commit (5802404) instead of feature branch HEAD (2f9f71e)
- **Fix:** git reset --soft to correct base
- **Commits:** 178b191 (CLAUDE.md restore after reset)

## Threat Mitigation

| Threat ID | Status | Implementation |
|-----------|--------|----------------|
| T-02-01 (DoS via LateUpdate) | Mitigated | LateUpdate early-returns when `_isDragging` or `_velocity == 0f`; velocity zeroed at `abs < 1f` |
| T-02-02 (Scrollbar infinite loop) | Mitigated | `_updatingScrollbar` guard flag prevents re-entrant scrollbar value changes |

## Self-Check: PASSED

All 4 created files exist. All 3 task commits verified in git log.
