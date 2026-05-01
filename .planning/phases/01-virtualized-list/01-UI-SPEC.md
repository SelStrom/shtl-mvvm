---
phase: 1
slug: virtualized-list
status: draft
shadcn_initialized: false
preset: none
created: 2026-04-10
---

# Phase 1 — UI Design Contract: Виртуализированный список

> Визуальный и interaction-контракт для runtime-компонента виртуализированного списка Unity uGUI. Этот контракт описывает поведенческие параметры, а не веб-стили — проект является Unity C# UPM-пакетом.

---

## Design System

| Property | Value |
|----------|-------|
| Tool | none (Unity uGUI runtime-библиотека) |
| Preset | not applicable |
| Component library | Unity uGUI (`com.unity.ugui` 1.0.0) |
| Icon library | not applicable |
| Font | not applicable (шрифт определяется потребителем пакета через TMP_Text) |

> Источник: CONTEXT.md D-07, RESEARCH.md Project Constraints

---

## Spacing Scale

Не применимо в классическом смысле. Виртуализированный список — runtime-компонент, spacing определяется потребителем через высоту элементов (`Func<int, float>` или фиксированная высота). Однако внутренние параметры layout зафиксированы ниже.

### Layout-параметры компонента

| Параметр | Значение по умолчанию | Единица | Обоснование |
|----------|----------------------|---------|-------------|
| Overscan (сверху) | 2 элемента | int | Предотвращает мерцание при скролле; источник: RESEARCH.md рекомендация |
| Overscan (снизу) | 2 элемента | int | Симметричный overscan для плавности |
| Content padding top | 0 | px (float) | Настраивается потребителем через SerializeField |
| Content padding bottom | 0 | px (float) | Настраивается потребителем через SerializeField |
| Минимальная высота элемента | 1f | px (float) | Guard от деления на ноль и некорректных layout |

Исключения: Overscan параметризуется через публичное свойство `OverscanCount` на `VirtualScrollRect`. Потребитель может переопределить значение по умолчанию.

---

## Typography

Не применимо. Виртуализированный список — layout-компонент, текстовое содержимое определяется потребителем в View-элементах через TMP_Text биндинги.

---

## Color

Не применимо. Виртуализированный список не задает цвета — визуальное оформление определяется потребителем в View-элементах (prefab).

---

## Interaction Contract

### Скролл-механизм (VirtualScrollRect)

Источник: CONTEXT.md D-07, RESEARCH.md Pattern 3

| Параметр | Значение по умолчанию | Тип | SerializeField | Обоснование |
|----------|----------------------|-----|----------------|-------------|
| Inertia | true | bool | да | Стандартное поведение ScrollRect |
| Deceleration Rate | 0.135f | float [0..1] | да | Идентично Unity ScrollRect default |
| Elasticity | 0.1f | float | да | Идентично Unity ScrollRect default |
| Scroll Sensitivity | 35f | float | да | Идентично Unity ScrollRect default для mouse wheel |
| Movement Type | Elastic | enum {Elastic, Clamped, Unrestricted} | да | Elastic — стандартное поведение для мобильных |
| Scroll Direction | Vertical | фиксировано | нет | VLIST-03: только вертикальная прокрутка в v1 |

### Поведение инерции

| Состояние | Формула | Источник |
|-----------|---------|----------|
| Инерция (свободный скролл) | `velocity *= Mathf.Pow(decelerationRate, deltaTime)` | Unity ScrollRect |
| Elastic bounce | `Mathf.SmoothDamp(position, target, ref velocity, elasticity, Infinity, deltaTime)` | Unity ScrollRect |
| Rubber delta (перетягивание за границы) | `(1 - 1 / (Abs(overStretching) * 0.55f / viewSize + 1)) * viewSize * Sign(overStretching)` | Unity ScrollRect |
| Порог остановки | `Abs(velocity) < 1f` → `velocity = 0` | Unity ScrollRect |

### Drag-обработка

| Событие | Поведение |
|---------|-----------|
| IBeginDragHandler | Обнуляет velocity, начинает tracking позиции |
| IDragHandler | Обновляет scroll position по delta; применяет RubberDelta за границами |
| IEndDragHandler | Рассчитывает velocity из последних drag-delta для инерции |
| IScrollHandler | Сдвигает scroll position на `scrollDelta.y * scrollSensitivity`; без инерции от scroll wheel |

### Mouse Wheel

| Параметр | Значение |
|----------|----------|
| Sensitivity | 35f (SerializeField) |
| Инерция от wheel | Нет — мгновенный сдвиг позиции |
| Направление | `scrollDelta.y > 0` — вверх (уменьшение scroll position) |

### Короткий список (contentHeight ≤ viewportHeight)

Когда суммарная высота контента не превышает высоту viewport, скроллить некуда — `MaxScrollPosition()` возвращает 0. В этом случае все input-события блокируются на уровне guard'а.

| Событие | Поведение |
|---------|-----------|
| OnBeginDrag | Игнорируется (_isDragging не устанавливается) |
| OnDrag | Игнорируется (_scrollPosition не изменяется) |
| OnScroll (wheel) | Игнорируется (_scrollPosition не изменяется) |
| Scrollbar | Скрыт (needsScroll = false, UpdateScrollbar скрывает gameObject) |

> Реализация: guard `if (_contentHeight <= ViewportSize) return;` в начале OnBeginDrag, OnDrag, OnScroll. Rubber-band эффект полностью устранён — _scrollPosition остаётся равным 0.

---

## Viewport Culling Contract

Источник: RESEARCH.md Pattern 1, VLIST-01

### Алгоритм определения видимого диапазона

| Шаг | Операция | Сложность |
|-----|----------|-----------|
| 1 | Prefix sum array пересчитывается при изменении данных (add/remove/replace/contentChanged) | O(n) разово |
| 2 | Binary search по prefix sum для `scrollPosition` → `firstVisibleIndex` | O(log n) |
| 3 | Binary search по prefix sum для `scrollPosition + viewportHeight` → `lastVisibleIndex` | O(log n) |
| 4 | Расширение диапазона на overscan: `firstVisible - overscanCount` .. `lastVisible + overscanCount` | O(1) |
| 5 | Clamping: `max(0, first)` .. `min(itemCount - 1, last)` | O(1) |

### Оптимизация для фиксированной высоты

Когда `heightProvider == null` (фиксированная высота):

| Шаг | Операция | Сложность |
|-----|----------|-----------|
| 1 | `firstVisible = (int)(scrollPosition / fixedHeight)` | O(1) |
| 2 | `lastVisible = (int)((scrollPosition + viewportHeight) / fixedHeight)` | O(1) |
| 3-5 | Overscan и clamping — идентично | O(1) |

### Dirty Flag

| Событие | Действие |
|---------|----------|
| ReactiveList.onElementAdded | `_isDirty = true` |
| ReactiveList.onElementRemoved | `_isDirty = true` |
| ReactiveList.onElementReplaced | `_isDirty = true` (высота могла измениться) |
| ReactiveList.onContentChanged | `_isDirty = true` |
| LateUpdate (если `_isDirty`) | Пересчёт prefix sum → пересчёт visible range → обновление View |

---

## Recycling Contract

Источник: RESEARCH.md Pattern 2, VLIST-02, VLIST-06

### Lifecycle элемента

| Переход | Действие | Аллокация |
|---------|----------|-----------|
| Pool → Visible | `pool.Get()` → `SetActive(true)` + `RectTransform.anchoredPosition` + `view.Connect(viewModel)` | 0 (переиспользование) |
| Visible → Pool | `view.Dispose()` → `SetActive(false)` → `pool.Push(view)` | 0 |
| Нет в pool → Visible | `factory.CreateWidget(viewModel)` → `view.Connect(viewModel)` | 1 GameObject (однократно) |
| Dispose всего | Все View в pool и active → `factory.RemoveWidget(view)` или `Object.Destroy` | 0 |

### Стратегия скрытия элементов

| Подход | Решение | Обоснование |
|--------|---------|-------------|
| Primary | `GameObject.SetActive(false)` при помещении в pool | Простота; Canvas rebuild происходит только при выходе/входе элемента, а не каждый кадр; RESEARCH.md Pitfall 5 рекомендует alternatives, но `SetActive` — проверенный паттерн ElementCollectionBinding |
| Fallback (будущее) | `CanvasGroup.alpha = 0` + `blocksRaycasts = false` | Для случаев, когда профайлер покажет проблемы с Canvas rebuild — не в scope v1 |

> Решение: используем `SetActive(false/true)` как в существующем `ElementCollectionBinding`. Если профайлер покажет проблемы с `Canvas.SendWillRenderCanvases`, оптимизация — в отдельной задаче.

### Pool sizing

| Параметр | Значение | Обоснование |
|----------|----------|-------------|
| Начальный размер pool | 0 | Lazy allocation — View создаются по мере необходимости |
| Максимальный размер pool | Не ограничен | Пул растёт естественно до `maxVisibleCount + 2 * overscan`; ограничение не нужно |
| Pre-warm | Нет | Первый скролл может быть чуть медленнее; приемлемо для v1 |

---

## Позиционирование элементов

### RectTransform Contract

| Свойство | Значение | Обоснование |
|----------|----------|-------------|
| Anchor | Top-Left (0, 1) / (0, 1) | Позиционирование сверху вниз по вертикали |
| Pivot | (0, 1) | Top-left для упрощения расчёта y-позиции |
| anchoredPosition.x | 0 | Полная ширина viewport |
| anchoredPosition.y | `-prefixHeights[index] + scrollPosition` | Позиция элемента относительно viewport |
| sizeDelta.x | viewport width | Элемент растягивается на ширину viewport |
| sizeDelta.y | `GetItemHeight(index)` | Высота из heightProvider или fixedHeight |

### Content Height

| Состояние | Значение |
|-----------|----------|
| Пустой список | 0 |
| N элементов, фиксированная высота | `N * fixedHeight` |
| N элементов, переменная высота | `prefixHeights[N]` (последний элемент prefix sum) |

---

## Состояния компонента

### Пустой список (0 элементов)

| Аспект | Поведение |
|--------|-----------|
| Scroll position | 0, скролл заблокирован (нет контента) |
| Visible range | `(0, 0)` — пустой диапазон |
| Pool | Пустой |
| Content height | 0 |
| Drag | Не вызывает изменений |
| View-элементы | Отсутствуют в иерархии |

> Примечание: пустое состояние визуально — ответственность потребителя. VirtualScrollRect не рисует placeholder. Потребитель может показать/скрыть overlay через биндинг `ReactiveVirtualList.Items.Count` → `GameObject.SetActive`.

### Первичное заполнение (Connect)

| Шаг | Действие |
|-----|----------|
| 1 | Рассчитать prefix sum для всех элементов |
| 2 | Установить scroll position = 0 |
| 3 | Определить visible range (0 .. firstPageCount + overscan) |
| 4 | Создать View для видимых элементов через factory/Instantiate |
| 5 | Позиционировать каждый View по prefix sum |
| 6 | Connect каждый View с соответствующим ViewModel |

### Данные изменяются во время скролла

Источник: RESEARCH.md Pitfall 1

| Событие | Поведение |
|---------|-----------|
| Add в середину видимого диапазона | Пересчёт prefix sum → сдвиг позиций всех элементов ниже → recycling если новый элемент в viewport |
| Remove из видимого диапазона | Dispose + pool removed View → пересчёт prefix sum → возможное подтягивание нового элемента снизу |
| Add/Remove за пределами viewport | Пересчёт prefix sum → корректировка scroll position если элемент выше viewport (сохранение визуальной позиции) |
| Clear | Dispose всех View → pool → scroll position = 0 |
| Replace в видимом диапазоне | Dispose старого VM на View → Connect нового VM → пересчёт высоты если изменилась |

### Scroll position корректировка при мутации

| Мутация | Корректировка scroll position |
|---------|-------------------------------|
| Add выше viewport | `scrollPosition += newItemHeight` (сохранение визуальной позиции) |
| Remove выше viewport | `scrollPosition -= removedItemHeight` (сохранение визуальной позиции) |
| Add ниже viewport | Без корректировки |
| Remove ниже viewport | Без корректировки; clamp если scroll position > maxScroll |
| Add/Remove в viewport | Без корректировки scroll position; visible range пересчитывается |

---

## Copywriting Contract

Не применимо. Виртуализированный список — runtime-компонент без собственного текстового содержимого. Весь текст определяется потребителем в View-элементах.

### Debug/Exception сообщения

| Ситуация | Сообщение | Тип |
|----------|-----------|-----|
| Повторный Connect на ReactiveVirtualList | `"ReactiveVirtualList is already bound"` | InvalidOperationException |
| Null prefab в .To() | `"Prefab cannot be null"` | ArgumentNullException |
| Null scrollRect в .To() | `"VirtualScrollRect cannot be null"` | ArgumentNullException |
| Negative item height | `"Item height must be positive, got {height} at index {index}"` | ArgumentException |
| heightProvider вернул <= 0 | `"Height provider returned non-positive value {value} for index {index}"` | InvalidOperationException |

> Стиль: идентичен существующему в ReactiveValue/ReactiveList — исключения на программные ошибки, без Debug.Log в ядре.

---

## Серверная API / Scrollbar Contract

### Scrollbar (опционально)

Источник: CONTEXT.md D-07

| Параметр | Значение | Обоснование |
|----------|----------|-------------|
| Поддержка | Опциональный SerializeField `Scrollbar _scrollbar` | Аналогично Unity ScrollRect |
| Синхронизация | Bidirectional: scroll position ↔ scrollbar value | Стандартное поведение |
| scrollbar.size | `viewportHeight / contentHeight` | Пропорциональный thumb |
| scrollbar.value | `scrollPosition / maxScrollPosition` | Нормализованная позиция [0..1] |
| Без scrollbar | Компонент полностью функционален без scrollbar | Scrollbar не обязателен |

---

## Public API Surface Contract

### VirtualScrollRect (MonoBehaviour)

| Свойство/Метод | Тип | SerializeField | Описание |
|----------------|-----|----------------|----------|
| `_viewport` | RectTransform | да | Область видимости (mask) |
| `_scrollbar` | Scrollbar | да (опционально) | Полоса прокрутки |
| `_inertia` | bool | да | Включить инерцию |
| `_decelerationRate` | float | да | Скорость затухания [0..1] |
| `_elasticity` | float | да | Упругость bounce |
| `_scrollSensitivity` | float | да | Чувствительность mouse wheel |
| `_overscanCount` | int | да | Количество элементов overscan в каждом направлении |
| `ScrollPosition` | float (property) | нет | Текущая позиция скролла (read/write) |
| `Velocity` | float (property, readonly) | нет | Текущая скорость скролла |
| `ScrollTo(float position)` | method | нет | Программный скролл к позиции |
| `ScrollToIndex(int index)` | method | нет | Программный скролл к элементу по индексу |

### ReactiveVirtualList<T>

| Свойство/Метод | Тип | Описание |
|----------------|-----|----------|
| `Items` | ReactiveList<T> (readonly field) | Внутренняя коллекция данных |
| `ScrollPosition` | ReactiveValue<float> (readonly field) | Текущая позиция скролла |
| `FirstVisibleIndex` | ReactiveValue<int> (readonly field) | Индекс первого видимого элемента |
| `VisibleCount` | ReactiveValue<int> (readonly field) | Количество видимых элементов |
| `Add(T)` | method | Проксирование к Items.Add |
| `RemoveAt(int)` | method | Проксирование к Items.RemoveAt |
| `Clear()` | method | Проксирование к Items.Clear |
| `Count` | int (property) | Проксирование к Items.Count |
| `this[int]` | T (indexer) | Проксирование к Items[index] |
| `GetItemHeight(int)` | float | Высота элемента через heightProvider или fixedHeight |

### Extension-метод .To()

```csharp
// Перегрузка 1: prefab + VirtualScrollRect
Bind.From(vm.Items).To(prefab, scrollRect);

// Перегрузка 2: factory + VirtualScrollRect
Bind.From(vm.Items).To(factory, scrollRect);
```

---

## Performance Contract

Источник: VLIST-07, RESEARCH.md

| Метрика | Требование | Как проверять |
|---------|------------|---------------|
| GC Alloc в кадре скролла | 0 bytes | Unity Profiler → GC.Alloc column при прокрутке |
| Одновременно в иерархии | visibleCount + 2 * overscanCount | Hierarchy window → child count |
| Binary search per frame | 2 вызова (first + last visible) | Код review |
| Prefix sum rebuild | Только при мутации данных, не при скролле | Dirty flag check |
| Canvas rebuild | Только при входе/выходе элемента из viewport | Profiler → Canvas.SendWillRenderCanvases |

---

## Registry Safety

| Registry | Blocks Used | Safety Gate |
|----------|-------------|-------------|
| Не применимо | Не применимо | Не применимо |

> Проект — Unity UPM-пакет без веб-зависимостей, shadcn или npm-registry.

---

## Checker Sign-Off

- [ ] Dimension 1 Copywriting: PASS (Exception messages defined)
- [ ] Dimension 2 Visuals: PASS (Positioning, recycling, state contracts)
- [ ] Dimension 3 Color: N/A (Runtime component, no colors)
- [ ] Dimension 4 Typography: N/A (Runtime component, no fonts)
- [ ] Dimension 5 Spacing: PASS (Layout parameters, overscan, padding)
- [ ] Dimension 6 Registry Safety: N/A (No external registries)

**Approval:** pending
