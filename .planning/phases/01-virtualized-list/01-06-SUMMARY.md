---
phase: 01-virtualized-list
plan: "06"
subsystem: VirtualScrollRect
tags: [gap-closure, elastic-bounce, scroll-lock, uat-defects]
gap_closure: true
gaps_addressed:
  - gap-2-elastic-no-return-to-bounds
  - gap-3-scroll-not-locked-when-content-smaller-than-viewport

dependency_graph:
  requires: [01-04]
  provides: [gap-2-fix, gap-3-fix]
  affects: [VirtualScrollRect.LateUpdate, VirtualScrollRect.OnBeginDrag, VirtualScrollRect.OnDrag, VirtualScrollRect.OnScroll]

tech_stack:
  added: []
  patterns:
    - "Guard pattern: ранний return при contentHeight <= ViewportSize"
    - "Двойное условие early-return: velocity==0 && offset==0 вместо одного velocity==0"

key_files:
  modified:
    - Runtime/Core/VirtualScroll/VirtualScrollRect.cs
    - .planning/phases/01-virtualized-list/01-UI-SPEC.md

decisions:
  - "velocity stop threshold обнуляет velocity только при offset==0, чтобы SmoothDamp завершил Elastic-возврат до конца"
  - "Guard в OnBeginDrag не устанавливает _isDragging=true, что делает OnDrag/OnEndDrag также безопасными при коротком контенте"

metrics:
  duration: "2 min"
  completed: "2026-05-01"
  tasks_completed: 2
  tasks_total: 3
  files_modified: 2
---

# Phase 01 Plan 06: Gap Closure — Elastic Bounce и блокировка скролла короткого списка

Исправлены два UAT-дефекта в `VirtualScrollRect`: Elastic bounce не запускался при velocity==0 (Gap 2), и скролл не блокировался когда контент помещается во viewport (Gap 3).

## Completed Tasks

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Исправить LateUpdate гейт и velocity stop threshold (Gap 2) | 0bd4f25 | VirtualScrollRect.cs |
| 2 | Guard для короткого списка в OnBeginDrag/OnDrag/OnScroll (Gap 3) + UI-SPEC | 0bd4f25 / 87124b3 | VirtualScrollRect.cs, 01-UI-SPEC.md |
| 3 | Visual verify в Unity Editor | — | (checkpoint, pending) |

## Changes Made

### Gap 2: Elastic bounce без velocity (LateUpdate)

**Проблема:** LateUpdate делал early-return при `_velocity == 0f` без учёта `CalculateOffset()`. Mouse wheel сдвигал `_scrollPosition` без установки velocity, и W-04 fix (SetContentSize обнулял velocity при out-of-range) создавал состояние out-of-range + velocity==0. В обоих случаях LateUpdate прерывался до запуска SmoothDamp.

**Изменение 1 — гейт LateUpdate (строка 232 → 240):**
```
// Было:
if (_isDragging || _velocity == 0f) return;

// Стало:
if (_isDragging) return;
var offset = CalculateOffset();
if (_velocity == 0f && offset == 0f) return;
```

**Изменение 2 — velocity stop threshold (строка 260 → 285):**
```
// Было:
if (Mathf.Abs(_velocity) < 1f) { _velocity = 0f; }

// Стало:
if (Mathf.Abs(_velocity) < 1f && offset == 0f) { _velocity = 0f; }
```

Логика: velocity обнуляется только когда позиция уже в допустимом диапазоне. Без этого SmoothDamp мог быть прерван до достижения границы.

### Gap 3: Блокировка скролла при contentHeight <= viewportHeight

**Проблема:** OnDrag и OnScroll безусловно изменяли `_scrollPosition`. При `contentHeight <= ViewportSize` maxScroll==0, но позиция временно уплывала, LateUpdate возвращал → пользователь видел rubber-band вместо полной блокировки.

**Guard в трёх методах:**
- `OnBeginDrag` (строка 178): не устанавливает `_isDragging = true` — drag не начинается
- `OnDrag` (строка 191): не изменяет `_scrollPosition`
- `OnScroll` (строка 223): не изменяет `_scrollPosition`

### UI-SPEC обновлён

Добавлен раздел "Короткий список (contentHeight ≤ viewportHeight)" после "Mouse Wheel" с таблицей поведения событий.

## Deviations from Plan

None — план выполнен точно как написан. Два изменения в LateUpdate объединены в один коммит (Task 1 + Task 2 — один файл), что является оптимальной атомарностью.

## Known Stubs

None.

## Threat Flags

None — изменения не вводят новые network endpoints, auth paths или trust boundaries.

## Self-Check: PASSED

- [x] `VirtualScrollRect.cs` модифицирован: `0bd4f25`
- [x] `01-UI-SPEC.md` модифицирован: `87124b3`
- [x] Grep: `_velocity == 0f && offset == 0f` — найден (строка 258)
- [x] Grep: `Mathf.Abs(_velocity) < 1f && offset == 0f` — найден (строка 285)
- [x] Grep: `_contentHeight <= ViewportSize` — найден 3 раза (178, 191, 223)
- [x] Grep: `Короткий список` в UI-SPEC — найден (строка 101)
- [x] Нет LINQ-аллокаций в hot path
- [x] Commits exist: `git log --oneline | grep 0bd4f25` и `87124b3`

## Pending

**Task 3 (checkpoint:human-verify):** Visual verify в Unity Editor — запускает orchestrator после возврата checkpoint.
