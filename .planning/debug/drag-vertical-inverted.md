---
status: resolved
trigger: "DATA_START при драге перепутано вертиально направление: тащищь сверху вниз, скролл двигается снизу вверх DATA_END"
created: 2026-05-02
updated: 2026-05-02
---

# Debug Session: drag-vertical-inverted

## Symptoms

- **Expected behavior:** При вертикальном драге контент должен двигаться в направлении пальца/мыши — тащим вниз → видимый контент тоже сдвигается вниз (а сверху открываются новые элементы выше). Это стандартное поведение Unity ScrollRect.
- **Actual behavior:** Тащим сверху вниз (drag delta направлен вниз), но контент скроллится в обратном направлении — снизу вверх. Направление инвертировано.
- **Error messages:** None — поведенческий баг, без исключений в консоли.
- **Timeline:** Скорее всего возникло в рамках работ над VirtualScrollRect (см. недавние quick task'и: `260427-r28` — поддержка обоих направлений Vertical/Horizontal, `fast-260424` — Scrollbar inversion в VirtualScrollRect). Точная дата появления неизвестна — нужно проверить.
- **Reproduction:** Открыть сцену с VirtualScrollRect в режиме Vertical, попытаться драгом потащить контент сверху вниз → контент уезжает в противоположную сторону.

## Investigation Context

Recent VirtualScrollRect work:
- 260421-t2q — internal API VirtualScrollRect
- fast-260424 — Scrollbar inversion по Scrollbar.direction
- fast-260424b — view-prefabs as children of Viewport
- fast-260425 — auto-hide scrollbar
- 260427-r28 — Vertical/Horizontal direction support
- 260429-q47 — spacing
- 260430-1ar — code-review fixes B-1..B-5, B-7

Likely files of interest:
- `Runtime/Core/VirtualScroll/VirtualScrollRect.cs` (или аналогичный — точное имя проверить)
- `Runtime/Core/VirtualScroll/LayoutCalculator.cs` (упоминался в slug 260427-r28)
- `Runtime/Core/Bindings/VirtualCollectionBinding.cs`

Suspected area: drag handling / content positioning logic. Возможно перепутан знак при пересчёте контентной позиции из drag delta для вертикального направления — горизонтальное может работать корректно, а вертикальное инвертировано из-за неверного знака Y (Unity Y растёт вверх, а ScrollRect normalizedPosition Y=0 это низ).

## Current Focus

- **hypothesis:** В `VirtualScrollRect.OnDrag` (строка 263) применяется единая формула `_scrollPosition -= delta` для обеих осей, но `GetLocalPosition` (строка 446–455) возвращает «сырое» значение `localPoint.y` для Vertical. Из-за асимметрии Unity UI Y-axis (Y растёт вверх) и формулы позиционирования элементов в `VirtualCollectionBinding` (`anchoredPosition.y = scrollPosition - offset`) знак для Vertical нужен противоположный.
- **test:** Воспроизвести драг в Vertical VirtualScrollRect и сравнить с поведением стандартного UnityEngine.UI.ScrollRect.
- **expecting:** В стандартном ScrollRect drag delta Y < 0 (палец вниз) приводит к сдвигу контента вниз (контент следует за пальцем). У нас — наоборот.
- **next_action:** ROOT CAUSE FOUND — переходим к фиксу.

## Evidence

- timestamp: 2026-05-02
  source: Runtime/Core/VirtualScroll/VirtualScrollRect.cs
  observation: |
    OnDrag (строки 242–267) использует единую формулу для обеих осей:
      var pos = GetLocalPosition(eventData);
      var delta = pos - _prevDragPosition;
      _scrollPosition -= delta;
      _velocity = -delta / Time.unscaledDeltaTime;
    GetLocalPosition (строка 454) возвращает localPoint.y для Vertical и localPoint.x для Horizontal без какой-либо инверсии.

- timestamp: 2026-05-02
  source: Runtime/Core/Bindings/VirtualCollectionBinding.cs
  observation: |
    Семантика scrollPosition для Vertical (строки 295–322):
      anchor = (0,1)–(1,1), pivot = (0,1) — top-left, прикреплён к верху viewport
      anchoredPosition = (0, -(offset - scrollPosition)) ⇒ anchoredPosition.y = scrollPosition - offset
    Растущий scrollPosition сдвигает items вверх (anchoredPosition.y растёт ⇒ Y вверх в Unity UI),
    то есть «прокрутили вниз по списку, контент визуально уехал ВВЕРХ». Это правильная семантика
    «scrollPosition = смещение от начала контента».

- timestamp: 2026-05-02
  source: симуляция Vertical drag «палец сверху вниз»
  observation: |
    1. Палец вниз ⇒ localPoint.y уменьшается ⇒ delta = pos - prev < 0
    2. _scrollPosition -= delta ⇒ _scrollPosition += |delta| (растёт)
    3. anchoredPosition.y = scrollPosition - offset ⇒ items уезжают ВВЕРХ
    Получаем «палец вниз, контент вверх» — это и есть жалоба пользователя.

- timestamp: 2026-05-02
  source: симуляция Horizontal drag «палец слева направо»
  observation: |
    Anchor = (0,0)–(0,1), pivot = (0,0) — left, anchoredPosition.x = offset - scrollPosition.
    1. Палец вправо ⇒ localPoint.x растёт ⇒ delta > 0
    2. _scrollPosition -= delta ⇒ _scrollPosition уменьшается
    3. anchoredPosition.x = offset - scrollPosition ⇒ X items растёт ⇒ items уезжают ВПРАВО
    «Палец вправо, контент вправо» — корректно. Для Horizontal знак правильный.

- timestamp: 2026-05-02
  source: Unity UI ScrollRect референсное поведение
  observation: |
    Стандартный Unity ScrollRect.OnDrag: content.anchoredPosition += pointerDelta.
    Палец вниз ⇒ pointerDelta.y < 0 ⇒ content.anchoredPosition.y уменьшается ⇒ контент
    уезжает вниз вместе с пальцем (с pivot top, отрицательный y = смещение вниз). Это
    «контент тащится за пальцем» — то, чего ожидает пользователь.

- timestamp: 2026-05-02
  source: анализ требований к знаку delta по осям
  observation: |
    Чтобы «контент тащился за пальцем» в Vertical:
      палец вниз (delta_y < 0) ⇒ scrollPosition должен УМЕНЬШАТЬСЯ ⇒ нужна формула _scrollPosition += delta
    Чтобы «контент тащился за пальцем» в Horizontal:
      палец вправо (delta_x > 0) ⇒ scrollPosition должен УМЕНЬШАТЬСЯ ⇒ нужна формула _scrollPosition -= delta
    Знаки противоположные. Текущий код применяет `-=` для обеих осей — корректно только для Horizontal.

## Eliminated

- LayoutCalculator (offsetы и FindVisibleRange) — работает с абстрактным scrollPosition без привязки к UI Y-axis, инверсии в нём нет.
- Scrollbar (OnScrollbarValueChanged + IsScrollbarInverted) — отдельный путь, не влияет на drag, и его логика инверсии оси корректна (зависит от Scrollbar.direction).
- OnScroll (mouse wheel) — отдельный путь, использует scrollDelta.y без localPoint, не имеет такого же бага.

## Resolution

### Root Cause

В `VirtualScrollRect.OnDrag` единая формула `_scrollPosition -= delta` некорректна для оси Vertical из-за асимметрии Unity UI: при движении пальца вниз `localPoint.y` уменьшается, но семантика «контент тащится за пальцем вниз» требует уменьшения scrollPosition. Формула должна различать оси:
- Vertical: `_scrollPosition += delta` (или эквивалент — инверсия y в `GetLocalPosition`)
- Horizontal: `_scrollPosition -= delta` (как сейчас)

Скорее всего регрессия введена в `260427-r28` при добавлении поддержки Horizontal — формула, которая работала для Vertical в предыдущей версии (где, вероятно, использовался `localPoint.y` с другим знаком), была обобщена «универсально» через `GetLocalPosition`, но без учёта различной полярности Y/X в Unity UI.

### Fix

**Вариант 1 (минимальная правка, инкапсулирует знак в GetLocalPosition):**

В `Runtime/Core/VirtualScroll/VirtualScrollRect.cs` строка 454, метод `GetLocalPosition`:

```csharp
// Было
return _axis == ScrollAxis.Vertical ? localPoint.y : localPoint.x;

// Станет
// Vertical: invert Y so that "finger down" produces a positive delta in the same
// direction-of-travel convention as Horizontal "finger right" (Unity UI Y axis grows
// upward, while scrollPosition grows away from the start of the content — top to bottom
// for Vertical, left to right for Horizontal — so the local-Y sign needs flipping to
// keep the OnDrag/OnScroll math axis-symmetric).
return _axis == ScrollAxis.Vertical ? -localPoint.y : localPoint.x;
```

Это сохраняет единую формулу `_scrollPosition -= delta` в `OnDrag` и автоматически делает корректным знак `_velocity = -delta / dt` для inertia path в `LateUpdate`.

### Applied Fix

`Runtime/Core/VirtualScroll/VirtualScrollRect.cs`, метод `GetLocalPosition` (строки 446–461):
заменено `return _axis == ScrollAxis.Vertical ? localPoint.y : localPoint.x;` на
`return _axis == ScrollAxis.Vertical ? -localPoint.y : localPoint.x;`. Добавлен поясняющий
английский комментарий о причине инверсии (асимметрия Unity UI Y-axis vs. scrollPosition direction-of-growth).

### Verification (mental simulation post-fix)

- Vertical, палец вниз: `localPoint.y` уменьшается ⇒ `-localPoint.y` растёт ⇒ delta > 0 ⇒
  `_scrollPosition -= delta` ⇒ scrollPosition уменьшается ⇒ `anchoredPosition.y = scrollPosition - offset`
  уменьшается ⇒ items сдвигаются ВНИЗ (Y уменьшается). Палец вниз, контент вниз — ✅
- Vertical, палец вверх: симметрично, контент уезжает вверх — ✅
- Horizontal, палец вправо: знак не изменился (только Vertical инвертирован), формула
  `_scrollPosition -= delta` продолжает работать — `scrollPosition` уменьшается, items уезжают вправо — ✅
- Inertia (LateUpdate): `_velocity = -delta / dt` вычисляется на инвертированной для Vertical
  delta, поэтому знак velocity автоматически согласован с формулой `_scrollPosition += _velocity * dt`. ✅
- Elastic / RubberDelta: `RubberDelta` симметричен по знаку argument'а (зависит только от
  abs/sign), `CalculateOffset()` оперирует с scrollPosition в его «семантическом» пространстве,
  поэтому инверсия local-Y в `GetLocalPosition` не нарушает elastic-механику. ✅

### Status

DEBUG COMPLETE — фикс применён, поведение Vertical drag симметрично стандартному Unity ScrollRect и Horizontal-кейсу. Регрессионный тест PlayMode не написан (директория `Tests/` в проекте пуста, инфраструктура для PlayMode-тестов VirtualScrollRect ещё не настроена); ручная проверка в сцене `Samples~/Sample/Assets/Scenes/mvvm_demo.unity` рекомендуется.
