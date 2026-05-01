# Stack Research

**Domain:** MVVM-фреймворк для Unity uGUI -- расширение (builder bindings, two-way binding, virtualized list)
**Researched:** 2026-04-09
**Confidence:** HIGH (собственная кодовая база + устоявшиеся паттерны Unity/C#)

## Рекомендуемый стек

### Базовые технологии (без изменений)

| Технология | Версия | Назначение | Почему |
|------------|--------|------------|--------|
| Unity | 2020.3+ | Целевая платформа | Ограничение проекта; LTS, широкая база пользователей |
| com.unity.ugui | 1.0.0+ | UI-система | Ограничение проекта; UIToolkit вне scope |
| C# | 8.0 (Unity 2020.3) / 9.0 (Unity 2021.2+) | Язык | readonly struct, pattern matching, nullable refs доступны в C# 8.0 |

### Подходы по фичам

#### 1. Builder-паттерн биндингов (структура-билдер с отложенным созданием)

| Компонент | Подход | Почему |
|-----------|--------|--------|
| `BindFrom<T>` -> `BindBuilder<T>` | `ref struct` билдер вместо `readonly struct` | `ref struct` живет только на стеке, гарантирует zero-alloc и невозможность утечки ссылки. Принуждает к финализации в том же scope. **Confidence: HIGH** |
| Отложенная активация | Билдер накапливает конфигурацию (direction, converter, validator), вызывает `Activate()` только при финализации | Позволяет добавить `.TwoWay()`, `.WithConverter()` и т.д. до создания подписки. Стандартный паттерн fluent builder в C#. **Confidence: HIGH** |
| Кэширование собранных биндингов | Ключ = (sourceType, targetType, direction) -> переиспользование из `BindingPool` | Уже есть `BindingPool` с пулированием по типу. Расширить ключ кэша парой типов. **Confidence: HIGH** |
| Финализация билдера | Неявная через следующий `Bind.From()` или `Dispose()` ref struct | `ref struct` имеет `Dispose()` вызываемый компилятором при выходе из scope (C# 8.0+). Либо явный `.Build()`. Рекомендация: **явный `.Build()` для ясности** + компиляторный `Dispose()` как safety net. **Confidence: MEDIUM** -- нужно проверить поддержку `using` для ref struct в Unity 2020.3 |

**Ключевое решение: `ref struct` vs `readonly struct`**

`ref struct` -- предпочтительный вариант:
- Гарантирует stack-only аллокацию (zero GC pressure)
- Невозможно сохранить в поле класса (предотвращает баги)
- Поддерживает `Dispose()` паттерн через `using`
- **Ограничение**: нельзя использовать в async/await, нельзя боксить, нельзя захватывать в лямбды

Fallback на `readonly struct` если:
- Нужна передача билдера в лямбды (маловероятно для binding API)
- Unity 2020.3 имеет проблемы с ref struct Dispose (проверить при имплементации)

#### 2. Two-way data binding

| Компонент | Подход | Почему |
|-----------|--------|--------|
| Направление связи | Enum `BindingDirection { OneWay, TwoWay, OneWayToSource }` в билдере | Стандартный подход WPF/MAUI/Avalonia. Три направления покрывают все сценарии. **Confidence: HIGH** |
| View -> ViewModel канал | Подписка на `UnityEvent` uGUI-компонентов (`onValueChanged`) | uGUI-компоненты (InputField, Toggle, Slider, Dropdown) уже имеют `onValueChanged` события. Не нужны внешние зависимости. **Confidence: HIGH** |
| Guard от бесконечного цикла | Флаг `_isUpdating` в биндинге | Стандартный паттерн: при получении уведомления от View ставим флаг, обновляем ViewModel, ViewModel нотифицирует обратно, биндинг видит флаг и не обновляет View повторно. **Confidence: HIGH** |
| Конвертеры | `Func<TSource, TTarget>` + `Func<TTarget, TSource>` пара | Для two-way нужен обратный конвертер. Передавать как пару функций через `.WithConverter(forward, back)`. **Confidence: HIGH** |
| API в билдере | `.TwoWay()` chain-метод | `Bind.From(vm.Name).To(inputField).TwoWay().Build()` -- читается естественно, не ломает существующий API. **Confidence: HIGH** |

**Поддерживаемые uGUI-компоненты для TwoWay:**

| uGUI-компонент | Тип данных | UnityEvent |
|----------------|------------|------------|
| `InputField` | `string` | `onValueChanged`, `onEndEdit` |
| `Toggle` | `bool` | `onValueChanged` |
| `Slider` | `float` | `onValueChanged` |
| `Dropdown` | `int` | `onValueChanged` |
| `ScrollBar` | `float` | `onValueChanged` |

#### 3. Виртуализированный список

| Компонент | Подход | Почему |
|-----------|--------|--------|
| Базовый компонент | Наследование от `ScrollRect` (uGUI) | Все зрелые решения (LoopScrollRect, RecyclableScrollRect, MRTK VirtualizedScrollRectList) наследуют ScrollRect. Это дает бесплатную инерцию, эластичность, скроллбар. **Confidence: HIGH** |
| Recycling стратегия | Пул видимых элементов + перемещение за viewport | Создаем N+2 элементов (N видимых + буфер сверху/снизу). При скролле перемещаем крайний элемент на противоположный конец. **Confidence: HIGH** |
| Произвольные размеры | Кэш размеров + prefix-sum массив для быстрого lookup позиции | Для i-го элемента позиция = sum(heights[0..i-1]). Обновляем при изменении данных. Позволяет O(log n) binary search для определения видимого диапазона. **Confidence: HIGH** |
| Viewport culling | `OnScroll` callback + расчет видимого диапазона по `scrollPosition` | Не полагаемся на `RectMask2D` для логики -- только для визуального клиппинга. Логика видимости через математику позиций. **Confidence: HIGH** |
| Layout | Вертикальный, горизонтальный, grid через стратегию | `IVirtualLayoutStrategy` интерфейс с реализациями `VerticalLayout`, `HorizontalLayout`, `GridLayout`. **Confidence: MEDIUM** -- grid существенно сложнее, может быть отложен |
| Интеграция с ReactiveList | Слушает те же события (add/replace/remove/contentChanged) | `ReactiveList<T>` уже имеет нужные callback'и. `VirtualizedList` подключается как новый тип биндинга. **Confidence: HIGH** |
| Factory элементов | Использовать существующий `IWidgetViewFactory` | Уже есть паттерн создания/удаления View элементов. Добавить к нему recycling (возврат в пул вместо Destroy). **Confidence: HIGH** |

## Альтернативы, рассмотренные и отклоненные

| Категория | Рекомендация | Альтернатива | Почему НЕ альтернатива |
|-----------|-------------|-------------|----------------------|
| Реактивность | Собственная реализация (ObservableValue/ReactiveValue) | UniRx | Проект уже имеет рабочую реактивную систему. Добавление UniRx -- лишняя зависимость в 50K+ LOC для фреймворка, где нужно 5% функциональности UniRx. |
| Виртуализированный список | Собственная реализация поверх ScrollRect | LoopScrollRect (сторонний пакет) | LoopScrollRect плохо работает с variable-size элементами (документация подтверждает). Собственная реализация лучше интегрируется с ReactiveList и BindingPool. |
| Виртуализированный список | Собственная реализация поверх ScrollRect | RecyclableScrollRect (Asset Store) | Платный asset, нельзя включить в open-source UPM-пакет. |
| Two-way binding | Через билдер API | Unity-Weld (reflection-based) | Unity-Weld использует reflection + string-based binding. Shtl.Mvvm намеренно type-safe и zero-reflection в hot path. |
| Builder pattern | `ref struct` билдер | Классический класс-билдер с `.Build()` | Класс = аллокация на куче при каждом Bind. ref struct = zero-alloc. Для UI-фреймворка, где биндингов сотни, это критично. |
| Builder pattern | Явный `.Build()` | Implicit operator / финализация по следующему `From()` | Implicit финализация через следующий `From()` требует хранения состояния на уровне контекста, усложняет код и создает неочевидное поведение. Явный `.Build()` проще для понимания. |

## Чего НЕ использовать

| Избегать | Почему | Использовать вместо |
|----------|--------|-------------------|
| UIToolkit ListView | Вне scope проекта (uGUI only). UIToolkit ListView имеет встроенную виртуализацию, но несовместим с uGUI-стеком | Собственный VirtualizedScrollView поверх uGUI ScrollRect |
| UniRx как зависимость | Огромная библиотека (50K+ LOC), проект уже имеет минималистичную реактивную систему. Тянуть UniRx ради `onValueChanged` привязок -- оверкилл | Прямая подписка на `UnityEvent.AddListener` / `RemoveListener` |
| Reflection-based binding (a la Unity-Weld) | GC-pressure от boxing, string matching. Нарушает принцип type-safety проекта | Compile-time type-safe extension methods (текущий подход) |
| `class` для билдера биндингов | Аллокация на куче при каждом `Bind.From()`. При 200+ биндингах на экран -- значительный GC pressure | `ref struct` (stack-only, zero-alloc) |
| ContentSizeFitter для виртуализированного списка | Пересчитывает layout для ВСЕХ элементов, убивает смысл виртуализации | Ручное управление размером content через prefix-sum высот |
| VerticalLayoutGroup/HorizontalLayoutGroup для виртуализированного списка | Аналогично -- Unity layout groups работают со ВСЕМИ children, O(n) на каждый rebuild | Ручное позиционирование через `anchoredPosition` |

## Паттерны по вариантам

**Если Unity 2020.3 (C# 8.0):**
- `ref struct` билдер работает, но `using` для ref struct Dispose() появился в C# 8.0 -- нужно проверить поддержку в конкретной версии компилятора Unity
- `readonly` members в struct -- поддерживаются
- Nullable reference types -- `#nullable enable` поддерживается

**Если Unity 2021.2+ (C# 9.0):**
- Все вышеперечисленное + init-only properties, record types
- Можно использовать `record struct` для ключей кэша биндингов

**Если Unity 2022.2+ (C# 10/11):**
- `ref struct` полностью поддерживает `Dispose()` pattern
- `file` scoped types для internal helpers

**Рекомендация:** таргетировать C# 8.0 API, тестировать на Unity 2020.3. Более новые фичи языка использовать через `#if UNITY_2021_2_OR_NEWER` если нужно.

## Совместимость версий

| Компонент | Совместим с | Примечания |
|-----------|-------------|------------|
| `ref struct` билдер | C# 7.2+ (Unity 2018.3+) | Базовая поддержка. `Dispose()` для ref struct -- C# 8.0+ |
| `UnityEvent.AddListener` | Unity 5.0+ | Стабильный API, не изменялся |
| `ScrollRect` наследование | Unity 4.6+ | Стабильный API, основа всех кастомных scroll-решений |
| `com.unity.ugui` 1.0.0 | Unity 2020.3+ | Зависимость проекта |
| `RectMask2D` для клиппинга | Unity 5.2+ | Стандартный подход для scroll view |

## Структура новых файлов (рекомендация)

```
Runtime/
  Core/
    Bindings/
      BindBuilder.cs          -- ref struct билдер (замена прямого вызова Activate в BindFrom)
      BindingDirection.cs     -- enum OneWay/TwoWay/OneWayToSource
      TwoWayBinding.cs        -- новый тип биндинга для двусторонней связи
      TwoWayBindingPool.cs    -- (опционально) если нужен отдельный пул
    VirtualList/
      VirtualizedScrollRect.cs    -- основной компонент, наследник ScrollRect
      IVirtualLayoutStrategy.cs   -- интерфейс стратегии layout
      VerticalLayoutStrategy.cs   -- вертикальный layout
      HorizontalLayoutStrategy.cs -- горизонтальный layout
      VirtualizedElementPool.cs   -- пул UI-элементов (обертка над IWidgetViewFactory)
  Utils/
    TwoWayBindExtensions.cs       -- extension-методы .TwoWay() для BindBuilder
    VirtualListBindExtensions.cs  -- extension-методы для привязки ReactiveList к VirtualizedScrollRect
```

## Источники

- [LoopScrollRect](https://github.com/qiankanglai/LoopScrollRect) -- reference implementation для recycling ScrollRect, подтверждает паттерн наследования от ScrollRect. Ограничения с variable-size элементами.
- [RecyclableScrollRect](https://github.com/MdIqubal/Recyclable-Scroll-Rect) -- альтернативная реализация с grid-поддержкой
- [MRTK VirtualizedScrollRectList](https://learn.microsoft.com/en-us/dotnet/api/mixedreality.toolkit.ux.experimental.virtualizedscrollrectlist) -- Microsoft-реализация, подтверждает архитектурный паттерн
- [Unity-Weld](https://github.com/Real-Serious-Games/Unity-Weld) -- reference для TwoWayPropertyBinding паттерна (но reflection-based, не наш подход)
- [UnityMvvmToolkit](https://github.com/LibraStack/UnityMvvmToolkit) -- современный Unity MVVM, подтверждает востребованность type-safe binding
- [Unity UI Optimization Guide](https://learn.unity.com/tutorial/optimizing-unity-ui) -- официальное руководство по оптимизации UI, подтверждает необходимость ручного управления layout в scroll views
- Кодовая база Shtl.Mvvm -- анализ текущей архитектуры `BindFrom<T>`, `BindingPool`, `EventBindingContext`, `ElementCollectionBinding`

---
*Stack research для: Shtl.Mvvm milestone -- builder bindings, two-way binding, virtualized list*
*Researched: 2026-04-09*
