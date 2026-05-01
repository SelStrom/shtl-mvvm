---
phase: 01-virtualized-list
reviewed: 2026-05-01T00:00:00Z
depth: standard
files_reviewed: 2
files_reviewed_list:
  - Runtime/Core/VirtualScroll/VirtualScrollRect.cs
  - Samples~/Sample/Packages/manifest.json
findings:
  critical: 0
  warning: 4
  info: 1
  total: 5
status: issues_found
---

# Phase 01: Code Review Report (Wave 5 — gap-2, gap-3)

**Reviewed:** 2026-05-01
**Depth:** standard
**Files Reviewed:** 2
**Status:** issues_found

## Summary

Ревью покрывает два gap-closure плана Wave 5:

- **01-05**: добавление `"testables": ["com.shtl.mvvm"]` в `Samples~/Sample/Packages/manifest.json`.
- **01-06**: два исправления в `VirtualScrollRect.cs`:
  - gap-2: Elastic bounce теперь запускается даже при `velocity == 0`, если `offset != 0`.
  - gap-3: Drag/wheel блокируются через ранний return `if (_contentHeight <= ViewportSize)`.

Критических ошибок (потеря данных, краш, security) не выявлено. Найдено 4 предупреждения — логические баги и edge-case некорректности в реализованных исправлениях — и 1 информационный дефект.

Из предыдущего раунда ревью (2026-04-30): W-03 (scrollbar clamping) и W-04 (velocity reset в SetContentSize) теперь зафиксированы в коде — это подтверждается на строках 374–380 (`_velocity = 0f` при out-of-bounds) и 374–380 (`Mathf.Clamp` в `UpdateScrollbar`). W-07 (ResetScroll + OnEnable sync) также закрыт.

---

## Warnings

### WR-01: `_isDragging` может застрять в `false` при drag + динамическом изменении контента

**File:** `Runtime/Core/VirtualScroll/VirtualScrollRect.cs:175-186`

**Issue:** После gap-3 fix `OnBeginDrag` имеет ранний return при `_contentHeight <= ViewportSize`, НЕ устанавливая `_isDragging = true`. `OnEndDrag` не имеет такого guard и всегда выполняет `_isDragging = false`. Это создаёт следующий edge case:

1. Пользователь начинает drag. `_contentHeight > ViewportSize` → `OnBeginDrag` проходит насквозь, `_isDragging = true`.
2. Пока drag активен, `SetContentSize` уменьшает контент до `<= ViewportSize`.
3. Последующие `OnDrag` — ранний return, `_prevDragPosition` не обновляется.
4. Пользователь отпускает палец → `OnEndDrag` → `_isDragging = false`. Всё корректно, последствий нет.

НО симметричный сценарий в обратную сторону создаёт реальный баг:

1. `_contentHeight <= ViewportSize` → `OnBeginDrag` ранний return, `_isDragging` остаётся `false`, `_prevDragPosition` **не инициализируется** (содержит значение из предыдущего drag-сессии).
2. Пока "drag" в процессе (pointer зажат), `SetContentSize` увеличивает контент сверх ViewportSize.
3. Следующий `OnDrag` проходит guard, вычисляет `delta = pos - _prevDragPosition`, где `_prevDragPosition` — **stale значение из прошлой сессии** → мусорный `delta`, резкий прыжок контента.

**Fix:** Устанавливать `_isDragging = true` до guard, чтобы пара Begin/End всегда была симметричной, и инициализировать `_prevDragPosition` безусловно:

```csharp
public void OnBeginDrag(PointerEventData eventData)
{
    _isDragging = true;
    _prevDragPosition = GetLocalPosition(eventData); // всегда, чтобы не было stale

    // Скролл недоступен — контент помещается во viewport без прокрутки
    if (_contentHeight <= ViewportSize)
    {
        return;
    }

    _velocity = 0f;
}
```

---

### WR-02: Точное float-сравнение `_velocity == 0f` ненадёжно

**File:** `Runtime/Core/VirtualScroll/VirtualScrollRect.cs:258`

**Issue:** Строка `if (_velocity == 0f && offset == 0f)` использует точное сравнение float на равенство нулю для принятия решения об early-return из `LateUpdate`. `_velocity` может принимать значение `!= 0f`, но `< epsilon` после `Mathf.SmoothDamp` — Unity's SmoothDamp реализован как численный интегратор и не гарантирует точного возврата `0f` при достижении target.

Конкретный сценарий: после Elastic return `SmoothDamp` может вернуть `_velocity = 2.4e-7f`. Тогда:
- `_velocity == 0f` → `false`
- Guard не срабатывает, `LateUpdate` продолжает работать
- Попадаем в `else if (_inertia)` ветку (т.к. `offset == 0f` после `ClampScrollPosition`)
- `_velocity *= Mathf.Pow(0.135f, deltaTime)` → velocity затухает, но с `2.4e-7` начальным значением это займёт ~80 кадров до порога `1f`
- За эти 80 кадров `_scrollPosition` меняется на `2.4e-7 * 0.016 * 80 ≈ 3e-10f` — визуально незначимо, но `OnScrollPositionChanged` вызывается на каждом кадре, что триггерит пересчёт видимого диапазона

Порог `1f` на строке 285 должен был бы «поймать» это, но он применяется **после** Pow-шага, а не как ранний выход.

**Fix:** Объединить guard со снизу стоящим порогом:

```csharp
// Прерываем цикл только если нет ни значимой инерции, ни offset для Elastic-возврата.
if (Mathf.Abs(_velocity) < 1f && offset == 0f)
{
    _velocity = 0f;
    return;
}
```

И удалить дублирующую проверку на строке 285. Это также делает логику линейной вместо двойного условия.

---

### WR-03: Velocity stop threshold `1f` (px/s) не масштабируется с viewport

**File:** `Runtime/Core/VirtualScroll/VirtualScrollRect.cs:285`

**Issue:** `Mathf.Abs(_velocity) < 1f` — порог в пикселях в секунду захардкожен как `1f`. Velocity получается через `_velocity = -delta / Time.unscaledDeltaTime` в `OnDrag`: при `delta = 5px` за кадр `16ms` это `~312 px/s`. При `_decelerationRate = 0.135f`:

```
velocity_after_N_seconds ≈ v0 * (0.135)^N
312 * (0.135)^N < 1f  →  N > log(312) / log(1/0.135) ≈ 1.34 секунды
```

При этом само по себе это было до данного PR. Проблема усугубляется тем, что изменение на строке 285 добавило условие `&& offset == 0f`, которое логически верно, но делает порог "de-facto невидимым" в Elastic-ветке: в Elastic-ветке `_velocity` задаётся `SmoothDamp`, который заканчивает работу только когда приближается к target достаточно близко. Снаружи Elastic-ветки (inertia-ветка) 1 px/s порог работает, но медленное затухание от малых значений velocity всё равно затратно в кадрах.

Значение `1f` является магическим числом без комментария.

**Fix:** Вынести в именованную константу с пояснением:

```csharp
// Порог остановки инерции: ниже 1 px/s движение визуально незаметно.
// При необходимости масштабировать относительно ViewportSize.
private const float VelocityStopThreshold = 1f;
// ...
if (Mathf.Abs(_velocity) < VelocityStopThreshold && offset == 0f)
```

---

### WR-04: `LateUpdate` не блокируется при `_contentHeight <= ViewportSize`, но drag/wheel — блокируются

**File:** `Runtime/Core/VirtualScroll/VirtualScrollRect.cs:248-292`

**Issue:** gap-3 добавил guard `_contentHeight <= ViewportSize` в `OnBeginDrag`, `OnDrag`, `OnScroll`. `LateUpdate` не имеет аналогичного guard и продолжает обрабатывать `offset` и velocity когда контент умещается во viewport.

Сценарий после gap-3:

1. Большой контент → пользователь колесом прокрутил до `_scrollPosition > 0` → инерция накоплена (хотя `OnScroll` не устанавливает `_velocity`, инерция не была целью)... Но `OnScroll` действительно не устанавливает `_velocity`. Поэтому инерции от wheel нет. ОК.

2. Drag-овый случай: пользователь с velocity drag-ает, затем `SetContentSize` уменьшает контент до `<= ViewportSize` **до** `OnEndDrag`. `_velocity` осталась ненулевой (накоплена drag-ом). `LateUpdate` продолжает применять inertia к `_scrollPosition`, хотя drag заблокирован. Если `MovementType = Elastic`, `CalculateOffset()` вернёт ненулевой `offset` (т.к. `_scrollPosition > MaxScrollPosition() = 0` при пустом/маленьком контенте), и SmoothDamp начнёт Elastic-возврат к 0. Это **визуально корректное поведение** — контент должен вернуться на место.

НО: если `_movementType != Elastic` (например `Unrestricted`), то `LateUpdate` будет продолжать двигать `_scrollPosition` при `_contentHeight <= ViewportSize` бесконечно (velocity затухает, но `ClampScrollPosition` не клампит для Unrestricted). Контент будет уплывать за пределы viewport без возможности вернуть его через drag (drag заблокирован guard'ом).

Это граничный случай (`Unrestricted` mode с маленьким контентом и остаточной velocity), но он реален при динамическом изменении контента.

**Fix:** Либо явно документировать что `LateUpdate` намеренно обрабатывает возврат при `_contentHeight <= ViewportSize` (и тогда `SetContentSize` должен обнулять velocity более агрессивно), либо добавить guard в `LateUpdate` для `Unrestricted` mode:

```csharp
private void LateUpdate()
{
    if (_isDragging)
    {
        return;
    }

    var offset = CalculateOffset();

    // При Unrestricted-mode и маленьком контенте -- нечего двигать.
    if (_movementType == MovementType.Unrestricted && _contentHeight <= ViewportSize)
    {
        _velocity = 0f;
        return;
    }
    // ...
}
```

Для Elastic mode guard не нужен — LateUpdate корректно обрабатывает возврат к 0.

---

## Info

### IN-01: `testables` в manifest.json синтаксически корректен, но editor-сборка не включена

**File:** `Samples~/Sample/Packages/manifest.json:42-44`

**Issue:** Добавленный блок:

```json
"testables": [
  "com.shtl.mvvm"
]
```

Синтаксически корректен (валидный JSON), расположен на правильном уровне (корневой объект после `dependencies`). Однако `Editor/Shtl.Mvvm.Editor.asmdef` — отдельная assembly definition. Если в будущем будут добавлены Editor-тесты, они потребуют отдельной записи `"com.shtl.mvvm.editor"`. Сейчас `Tests/Runtime/` и `Tests/Editor/` пусты — не баг.

**Fix:** Не требуется сейчас. При добавлении Editor-тестов расширить массив:

```json
"testables": [
  "com.shtl.mvvm",
  "com.shtl.mvvm.editor"
]
```

---

_Reviewed: 2026-05-01_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
