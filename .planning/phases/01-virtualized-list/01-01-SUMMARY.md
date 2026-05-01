---
phase: 01-virtualized-list
plan: 01
subsystem: core-data-types
tags: [reactive, virtual-list, layout, tdd, zero-alloc]
dependency_graph:
  requires: []
  provides: [ReactiveVirtualList, LayoutCalculator, VisibleRange, test-infrastructure]
  affects: [01-02, 01-03, 01-04]
tech_stack:
  added: [NUnit test framework via asmdef]
  patterns: [prefix-sum, binary-search, struct-based-zero-alloc, tdd-red-green]
key_files:
  created:
    - Tests/Runtime/Shtl.Mvvm.Tests.asmdef
    - Runtime/Core/Types/ReactiveVirtualList.cs
    - Runtime/Core/VirtualScroll/LayoutCalculator.cs
    - Tests/Runtime/ReactiveVirtualListTests.cs
    - Tests/Runtime/LayoutCalculatorTests.cs
  modified: []
decisions:
  - "ReactiveList.Dispose() -- explicit interface implementation, requires cast to IReactiveValue"
  - "LayoutCalculator as internal struct for zero-alloc and encapsulation"
  - "VisibleRange as internal readonly struct"
  - "EnsureCapacity with 2x growth strategy and minimum 16 elements"
metrics:
  duration_seconds: 182
  completed: "2026-04-15T23:12:10Z"
  tasks_completed: 2
  tasks_total: 2
  tests_written: 33
  files_created: 5
---

# Phase 01 Plan 01: Core Data Types (ReactiveVirtualList + LayoutCalculator) Summary

ReactiveVirtualList<T> -- ViewModel-тип виртуализированного списка с реактивными полями (Items, ScrollPosition, FirstVisibleIndex, VisibleCount) и поддержкой фиксированной/переменной высоты элементов. LayoutCalculator -- struct с prefix sum массивом и binary search для O(log n) определения видимого диапазона, O(1) fast path для фиксированной высоты, zero-alloc в FindVisibleRange.

## Task Results

### Task 1: ReactiveVirtualList<T> (TDD)

| Step | Commit | Description |
|------|--------|-------------|
| RED | 5cf67b9 | 13 failing tests: fixed/variable height, CRUD, Dispose/Unbind, initial values |
| GREEN | 786c56b | ReactiveVirtualList<T> implementation with full API |

**Key implementation details:**
- `ReactiveVirtualList<TElement> : IReactiveValue where TElement : AbstractViewModel, new()`
- 4 public readonly reactive fields: Items (ReactiveList), ScrollPosition, FirstVisibleIndex, VisibleCount
- 3 constructors: fixed height (float), variable height (Func<int,float>), default
- GetItemHeight с AggressiveInlining и валидацией non-positive значений (T-01-01 mitigation)
- Proxy-методы к Items: Add, RemoveAt, Clear, Count, indexer
- Dispose/Unbind делегируют ко всем внутренним полям
- Items.Dispose() через каст к IReactiveValue (explicit interface implementation в ReactiveList)

### Task 2: LayoutCalculator (TDD)

| Step | Commit | Description |
|------|--------|-------------|
| RED | 4d6b930 | 19 failing tests: prefix sum, binary search, insert/remove, edge cases |
| GREEN | ee642b1 | LayoutCalculator struct with prefix sum + binary search |

**Key implementation details:**
- `internal struct LayoutCalculator` -- zero-alloc, internal
- Prefix sum array `_prefixHeights[i]` = сумма высот 0..i-1
- Binary search: `lo + (hi - lo) / 2` для определения первого видимого элемента
- O(1) fast path для фиксированной высоты: `index * _fixedHeight`
- FindVisibleRange с overscan и boundary clamping
- InsertAt/RemoveAt с инкрементальным пересчётом prefix sum
- EnsureCapacity с 2x growth strategy (min 16)
- `internal readonly struct VisibleRange` с FirstIndex, LastIndex, Count
- AggressiveInlining на GetItemOffset, GetItemHeight, TotalHeight

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] ReactiveList explicit Dispose**
- **Found during:** Task 1 GREEN
- **Issue:** ReactiveList реализует IReactiveValue.Dispose() как explicit interface implementation, прямой вызов `Items.Dispose()` не компилируется
- **Fix:** Cast к IReactiveValue: `((IReactiveValue)Items).Dispose()`
- **Files modified:** Runtime/Core/Types/ReactiveVirtualList.cs
- **Commit:** 786c56b

## Verification

- ReactiveVirtualListTests: 13 test methods (>= 10 required)
- LayoutCalculatorTests: 19 test methods (>= 10 required)
- Total: 32 test methods (>= 20 required)
- IReactiveValue implemented on ReactiveVirtualList
- AggressiveInlining present on hot path methods
- Binary search pattern confirmed
- No MonoBehaviour dependencies -- pure logic
- No LINQ, no boxing, no new in FindVisibleRange -- zero-alloc confirmed by code review

## Known Stubs

None -- all implementations are complete and functional.

## Self-Check: PASSED

All 5 created files verified on disk. All 4 commits verified in git log.
