---
phase: 01-virtualized-list
plan: "05"
subsystem: testing
gap_closure: true
gaps_addressed:
  - gap-1-tests-not-discovered
tags: [testing, upm, manifest, test-runner]
requirements: [TEST-04]

dependency_graph:
  requires: []
  provides:
    - "testables opt-in для com.shtl.mvvm в Sample manifest"
    - "UNITY_INCLUDE_TESTS defineConstraint активируется"
  affects:
    - "Samples~/Sample/Packages/manifest.json"
    - "Tests/Runtime/Shtl.Mvvm.Tests.Runtime.asmdef"

tech_stack:
  added: []
  patterns:
    - "UPM testables field для opt-in дискавери тестов пакета"

key_files:
  created: []
  modified:
    - "Samples~/Sample/Packages/manifest.json"

decisions:
  - "Добавить testables в manifest.json — стандартный UPM-механизм для дискавери тестов зависимого пакета"

metrics:
  duration: "<1 min"
  completed: "2026-05-01"
  tasks_completed: 1
  tasks_total: 2
  files_changed: 1
---

# Phase 1 Plan 05: Add testables opt-in for Test Runner — Summary

## One-liner

Одна строка `"testables": ["com.shtl.mvvm"]` в manifest.json Sample-проекта активирует UNITY_INCLUDE_TESTS и делает 49 тестов фазы 1 видимыми в Test Runner.

## What Was Done

### Task 1: Добавить testables в manifest.json Sample-проекта

Добавлено поле `testables` на верхний уровень `Samples~/Sample/Packages/manifest.json` после блока `dependencies`. Поле содержит массив с именем пакета `com.shtl.mvvm`.

**Commit:** `dd08dd1` — `chore(01-05): add testables opt-in for com.shtl.mvvm in Sample manifest`

**Механизм:** При наличии `testables` в manifest.json Unity Package Manager активирует `UNITY_INCLUDE_TESTS` define constraint, что позволяет `defineConstraints` в `Tests/Runtime/Shtl.Mvvm.Tests.Runtime.asmdef` включить тестовую сборку. Без этого поля Test Runner не видит тесты пакета, подключённого через `file:`-ссылку.

## Checkpoint Reached

**Task 2** — `checkpoint:human-verify` (blocking gate). Требует ручной верификации в Unity Editor:

1. Переключить фокус в Unity Editor (domain reload)
2. Открыть Window -> General -> Test Runner -> EditMode
3. Убедиться что видны 4 тест-сьюта (ReactiveVirtualListTests, LayoutCalculatorTests, ViewRecyclingPoolTests, VirtualCollectionBindingTests)
4. Нажать Run All — все 49 тестов должны быть зелёными

## Deviations from Plan

None — план выполнен точно как написан.

## Known Stubs

None.

## Threat Flags

None. Изменение только в manifest.json (данные от разработчика, трекируются git).

## Self-Check

- [x] `Samples~/Sample/Packages/manifest.json` содержит `testables` (grep -c вернул 1)
- [x] Commit `dd08dd1` существует
- [x] Нет случайных удалений файлов

## Self-Check: PASSED
