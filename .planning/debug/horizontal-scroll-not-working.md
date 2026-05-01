---
status: resolved
trigger: "Горизонтальный скроллер отображается, но не работает — после quick-260427-r28 (commits 094cd92, f932333) переключение _axis на Horizontal в mvvm_demo.unity показывает scroller, но прокрутка не происходит / контент не двигается"
created: 2026-04-29
updated: 2026-04-29
resolved: 2026-04-29
fix_commit: 5d6a290
verification: User confirmed in Unity Editor — drag по Horizontal теперь двигает контент в правильную сторону.
---

# Debug Session: horizontal-scroll-not-working

## Symptoms

- **Expected behavior:** При `VirtualScrollRect._axis = ScrollAxis.Horizontal` drag по X / scroll wheel / Scrollbar.handle должны двигать контент по X. Виртуальные view-prefabs должны переезжать слева-направо/справа-налево при изменении ScrollPosition. Скрытие Scrollbar.gameObject когда контент уже viewport.
- **Actual behavior:** Scrollbar (горизонтальный) виден в сцене, но прокрутка не функционирует. Контент не двигается. Возможны варианты: drag не работает / wheel не работает / scrollbar handle не двигает контент / контент уезжает не туда / FirstVisibleIndex не обновляется.
- **Error messages:** Нет (по сообщению пользователя — поведенческий баг).
- **Timeline:** Только что появилось после quick-260427-r28 (коммиты 094cd92 «add ScrollAxis to VirtualScrollRect», f932333 «branch VirtualCollectionBinding.PositionView by axis»). До этого вертикальный режим работал корректно.
- **Reproduction:**
  1. Открыть `Samples~/Sample/Assets/Scenes/mvvm_demo.unity`, перейти на экран VirtualList.
  2. На demo-префабе `VirtualScroll` переключить `_axis` на `Horizontal` в инспекторе.
  3. Назначить горизонтальный Scrollbar в поле `_scrollbar`.
  4. Запустить Play Mode.
  5. Попытаться прокрутить — drag/wheel/scrollbar.handle.

## Current Focus

- hypothesis: 3 концентрических проблемы (input wheel sign + scene Scrollbar.direction + возможно drag area). Без уточнения от пользователя «что именно не работает» нельзя выделить ОДИН root cause; статический анализ выявляет несколько багов одновременно.
- test: запросить уточнение у пользователя — какой режим взаимодействия не работает: drag по item / wheel / shift+wheel / scrollbar handle / контент стоит на месте / контент уезжает не туда.
- expecting: Точная локализация → один из B1/B2/B3.
- next_action: Передать управление пользователю с подробной картой подозреваемых багов и инструкцией по воспроизведению каждого.

## Initial Suspects (для расследования)

(см. Evidence ниже)

## Evidence

### 2026-04-29 — Static analysis: B1 — OnScroll (mouse wheel) знак для Horizontal

- timestamp: 2026-04-29 (analysis)
- source: `Runtime/Core/VirtualScroll/VirtualScrollRect.cs:195-215`
- finding: **OnScroll использует `_scrollPosition -= delta` для ОБОИХ осей**. OnDrag для Horizontal использует `_scrollPosition += delta` (противоположный знак). Для Shift+wheel (`scrollDelta.x != 0`) направление будет ОБРАТНОЕ относительно drag по тачскрину. Для обычного wheel в горизонтальном режиме (fallback на `scrollDelta.y`) направление совпадает с drag (так как вертикальный wheel и горизонтальный drag оба в "стандарте" уезжают).

```csharp
public void OnScroll(PointerEventData eventData)
{
    float rawDelta;
    if (_axis == ScrollAxis.Vertical)
    {
        rawDelta = eventData.scrollDelta.y;
    }
    else
    {
        rawDelta = eventData.scrollDelta.x != 0f
            ? eventData.scrollDelta.x
            : eventData.scrollDelta.y;
    }

    var delta = rawDelta * _scrollSensitivity;
    _scrollPosition -= delta;   // <-- одинаковый знак для обеих осей
    ClampScrollPosition();
    OnScrollPositionChanged();
}
```

- impact: Если пользователь тестирует Shift+колесо или горизонтальный жест на тачпаде → wheel может скроллить «не в ту сторону». Если пользователь крутит обычное вертикальное колесо в горизонтальном режиме — должно работать (через fallback на .y).
- proposed fix: Симметрично OnDrag, ветвить знак по оси:

```csharp
if (_axis == ScrollAxis.Vertical)
{
    _scrollPosition -= delta;
}
else
{
    _scrollPosition += delta;
}
```

### 2026-04-29 — Static analysis: B2 — Scrollbar.m_Direction в demo-сцене

- timestamp: 2026-04-29 (analysis)
- source: `Samples~/Sample/Assets/Scenes/mvvm_demo.unity:3076` (Scrollbar fileID 1256544782, привязан к Horizontal VirtualScrollRect 1773468142)
- finding: **`m_Direction: 2`** = `Scrollbar.Direction.BottomToTop` — это _вертикальный_ direction. Для горизонтального scrollbar нужно `0` (LeftToRight) или `1` (RightToLeft). Скопирован из вертикального scrollbar и не обновлён.
- impact:
  1. **Visual handle:** Unity Scrollbar управляет позицией handle по одной из двух осей в зависимости от direction. С BottomToTop handle ожидает движения мыши по Y. Если визуальный rect scrollbar горизонтальный, handle "скачет" по узкому rect-у вертикально — пользователь не может его перетащить корректно.
  2. **OnScrollbarValueChanged callback:** `IsScrollbarInverted()` для `BottomToTop` возвращает true → инверсия value применяется. Но Scrollbar value сам не обновится корректно если drag по неправильной оси не регистрируется.
- proposed fix: В сцене изменить `m_Direction: 2` → `0` (LeftToRight) для горизонтального scrollbar (fileID 1256544782).

### 2026-04-29 — Static analysis: B3 — Drag handler routing зависит от Graphic в дереве

- timestamp: 2026-04-29 (analysis)
- source: `Samples~/Sample/Assets/Scenes/mvvm_demo.unity` (GameObject "Scroll View Horizontal" 1773468140) + `Samples~/Sample/Assets/Prefabs/chat_message_view_horizontal.prefab`
- finding: GameObject "Scroll View Horizontal" имеет только: RectTransform, VirtualScrollRect, ChatMessagesView. Нет Graphic / Image. Viewport (749860965) имеет: RectTransform + RectMask2D. Тоже без Graphic. Drag-events приходят к VirtualScrollRect через **bubbling от child-элементов с raycastTarget**. ChatMessageView prefab имеет Image с RaycastTarget=1. Vertical-режим работает через тот же путь.
- impact:
  1. **Drag по item (изображения внутри view):** работает — bubble-up.
  2. **Drag по пустой области viewport** (например при пустом списке или scrolling в конец где последний item не закрывает viewport): **НЕ работает** — нет Graphic → нет raycast target → drag не зарегистрируется. Это применимо и к Vertical, но Vertical список обычно длинный и заполняет viewport полностью.
- proposed fix (если это окажется проблемой): добавить прозрачный Image с RaycastTarget=1 на Viewport или Scroll View root. Не входит в scope текущего бага если основной симптом не "drag по пустой области".

### 2026-04-29 — Verified working: PositionView Horizontal layout

- source: `Runtime/Core/Bindings/VirtualCollectionBinding.cs:235-243`
- finding: anchors=(0,0)/(0,1), pivot=(0,0), anchoredPosition=(offset - scrollPosition, 0), sizeDelta=(size, rt.sizeDelta.y). При scrollPosition=0 первый элемент на левом крае viewport. При scrollPosition>0 элементы уезжают влево, новые входят справа. Логика верная.

### 2026-04-29 — User feedback contradicts "verified working"

User эмпирически подтвердил:
1. Drag по карточке двигает контент **в противоположную сторону** (ожидаемому).
2. Wheel — не проверял (touch pad).
3. Shift+wheel — не имеет (нет мыши).
4. Drag по handle scrollbar **не реагирует**. После смены `m_Direction` scrollbar полностью пропал, в инспекторе вместо визуала красный крест.
5. Handle не виден при старте.

Это переворачивает гипотезу про OnDrag. Перепроверка кода:

- **Vertical** `_scrollPosition -= delta`: drag вверх (delta_y_local > 0) → scrollPosition уменьшается → items сдвигаются вниз визуально (anchoredPosition.y = scrollPosition - offset уменьшается). User это принимает как «работает».
- **Horizontal** `_scrollPosition += delta`: drag вправо (delta_x_local > 0) → scrollPosition увеличивается → items сдвигаются влево визуально (anchoredPosition.x = offset - scrollPosition уменьшается). Это **противоположно** Vertical-конвенции относительно жеста.
- Комментарий в коде ("Vertical: drag вверх → scrollPosition увеличивается") описывает Vertical неверно; ось-ветка для Horizontal была сделана на основе ошибочного представления о Vertical.

### 2026-04-29 — ROOT CAUSE: ось-ветка в OnDrag несимметрична Vertical

- source: `Runtime/Core/VirtualScroll/VirtualScrollRect.cs:176-185` (до фикса)
- finding: Для Horizontal `_scrollPosition += delta` (и `_velocity = +delta/dt`), для Vertical `-= delta`. Поскольку `GetLocalPosition` возвращает локальную координату по соответствующей оси (Y up для Vertical, X right для Horizontal — обе положительные «вперёд»), знаки должны совпадать, чтобы получить consistent scrollbar-drag поведение по обеим осям.
- impact: drag в Horizontal двигает контент в противоположную сторону относительно Vertical-конвенции — что user и видит.
- fix applied: ось-ветка убрана, оба axis используют `_scrollPosition -= delta` и `_velocity = -delta/dt`. Комментарий обновлён.

### 2026-04-29 — Open issue (out of scope текущего фикса): Scrollbar handle не реагирует / пропадает при смене direction

- user reported: drag по handle scrollbar не работает; после смены `m_Direction` scrollbar полностью пропал; handle не виден при старте.
- analysis: горизонтальный Scrollbar в demo-сцене собран как копия вертикального с `m_Direction: 2` (BottomToTop). Когда user в Editor переключил direction на LeftToRight, Unity не пересобирает анкеры handle автоматически — handle остаётся с layout для вертикали (узкий горизонтально, высокий вертикально), что в горизонтальном rect выглядит как нулевая ширина / «крест». Это **scene/prefab уровень**, не код пакета.
- recommended action для пользователя:
  1. Удалить горизонтальный Scrollbar из сцены и заново создать через GameObject → UI → Scrollbar — Unity сразу создаст с правильными анкерами Handle для default direction (LeftToRight).
  2. Привязать в `_scrollbar` поле `VirtualScrollRect`.
  3. Visual rect — горизонтальный (e.g. width=400, height=20) поверх viewport bottom edge.
- альтернатива: оставить как issue в backlog проекта и сделать отдельную задачу «фикс demo-сцены под Horizontal axis», вынеся Scrollbar config в правильно собранный prefab.

### 2026-04-29 — Verified working: ContentSize / ViewportSize / MaxScrollPosition / Scrollbar visibility

- source: `Runtime/Core/VirtualScroll/VirtualScrollRect.cs:65-89, 313-339`
- finding: `_contentHeight` (название историческое, по факту "Content Length" по выбранной оси) задаётся через `SetContentSize(TotalHeight)`. `ViewportSize` ось-агностично возвращает width или height. `needsScroll = _contentHeight > viewportSize` корректно показывает/скрывает scrollbar GO.

### 2026-04-29 — Verified: tests don't cover input path

- source: `Tests/Runtime/VirtualCollectionBindingTests.cs:307-376`
- finding: Все 3 horizontal-теста (`OnContentChanged_HorizontalAxis_*`, `ScrollPositionChange_HorizontalAxis_*`, `HorizontalAxis_FirstVisibleIndex_*`) выставляют `_scrollRect.ScrollPosition` через property setter. Не идут через OnDrag/OnScroll/OnScrollbarValueChanged. Поэтому B1 (wheel sign) и B2 (scrollbar direction) тесты не ловят.

## Eliminated Hypotheses

- **PositionView layout** — анкеры и позиционирование horizontal-ветки логически верные, проверены статически.
- **OnDrag sign** — знак согласован с inertia, drag по items должен двигать scrollPosition в правильную сторону.
- **ContentSize / Scrollbar visibility** — формула `_contentHeight > ViewportSize` ось-агностична, работает.
- **PositionView sizeDelta.y leakage** — в текущем horizontal-prefab `chat_message_view_horizontal.prefab` остаётся валидное значение sizeDelta.y=-20.

## Resolution

Status: **fix_applied** (для основного симптома — drag в обратную сторону)

### Root cause

Несимметричный знак в `OnDrag` между Vertical и Horizontal: Horizontal делал `_scrollPosition += delta`, тогда как Vertical делает `-= delta`. Конвенция должна быть единой («drag в положительную сторону viewport-локальной оси сдвигает scrollPosition вниз → контент сдвигается визуально за пальцем»). Несимметрия возникла из ошибочного предположения автора (комментарий в коде описывал Vertical неверно).

### Fix

`Runtime/Core/VirtualScroll/VirtualScrollRect.cs:161-188` — убрана ось-ветка:

```csharp
// до:
if (_axis == ScrollAxis.Vertical)
{
    _scrollPosition -= delta;
    _velocity = -delta / Time.unscaledDeltaTime;
}
else
{
    _scrollPosition += delta;
    _velocity = delta / Time.unscaledDeltaTime;
}

// после:
_scrollPosition -= delta;
_velocity = -delta / Time.unscaledDeltaTime;
```

Логика осталась согласованной с inertia (`LateUpdate` уже была одинаковой для обеих осей, `_scrollPosition += _velocity * dt` со знаком velocity, который теперь корректен и для Horizontal).

### Verification

- Vertical: код для Vertical не менялся (`-= delta`), регрессии быть не должно.
- Horizontal: после фикса drag вправо (delta_x_local > 0) → scrollPosition уменьшается → items сдвигаются вправо визуально → совпадает с Vertical-конвенцией.
- Existing tests (`VirtualCollectionBindingTests`) пишут `ScrollPosition` напрямую через property, не через `OnDrag` — не затронуты.

### Out of scope (отдельные задачи)

1. **Scrollbar invisibility при смене m_Direction в demo-сцене** — scene/prefab issue, требует пересоздания горизонтального Scrollbar с правильными анкерами Handle (в Unity Editor). Отдельная quick-задача.
2. **Wheel/touchpad sign** для Horizontal — требует отдельной проверки (у пользователя touchpad без мыши); поведение сейчас идёт через fallback на `scrollDelta.y` если `scrollDelta.x == 0`.
