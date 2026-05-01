---
phase: quick
plan: 260421-t2q
subsystem: virtual-scroll
tags: [api-design, encapsulation, internal]
dependency_graph:
  requires: []
  provides:
    - "VirtualScrollRect internal API"
    - "InternalsVisibleTo for test assemblies"
  affects:
    - "Runtime/Core/VirtualScroll/VirtualScrollRect.cs"
    - "Runtime/AssemblyInfo.cs"
tech_stack:
  added: []
  patterns:
    - "InternalsVisibleTo for cross-assembly test access"
key_files:
  created:
    - Runtime/AssemblyInfo.cs
  modified:
    - Runtime/Core/VirtualScroll/VirtualScrollRect.cs
decisions:
  - "MovementType enum made internal (used only within same assembly)"
  - "InternalsVisibleTo for both Shtl.Mvvm.Tests and Shtl.Mvvm.Tests.Runtime"
metrics:
  duration: "2m 12s"
  completed: "2026-04-21"
  tasks_completed: 2
  tasks_total: 2
---

# Quick Task 260421-t2q: VirtualScrollRect API Encapsulation Summary

Скрытие публичного API VirtualScrollRect за internal-модификатором с сохранением доступа для тестов через InternalsVisibleTo.

## Completed Tasks

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1 | AssemblyInfo.cs с InternalsVisibleTo | 7a7eba4 | Runtime/AssemblyInfo.cs |
| 2 | Модификаторы доступа в VirtualScrollRect | f9ca93d | Runtime/Core/VirtualScroll/VirtualScrollRect.cs |

## Changes Made

### Task 1: AssemblyInfo.cs

Создан `Runtime/AssemblyInfo.cs` с `InternalsVisibleTo` для обеих тестовых сборок:
- `Shtl.Mvvm.Tests` (Editor-тесты)
- `Shtl.Mvvm.Tests.Runtime` (Runtime-тесты)

### Task 2: VirtualScrollRect.cs

10 членов изменены с `public` на `internal`:
- Enum `MovementType`
- Свойства: `ScrollPosition`, `Velocity`, `OverscanCount`, `ViewportHeight`
- Методы: `SetContentHeight`, `ScrollTo`, `ScrollToIndex`, `SetOnScrollPositionChanged`, `ResetScroll`

5 членов остались `public`:
- `class VirtualScrollRect` (MonoBehaviour, ссылается из сцен)
- `OnBeginDrag` (IBeginDragHandler)
- `OnDrag` (IDragHandler)
- `OnEndDrag` (IEndDragHandler)
- `OnScroll` (IScrollHandler)

## Verification Results

1. AssemblyInfo.cs существует с корректными InternalsVisibleTo -- PASS
2. Ровно 5 public членов в VirtualScrollRect.cs -- PASS
3. VirtualCollectionBinding.cs не изменён -- PASS
4. VirtualListBindExtensions.cs не изменён -- PASS
5. VirtualCollectionBindingTests.cs не изменён -- PASS

## Deviations from Plan

None -- план выполнен точно как написано.

## Known Stubs

None.
