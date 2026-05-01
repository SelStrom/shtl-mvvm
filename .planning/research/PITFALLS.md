# Подводные камни

**Домен:** MVVM-фреймворк для Unity (uGUI) -- рефакторинг биндингов, two-way binding, виртуализированные списки
**Исследовано:** 2026-04-09

## Критические подводные камни

Ошибки, которые приводят к переписыванию или серьёзным проблемам.

### 1. Бесконечный цикл обновлений в two-way binding

**Что ломается:** View обновляет ViewModel, ViewModel уведомляет View, View снова обновляет ViewModel -- бесконечная рекурсия, стек-оверфлоу или зависание Unity.

**Почему возникает:** Текущий `ReactiveValue<T>.Value` setter вызывает `_onChanged` при любом изменении значения. Если View записывает значение в ViewModel, а ViewModel уведомляет View, и View снова записывает (например, после форматирования текста), цикл замыкается. В `ObservableValue<T>` есть проверка `EqualityComparer`, но floating-point конверсии или string-форматирование могут давать разные значения на каждом витке.

**Последствия:** Crash приложения, freeze главного потока Unity. Проблема проявляется не сразу -- только когда конвертер возвращает значение, отличающееся от исходного.

**Предотвращение:**
- Ввести флаг `_isUpdating` (guard flag) в механизм two-way binding, блокирующий обратное уведомление во время обновления
- Проверку равенства делать ДО вызова setter, а не внутри него -- чтобы конвертеры не создавали ложных различий
- Альтернатива: использовать "batch" подход -- two-way binding подавляет обратное событие на один tick

**Признаки раннего обнаружения:** Unity перестаёт отвечать при взаимодействии с InputField; стек-трейс показывает рекурсию через OnValueChanged -> setter -> OnChanged -> OnValueChanged.

**Фаза:** Two-way binding (должен быть решён до первой реализации двусторонней связи).

**Уверенность:** HIGH -- это задокументированная проблема во всех фреймворках с two-way binding (Angular, Vue, WPF, Android Data Binding).

---

### 2. Потеря состояния в билдере-структуре из-за copy semantics

**Что ломается:** Текущий `BindFrom<TSource>` -- это `readonly struct`. Если билдер-рефакторинг добавит мутабельное состояние (флаги конфигурации, кэш) в структуру, каждый вызов в fluent-цепочке может работать с копией, а не с оригиналом. Настройки "исчезают".

**Почему возникает:** В C# struct передаётся по значению. Код вида:
```csharp
var builder = Bind.From(source);
builder.TwoWay();  // мутирует копию, оригинал не изменён
builder.To(target); // создаёт binding без TwoWay
```
Если методы возвращают `void` вместо `ref` или нового экземпляра -- состояние теряется. Хуже того, `readonly struct` при вызове не-readonly метода создаёт скрытую defensive copy.

**Последствия:** Биндинги создаются без запрошенной конфигурации. Баг молчаливый -- нет исключений, просто неправильное поведение.

**Предотвращение:**
- **Вариант A (рекомендуемый):** Сделать билдер классом, а не структурой. Пулировать через `BindingPool` для zero-alloc. Fluent chain всегда работает с одним экземпляром
- **Вариант B:** Оставить struct, но каждый метод в цепочке возвращает новый экземпляр (immutable builder pattern). Недостаток: больше копирований
- **Вариант C:** Использовать `ref struct` -- но это запрещает хранение в полях и async, что слишком ограничительно
- В любом случае: текущий `readonly struct BindFrom<T>` менять аккуратно. Он не хранит мутабельное состояние сейчас, но рефакторинг может это изменить

**Признаки раннего обнаружения:** Тесты на цепочку `.TwoWay().WithConverter().To()` должны проверять, что все флаги дошли до финального binding.

**Фаза:** Рефакторинг биндингов (первая фаза, до two-way).

**Уверенность:** HIGH -- это фундаментальное свойство value types в C#.

---

### 3. Canvas rebuild storm при виртуализации списка

**Что ломается:** При скроллинге виртуализированного списка каждый переиспользованный элемент запускает пересчёт Canvas layout. При 10+ элементах, обновляемых за один кадр, uGUI перестраивает весь Canvas, вызывая фризы.

**Почему возникает:** uGUI Canvas rebuild срабатывает при любом изменении RectTransform (position, size) или при SetActive(true/false). Наивная реализация виртуализации двигает элементы и включает/выключает их на каждом кадре скроллинга, что генерирует десятки rebuild-ов.

**Последствия:** Падение FPS с 60 до 15-20 на мобильных устройствах. Виртуализация, которая должна была ускорить список, делает его медленнее наивного подхода при малом количестве элементов.

**Предотвращение:**
- Не использовать `SetActive(false)` для скрытия -- вместо этого двигать элемент за пределы viewport или использовать `CanvasGroup.alpha = 0` с `CanvasGroup.blocksRaycasts = false`
- Группировать обновления позиций в один кадр: менять все RectTransform за один проход, а не по одному
- Использовать отдельный Canvas для содержимого списка (sub-canvas), чтобы rebuild не распространялся на весь UI
- Не использовать ContentSizeFitter и LayoutGroup на дочерних элементах виртуализированного списка -- вычислять размеры вручную
- Кэшировать высоты элементов, не пересчитывать при каждом recycle

**Признаки раннего обнаружения:** Profiler показывает `Canvas.SendWillRenderCanvases` > 2ms при скроллинге; частые вызовы `LayoutRebuilder.MarkLayoutForRebuild`.

**Фаза:** Виртуализированный список.

**Уверенность:** HIGH -- задокументировано в Unity Performance Guidelines и подтверждено множеством open-source решений (LoopScrollRect, RecyclableScrollRect).

---

### 4. Нарушение обратной совместимости API

**Что ломается:** Существующий `Bind.From(x).To(y)` перестаёт компилироваться или меняет поведение после рефакторинга.

**Почему возникает:** Проект явно требует обратной совместимости (`Bind.From().To()` должен работать как раньше). При добавлении промежуточного билдера с ленивым созданием, метод `To()` может стать extension-методом на другом типе, или `LinkTo()` может вызываться в другой момент. Если `BindFrom<T>` перестанет быть `readonly struct` -- изменится семантика передачи по значению.

**Последствия:** Все существующие View-классы, использующие `Bind.From().To()`, ломаются. Необходим рефакторинг всех потребителей.

**Предотвращение:**
- Написать интеграционные тесты на все текущие паттерны использования ДО начала рефакторинга
- Сохранить `BindFrom<T>` как точку входа, добавив новые методы (`.TwoWay()`, `.WithConverter()`) как extension-методы, возвращающие новый тип билдера
- `To()` должен оставаться терминальным методом -- и на старом `BindFrom<T>`, и на новом билдере
- Использовать implicit conversion или overload resolution для бесшовного перехода

**Признаки раннего обнаружения:** Компиляция Sample проекта из `Samples~` после каждого изменения API. Любая ошибка компиляции -- сигнал.

**Фаза:** Рефакторинг биндингов (самое начало).

**Уверенность:** HIGH -- требование зафиксировано в PROJECT.md.

---

## Умеренные подводные камни

### 5. ReactiveList single-subscriber ограничение при виртуализации

**Что ломается:** `ReactiveList<T>.Connect()` выбрасывает `InvalidOperationException("Already bound")` при попытке подключить второго подписчика. Виртуализированный список может потребовать отдельную подписку для управления viewport (определение видимых индексов) в дополнение к существующему `ElementCollectionBinding`.

**Почему возникает:** Текущая архитектура `ReactiveList` допускает ровно одного подписчика (`_isBound` flag). Это работает для простых списков, но виртуализация требует реагировать на изменения коллекции из нескольких мест: рассчёт позиций, recycle pool, viewport culling.

**Предотвращение:**
- Вариант A: Перейти на multi-subscriber модель (event/delegate вместо single action). Но это ломает гарантию "один биндинг -- один подписчик"
- Вариант B (рекомендуемый): Виртуализированный список заменяет `ElementCollectionBinding` целиком -- один подписчик управляет и данными, и viewport. Не нужно два подписчика
- Вариант C: Создать `VirtualizedReactiveList<T>`, наследующий `ReactiveList<T>` с расширенной логикой подписок

**Признаки раннего обнаружения:** `InvalidOperationException` при попытке подключить виртуализацию к `ReactiveList`, на который уже подписан `ElementCollectionBinding`.

**Фаза:** Виртуализированный список.

**Уверенность:** HIGH -- ограничение явно видно в исходном коде `ReactiveList.Connect()`.

---

### 6. Утечка подписок при recycle элементов виртуализированного списка

**Что ломается:** При переиспользовании View-элемента в виртуализированном списке старые биндинги не отписываются, или отписка происходит некорректно. Со временем один ObservableValue накапливает сотни подписчиков.

**Почему возникает:** Текущий `AbstractWidgetView.Connect()` вызывает `_bindingContext?.CleanUp()` и `ViewModel?.Dispose()` перед подключением нового ViewModel. Но при recycle в виртуализированном списке логика может отличаться: View не уничтожается (`Dispose`), а переподключается к другому ViewModel. Если `Connect` не полностью очищает старые подписки (например, `ReactiveValue.Connect` через прямой callback в `ViewModelToUIEventBindingsExtensions.To()` для TMP_Text) -- подписка утекает.

**Последствия:** Memory leak, нарастающее замедление, обновление UI элементов, которые уже не видны.

**Предотвращение:**
- Все биндинги виртуализированных элементов должны проходить через `EventBindingContext`, а не через прямые подписки (`ReactiveValue.Connect`)
- Текущие extension-методы для TMP_Text (`from.Source.Connect(value => view.text = value)`) обходят `EventBindingContext` -- это нужно исправить ДО виртуализации
- Recycle должен вызывать полный `Dispose()` + `Connect()`, а не пытаться частично обновить View

**Признаки раннего обнаружения:** Profiler показывает растущее количество подписчиков в ObservableValue/ReactiveValue; GC pressure растёт при скроллинге.

**Фаза:** Виртуализированный список (но корень проблемы -- в текущих extension-методах, которые стоит исправить при рефакторинге биндингов).

**Уверенность:** HIGH -- прямые подписки без EventBindingContext видны в текущем коде `ViewModelToUIEventBindExtensions.cs` (строки 43-59).

---

### 7. Проблемы с произвольными размерами элементов в виртуализированном списке

**Что ломается:** При элементах разной высоты невозможно точно рассчитать позицию скролла и определить, какие элементы видны, без измерения каждого элемента заранее.

**Почему возникает:** Фиксированная высота позволяет вычислить позицию элемента за O(1): `position = index * itemHeight`. Произвольная высота требует кумулятивной суммы высот всех предшествующих элементов, что: (а) требует знать высоту до рендеринга, (б) при изменении одного элемента пересчитывает позиции всех последующих.

**Предотвращение:**
- Фаза 1: реализовать только фиксированный размер элементов. Это покрывает 80% use cases (инвентари, чаты с одинаковыми ячейками, лидерборды)
- Фаза 2 (если нужно): добавить estimated height с коррекцией при рендеринге. Хранить кэш `Dictionary<int, float>` измеренных высот, для неизмеренных использовать среднее
- Не пытаться реализовать variable-size И grid layout одновременно -- это экспоненциально усложняет задачу

**Признаки раннего обнаружения:** "Прыгающий" scrollbar, элементы перекрываются или имеют щели между собой, некорректное определение visible range.

**Фаза:** Виртуализированный список (начать с фиксированных размеров).

**Уверенность:** MEDIUM -- зависит от конкретных требований проекта. Для MVP фиксированный размер достаточен.

---

### 8. Некорректный Dispose при пулировании биндингов

**Что ломается:** `BindingPool` возвращает binding в пул после `Dispose()`, но если `Dispose()` не полностью обнуляет внутреннее состояние, переиспользованный binding сохраняет ссылки на старые объекты.

**Почему возникает:** Текущий код `EventBindingContext.CleanUp()` вызывает `binding.Dispose()` и затем `BindingPool.Release(binding)`. Каждый `Dispose()` обнуляет поля. Но если новый тип binding (например, TwoWayBinding) забудет обнулить одно из полей -- утечка. Кроме того, для two-way binding нужно отписаться от событий View-стороны (например, `InputField.onValueChanged`), а не только от ViewModel-стороны.

**Предотвращение:**
- Ввести абстрактный метод `Reset()` или проверку в `AbstractEventBinding`, что все поля обнулены после `Dispose()`
- Тесты: создать binding, dispose, получить из пула, убедиться что все поля == null/default
- Для two-way binding: `Dispose()` должен отписываться от ОБЕИХ сторон (ViewModel.OnChanged и View.onValueChanged)

**Признаки раннего обнаружения:** NullReferenceException при переиспользовании binding; обновляется "чужой" UI элемент.

**Фаза:** Рефакторинг биндингов + Two-way binding.

**Уверенность:** HIGH -- паттерн object pool с неполным reset -- классическая ошибка.

---

## Незначительные подводные камни

### 9. EventBindingContext хранит binding по ключу-объекту -- коллизии ключей

**Что ломается:** `EventBindingContext._keyToBinding` использует `object` как ключ (source объект биндинга). Если один source привязывается к нескольким target-ам (например, один `ReactiveValue` к TMP_Text и к Image), второй `AddBinding` выбросит исключение "The binding has already exists".

**Предотвращение:** При рефакторинге использовать составной ключ (source + target) или перейти на `List<AbstractEventBinding>` вместо `Dictionary`.

**Фаза:** Рефакторинг биндингов.

**Уверенность:** HIGH -- видно в коде `EventBindingContext.AddBinding()`.

---

### 10. Совместимость с Unity 2020.3 при использовании новых C# фич

**Что ломается:** Unity 2020.3 поддерживает C# 8.0. Если рефакторинг использует C# 9+ фичи (records, init-only setters, pattern matching enhancements), код не скомпилируется в целевой минимальной версии Unity.

**Предотвращение:**
- Настроить CI на сборку с Unity 2020.3 LTS
- Не использовать: records, init-only, static abstract members, file-scoped namespaces (допустимы с Unity 2021.2+)
- `readonly struct` и `Span<T>` безопасны для 2020.3

**Фаза:** Все фазы.

**Уверенность:** HIGH -- ограничение зафиксировано в PROJECT.md.

---

### 11. Velocity артефакты при recycle в ScrollRect

**Что ломается:** При быстром скроллинге, когда виртуализация перемещает элементы, ScrollRect.velocity может получить некорректные значения, вызывая "выстреливание" скролла.

**Предотвращение:**
- Не двигать content RectTransform напрямую -- управлять normalizedPosition ScrollRect
- При recycle элементов корректировать `content.anchoredPosition` на величину сдвига, чтобы скомпенсировать визуальное перемещение
- Тестировать с `ScrollRect.inertia = true` на мобильных устройствах

**Фаза:** Виртуализированный список.

**Уверенность:** MEDIUM -- зависит от конкретной реализации. Задокументировано в issue-трекерах LoopScrollRect и RecyclableScrollRect.

---

## Предупреждения по фазам

| Тема фазы | Вероятный подводный камень | Смягчение |
|-----------|--------------------------|-----------|
| Рефакторинг биндингов (билдер) | Потеря состояния в struct-билдере (#2), нарушение обратной совместимости (#4), коллизии ключей в EventBindingContext (#9) | Тесты на существующий API до начала рефакторинга; билдер как class, а не struct |
| Two-way binding | Бесконечный цикл (#1), неполный Dispose при пулировании (#8), утечка прямых подписок (#6) | Guard flag в two-way механизме; все подписки через EventBindingContext |
| Виртуализированный список | Canvas rebuild storm (#3), single-subscriber ReactiveList (#5), variable-size сложность (#7), velocity артефакты (#11) | Sub-canvas, ручной layout без LayoutGroup, начать с фиксированных размеров |
| Все фазы | Совместимость с Unity 2020.3 (#10) | CI на минимальной поддерживаемой версии |

## Источники

- [Unity Forum: MVVM and DataBinding](https://forum.unity.com/threads/mvvm-and-databinding-in-unity.155604/)
- [Unity Forum: Two-Way Data Binding Issues](https://discussions.unity.com/t/two-way-data-binding-issues-to-various-input-fields/1691954)
- [HogoNext: Common Pitfalls in MVVM](https://hogonext.com/how-to-avoid-common-pitfalls-in-mvvm-implementation/)
- [GitHub: LoopScrollRect](https://github.com/qiankanglai/LoopScrollRect)
- [GitHub: UnityRecyclingListView](https://github.com/sinbad/UnityRecyclingListView)
- [GitHub: Recyclable Scroll Rect](https://github.com/MdIqubal/Recyclable-Scroll-Rect)
- [Unity Forum: ScrollRect Performance](https://forum.unity.com/threads/can-the-scrollrect-performance-issue-even-be-fixed.297258/)
- [Unity Docs: Content Size Fitter](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/script-ContentSizeFitter.html)
- [Unity Forum: Layout Rebuild Issues](https://discussions.unity.com/t/need-help-avoiding-layout-rebuild-on-ugui-objects/855085)
- [Microsoft Learn: Structure Types in C#](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/struct)
- [Angular Issue: Infinite two-way binding loop](https://github.com/angular/angular/issues/21551)
- [Svelte Issue: Infinite loop caused by two-way binding](https://github.com/sveltejs/svelte/issues/398)
- [Polymer Issue: Two-way binding infinite loop](https://github.com/Polymer/polymer/issues/3399)
- [Microsoft DevBlog: readonly structs and in modifier](https://devblogs.microsoft.com/premier-developer/the-in-modifier-and-the-readonly-structs-in-c/)
