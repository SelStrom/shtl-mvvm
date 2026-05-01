---
quick_id: 260502-0nt
description: "В примере для вертикального списка настроить сообщения произвольной высоты — высоту задавать через random"
status: complete
date: 2026-05-02
commit: 8e9ddf7
---

# Summary — Quick 260502-0nt

## Что сделано

Vertical-список в `VirtualListEntryScreen` переключён с fixed-height (80f) на variable-height с random'ом 50..150 px. Horizontal-список оставлен как был (fixed 208f) — чтобы в одной сцене были обе витрины.

Высота генерируется один раз при добавлении сообщения и кэшируется по индексу в `VirtualListSampleWidget._verticalHeights` — так требует контракт `ReactiveVirtualList`: один и тот же индекс должен возвращать одну и ту же высоту, иначе биндинг будет «прыгать» при каждой перерисовке (PositionView вызывает GetItemHeight каждый кадр для видимых элементов).

## Изменения по файлам

### `Samples~/Sample/Assets/Scripts/View/ChatMessagesWidgetView.cs`
- `using System;` добавлен (для `Func<int, float>`).
- Добавлен третий конструктор `ChatMessagesViewModel(Func<int, float> heightProvider)`. Существующие `()` и `(float fixedHeight)` сохранены — W-05 контракт цел.

### `Samples~/Sample/Assets/Scripts/VirtualListSampleWidget.cs`
- Добавлены `using System.Collections.Generic;` и `using UnityEngine;` (для `Random.Range`).
- Константы `MinHeight = 50f`, `MaxHeight = 150f`.
- Поле `private readonly List<float> _verticalHeights = new();`.
- Публичный метод `public float GetVerticalHeight(int index) => _verticalHeights[index];` — exposed как method group для height-provider injection.
- В `OnMessageAdded` ПЕРВОЙ строкой `_verticalHeights.Add(Random.Range(MinHeight, MaxHeight));`, потом уже `Messages.Add(...)`. Порядок важен: биндинг при `Add` тут же запросит `GetItemHeight(newIndex)`.

### `Samples~/Sample/Assets/Scripts/VirtualListEntryScreen.cs`
- Добавлено поле `private VirtualListSampleWidget _widget;`.
- В `Start()` widget теперь создаётся ДО ViewModel-ей: `_widget = new VirtualListSampleWidget();`.
- `_verticalMessages = new ChatMessagesViewModel(_widget.GetVerticalHeight);` — method group вместо лямбды (без аллокации замыкания).
- `_horizontalMessages = new ChatMessagesViewModel(208f);` — без изменений.
- `_widget.Connect(...)` вместо anonymous `new VirtualListSampleWidget().Connect(...)`.

## Чего НЕ трогали
- `Runtime/` — никаких изменений во фреймворке (variable-height уже был реализован на стороне `ReactiveVirtualList` + `VirtualCollectionBinding`).
- `ChatMessageView`, `ChatMessageViewModel`, `ChatModel`, `ChatMessageModel` — модель сообщения не знает о высоте, это чистый view-concern в widget'е.
- Horizontal-список — остался fixed 208f, чтобы в сцене было видно оба режима.

## Контракт height-provider'а
- Стабильность: один и тот же индекс возвращает одно и то же значение во всех вызовах `GetItemHeight(index)` (значения кэшируются в `_verticalHeights`).
- Жизненный цикл: индексы только растут (в Sample есть `AddMessage` / `AddBatch(100)`, но нет remove). Если remove добавится — нужно держать `_verticalHeights.RemoveAt(index)` синхронным с `Messages.RemoveAt(index)` — вне scope этой quick-таски.

## Verification
- `grep "Func<int, float>" Samples~/Sample/Assets/Scripts/View/ChatMessagesWidgetView.cs` — есть.
- `grep "_verticalHeights" Samples~/Sample/Assets/Scripts/VirtualListSampleWidget.cs` — три места (поле, push, чтение).
- `grep "GetVerticalHeight" Samples~/Sample/Assets/Scripts/VirtualListEntryScreen.cs` — передача в ctor.
- `grep "new ChatMessagesViewModel(80f)" Samples~/Sample/Assets/Scripts/VirtualListEntryScreen.cs` — пусто (старая fixed-конструкция убрана).
- `git status` показал только три ожидаемых sample-файла, ничего лишнего.

## Manual UAT (в Unity Editor)
1. Открыть scene с `VirtualListEntryScreen`, нажать Play.
2. Vertical-список должен показывать 3 seed-сообщения с заметно разной высотой.
3. Нажать Add Batch (100 сообщений) — vertical-список заполняется элементами разной высоты, скролл должен оставаться плавным.
4. Прокрутить vertical-список вверх-вниз несколько раз — высоты конкретных индексов не должны меняться между кадрами.
5. Horizontal-список остаётся равномерным (fixed 208f).
