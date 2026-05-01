---
phase: 01-virtualized-list
fixed_at: 2026-05-01T00:00:00Z
review_path: .planning/phases/01-virtualized-list/01-REVIEW.md
iteration: 1
findings_in_scope: 4
fixed: 4
skipped: 0
status: all_fixed
---

# Phase 01: Code Review Fix Report (Wave 5 — gap-2 / gap-3 follow-up)

**Fixed at:** 2026-05-01
**Source review:** `.planning/phases/01-virtualized-list/01-REVIEW.md`
**Iteration:** 1

**Summary:**
- Findings in scope: 4 (Critical 0 + Warning 4)
- Fixed: 4
- Skipped: 0
- Out of scope: 1 (IN-01 — info-уровень, scope = `critical_warning`)

Все 4 предупреждения из 5-й волны ревью (REVIEW.md от 2026-05-01) исправлены и закоммичены атомарно. Каждый коммит ссылается на ID находки в первой строке. Все правки в одном файле: `Runtime/Core/VirtualScroll/VirtualScrollRect.cs`. Tier-1 верификация (re-read изменённых блоков) выполнена; Tier-2 заменён brace/paren balance check (57/57 фигурных, 96/96 круглых — структура цела).

> **Примечание о хронологии:** этот файл перезаписан поверх раунда #2 от 2026-04-30 (round-2 фикс был для другого набора находок: B-01..B-04 + W-01..W-07, заверший gap-1..gap-3 wave 4). История раунда #2 сохранена в git-коммитах `daf859a..45c7a71` и REVIEW.md от 2026-04-30.

## Fixed Issues

### WR-01: `_isDragging` может застрять в `false` при drag + динамическом изменении контента

**Files modified:** `Runtime/Core/VirtualScroll/VirtualScrollRect.cs`
**Commit:** `d6a154d`
**Applied fix:** Перенёс присваивания `_isDragging = true` и `_prevDragPosition = GetLocalPosition(eventData)` ДО guard'а `_contentHeight <= ViewportSize` в `OnBeginDrag`. Теперь пара Begin/End симметрична: `OnEndDrag` всегда обнуляет `_isDragging`, а `_prevDragPosition` всегда инициализируется свежим значением — исключается stale-данные из предыдущей drag-сессии при динамическом увеличении контента сверх viewport во время drag. `_velocity = 0f` оставлен ПОСЛЕ guard'а, т.к. для случая `_contentHeight <= ViewportSize` обнулять velocity не имеет смысла (велоциты в этом режиме и не возникает).

### WR-03: Velocity stop threshold `1f` (px/s) не масштабируется с viewport

**Files modified:** `Runtime/Core/VirtualScroll/VirtualScrollRect.cs`
**Commit:** `60ae6d2`
**Applied fix:** Магическое число `1f` для порога остановки инерции вынесено в именованную константу класса:
```csharp
// Порог остановки инерции: ниже 1 px/s движение визуально незаметно.
// При необходимости масштабировать относительно ViewportSize.
private const float VelocityStopThreshold = 1f;
```
Использование на старой строке (`Mathf.Abs(_velocity) < 1f && offset == 0f`) заменено на `< VelocityStopThreshold`. Применено первым из пары WR-02/WR-03, т.к. WR-02 опирается на эту константу. Численное значение не менялось (1 px/s) — только семантика.

### WR-02: Точное float-сравнение `_velocity == 0f` ненадёжно

**Files modified:** `Runtime/Core/VirtualScroll/VirtualScrollRect.cs`
**Commit:** `3cc68ba`
**Applied fix:** Объединил early-return guard в `LateUpdate` с пост-step threshold-проверкой. Старая последовательность из двух условий (раннее `if (_velocity == 0f && offset == 0f) return;` + хвостовое `if (Mathf.Abs(_velocity) < 1f && offset == 0f) _velocity = 0f;`) заменена единым guard'ом в начале:
```csharp
if (Mathf.Abs(_velocity) < VelocityStopThreshold && offset == 0f)
{
    _velocity = 0f;
    return;
}
```
Это устраняет лишние вызовы `OnScrollPositionChanged` для under-threshold velocity, которые могут оставаться после `Mathf.SmoothDamp` (численный интегратор не гарантирует точного `0f`), и линеаризует управление потоком в `LateUpdate`. Хвостовая проверка удалена — её эффект теперь покрыт guard'ом следующего кадра.

### WR-04: `LateUpdate` не блокируется при `_contentHeight <= ViewportSize`, но drag/wheel — блокируются

**Files modified:** `Runtime/Core/VirtualScroll/VirtualScrollRect.cs`
**Commit:** `b1e39fa`
**Applied fix:** Добавил guard в начало `LateUpdate` (после проверки `_isDragging`) для случая `_movementType == MovementType.Unrestricted && _contentHeight <= ViewportSize`: обнуляет `_velocity` и выходит. Без этой проверки остаточная velocity, накопленная drag'ом до уменьшения контента ниже ViewportSize, продолжала бы бесконечно сдвигать `_scrollPosition` для Unrestricted-режима (`ClampScrollPosition` для Unrestricted не клампит, drag заблокирован guard'ом из gap-3). Для Elastic guard не нужен — там `CalculateOffset` корректно тянет позицию обратно к 0, а `LateUpdate` корректно отрабатывает Elastic-возврат.

## Verification

- **Tier 1 (re-read):** все 4 фикса верифицированы повторным чтением соответствующих участков файла после Edit — фикс присутствует, окружение цело, отступы (4 пробела) и Allman-стиль соблюдены.
- **Tier 2 (syntax check):** для C# в окружении нет дешёвого syntax-only чекера без зависимостей от Unity-сборок и `.csproj`. Выполнена эквивалентная проверка балансировки `{}/()` через python: фигурные скобки сбалансированы (57/57), круглые — (96/96). Это исключает грубое повреждение структуры от Edit-операций. Семантическая корректность (компиляция) делегируется фазе verifier.
- **Tier 3 (fallback):** Tier 1 + brace-balance принят как достаточная верификация для guard-условий.
- **Logic-bug findings:** все 4 правки — guard-условия (WR-01 симметрия assignments, WR-04 ранний return для Unrestricted, WR-02 unified threshold guard, WR-03 константа без изменения значения), не алгоритмические преобразования. Риск семантической ошибки минимален и легко увидим в diff'ах.
- **Соблюдение проектных конвенций (CLAUDE.md):** Allman-скобки, 4-space indent, фигурные скобки даже для однострочников, комментарии на русском, без избыточных комментариев — соблюдено.

## Out of Scope

### IN-01: `testables` в manifest.json синтаксически корректен, но editor-сборка не включена

**File:** `Samples~/Sample/Packages/manifest.json:42-44`
**Status:** не применялся (фикс-scope = `critical_warning`, IN-01 — info-уровень).
**Note:** Сам REVIEW.md указывает «Fix не требуется сейчас» — это пометка на будущее при добавлении Editor-тестов.

---

_Fixed: 2026-05-01_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
