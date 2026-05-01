# Сводка исследований проекта

**Проект:** Shtl.Mvvm -- расширение MVVM-фреймворка для Unity uGUI
**Домен:** MVVM data binding фреймворк (builder bindings, two-way binding, виртуализированный список)
**Дата исследования:** 2026-04-09
**Уверенность:** HIGH

## Резюме

Shtl.Mvvm -- это type-safe, zero-reflection MVVM-фреймворк для Unity uGUI с fluent API биндингов и пулированием объектов. Текущий милестоун требует трёх ключевых расширений: (1) рефакторинг `BindFrom<T>` в полноценный builder-паттерн с отложенной активацией, (2) двусторонние биндинги для input-компонентов uGUI и (3) виртуализированный список с recycling элементов. Все три фичи имеют хорошо документированные паттерны в индустрии (WPF/MAUI для binding modes, iOS UITableView/Android RecyclerView для виртуализации), адаптированные под специфику Unity uGUI.

Рекомендуемый подход: последовательная реализация от builder-рефакторинга (основа для всего) через two-way binding к виртуализированному списку. Ключевое архитектурное решение -- builder как `readonly struct` с immutable chain (каждый метод возвращает новый экземпляр), финализация по `.To()`. Виртуализация -- наследование от `ScrollRect` с `ILayoutStrategy` стратегией и `RecyclingPool` для View-элементов. Начинать с фиксированного размера элементов.

Главные риски: (1) бесконечный цикл обновлений в two-way binding -- решается guard-флагом `_isUpdating`; (2) потеря состояния в struct-билдере из-за copy semantics -- решается immutable builder pattern с возвратом нового экземпляра; (3) Canvas rebuild storm при виртуализации -- решается sub-canvas, ручным позиционированием без LayoutGroup и избеганием `SetActive`; (4) нарушение обратной совместимости API -- решается интеграционными тестами до начала рефакторинга.

## Ключевые находки

### Рекомендуемый стек

Проект остаётся на собственном стеке без внешних зависимостей. Все новые компоненты строятся поверх существующей архитектуры (`BindingPool`, `EventBindingContext`, `ReactiveValue`, `ReactiveList`).

**Базовые технологии:**
- **Unity 2020.3+ / C# 8.0**: минимальная поддерживаемая версия, `readonly struct` и `Span<T>` доступны
- **com.unity.ugui 1.0.0+**: `ScrollRect` для виртуализации, `UnityEvent<T>` для two-way подписок
- **ref struct или readonly struct билдер**: zero-alloc builder на стеке; рекомендация -- `readonly struct` с immutable chain для максимальной совместимости

**Отклонённые альтернативы:** UniRx (избыточная зависимость), LoopScrollRect (плохо с variable-size), reflection-based binding (нарушает type-safety), class-based builder (GC pressure).

### Ожидаемые фичи

**Обязательно (table stakes):**
- Builder-паттерн биндингов с отложенной активацией и chain-методами
- TwoWay биндинг для InputField, Toggle, Slider, Dropdown
- Виртуализированный вертикальный список с recycling и overscan-буфером
- Конвертеры значений (`WithConverter`)
- Автоматический Dispose при уничтожении GameObject (уже есть)

**Конкурентные преимущества (differentiators):**
- Fluent builder API -- уникально среди Unity MVVM фреймворков
- Встроенная виртуализация списков (ни один конкурент не объединяет MVVM + виртуализацию)
- Zero-alloc hot path через пулирование + struct builder
- Горизонтальный скролл, snap-to-item

**Отложить (v1.3+):**
- Grid-layout (значительная сложность)
- Элементы переменной высоты (требует кумулятивного кэша позиций)
- Кэширование конфигурации билдера (оптимизация)

### Архитектурный подход

Архитектура строится на трёх новых компонентах, интегрированных в существующий слой биндингов. `BindingBuilder<TSource>` заменяет `BindFrom<TSource>` как точку входа fluent API, добавляя конфигурацию (direction, converter) до финализации в `.To()`. `TwoWayEventBinding<T>` -- новый тип биндинга с guard-логикой и двусторонней подпиской. `VirtualizedList<TVM, TView>` -- MonoBehaviour на базе ScrollRect с ILayoutStrategy и RecyclingPool.

**Основные компоненты:**
1. **BindingBuilder\<TSource\>** -- readonly struct, замена BindFrom, накапливает конфигурацию (Direction, Converter), финализация в `.To()`
2. **TwoWayEventBinding\<TValue\>** -- AbstractEventBinding с подпиской на обе стороны (VM.OnChanged + View.onValueChanged), guard `_isUpdating`
3. **VirtualizedList\<TVM, TView\>** -- recycling pool + ILayoutStrategy, подписка на ReactiveList через единственный Connect
4. **ILayoutStrategy** -- стратегия позиционирования (Vertical, Horizontal, Grid), отделяет layout от recycling

### Критические подводные камни

1. **Бесконечный цикл в two-way binding** -- guard-флаг `_isUpdating` в TwoWayEventBinding, проверка равенства ДО setter. Задокументировано во всех MVVM фреймворках (Angular, Vue, WPF).
2. **Потеря состояния struct-билдера (copy semantics)** -- каждый chain-метод возвращает новый экземпляр struct (immutable builder). Тесты на цепочку `.TwoWay().WithConverter().To()`.
3. **Canvas rebuild storm при виртуализации** -- sub-canvas для содержимого списка, ручное позиционирование через `anchoredPosition`, НЕ использовать SetActive/LayoutGroup/ContentSizeFitter.
4. **Нарушение обратной совместимости API** -- интеграционные тесты до начала рефакторинга, сохранение `Bind.From(x).To(y)` без изменений.
5. **Утечка подписок при recycle** -- все биндинги через EventBindingContext, полный Dispose+Connect при переиспользовании View.

## Импликации для дорожной карты

На основе исследований предлагается 4-фазная структура.

### Фаза 1: Рефакторинг Builder-паттерна биндингов
**Обоснование:** Фундамент для TwoWay и конвертеров. Без builder-рефакторинга невозможно добавить конфигурацию между `From()` и `To()`.
**Результат:** `BindingBuilder<TSource>` с chain-методами, полная обратная совместимость.
**Фичи из FEATURES.md:** Ленивая/отложенная активация биндинга, конвертеры значений.
**Избегает:** Потеря состояния в struct (#2), нарушение обратной совместимости (#4), коллизии ключей в EventBindingContext (#9).

### Фаза 2: Two-way биндинги
**Обоснование:** Зависит от Фазы 1 (builder с `.TwoWay()` конфигурацией). Самый запрашиваемый недостающий функционал.
**Результат:** Двусторонняя привязка для InputField, Toggle, Slider, Dropdown, Scrollbar.
**Фичи из FEATURES.md:** TwoWay биндинг, Two-way InputField/Slider/Toggle, OneTime режим.
**Избегает:** Бесконечный цикл обновлений (#1), неполный Dispose при пулировании (#8), утечка прямых подписок (#6).

### Фаза 3: Виртуализированный список (базовый)
**Обоснование:** Независим от Фаз 1-2 технически, но логически идёт после стабилизации API биндингов. Высокая сложность -- требует полный фокус.
**Результат:** VirtualizedList с вертикальным скроллом, фиксированным размером элементов, recycling pool, overscan-буфер.
**Фичи из FEATURES.md:** Виртуализированный вертикальный список, recycling элементов, overscan-буфер.
**Избегает:** Canvas rebuild storm (#3), single-subscriber ReactiveList (#5), velocity артефакты (#11).

### Фаза 4: Расширения виртуализации
**Обоснование:** Параметрическое расширение базового списка. Низкий риск при наличии стабильной Фазы 3.
**Результат:** Горизонтальный скролл, snap-to-item, zero-alloc оптимизации.
**Фичи из FEATURES.md:** Горизонтальный скролл, snap-to-item, zero-alloc hot path.
**Избегает:** Variable-size сложность (#7) -- оставить на v1.3+.

### Обоснование порядка фаз

- **Фаза 1 первая** -- все остальные компоненты зависят от стабильного builder API. Рефакторинг затрагивает публичный API, поэтому должен быть завершён и протестирован до добавления новых типов биндингов.
- **Фаза 2 перед Фазой 3** -- two-way binding проще в реализации и быстрее даёт пользу. Позволяет валидировать builder API на реальном use case до начала сложной виртуализации.
- **Фаза 3 отдельно** -- виртуализация -- самый сложный компонент, требует отдельной итерации с профилированием.
- **Фаза 4 последняя** -- расширения можно добавлять инкрементально, они не блокируют релиз.

### Флаги исследований

Фазы, требующие углублённого исследования при планировании:
- **Фаза 1:** исследовать поведение `using` для ref struct в Unity 2020.3; проверить совместимость implicit conversion при переименовании BindFrom
- **Фаза 3:** исследовать оптимальный размер overscan-буфера; проверить взаимодействие recycling с `AbstractWidgetView.Connect/Dispose` lifecycle

Фазы с устоявшимися паттернами (можно пропустить research-phase):
- **Фаза 2:** two-way binding -- хорошо задокументированный паттерн (WPF, MAUI, Angular), guard flag -- стандартное решение
- **Фаза 4:** горизонтальный layout -- параметризация вертикального, snap-to-item -- стандартный компонент

## Оценка уверенности

| Область | Уверенность | Примечания |
|---------|-------------|------------|
| Стек | HIGH | Собственная кодовая база, устоявшиеся паттерны Unity/C#, нет внешних зависимостей |
| Фичи | MEDIUM-HIGH | Анализ конкурентов (Loxodon, UnityMvvmToolkit, Aspid.MVVM), стандарты MVVM (WPF/MAUI) |
| Архитектура | HIGH | Текущий исходный код проанализирован, паттерны подтверждены множеством open-source реализаций |
| Подводные камни | HIGH | Задокументированы в issue-трекерах Angular, Svelte, Polymer, LoopScrollRect и Unity Forum |

**Общая уверенность:** HIGH

### Пробелы для проработки

- **ref struct Dispose в Unity 2020.3:** необходимо проверить поддержку `using` для ref struct в конкретной версии компилятора Roslyn, которую использует Unity 2020.3. Fallback -- readonly struct с immutable chain.
- **Билдер: class vs struct:** STACK.md рекомендует ref struct, PITFALLS.md предупреждает о copy semantics и рекомендует class. Решение: начать с readonly struct + immutable chain, перейти на class из пула если возникнут проблемы.
- **EventBindingContext -- коллизии ключей:** текущий Dictionary с object-ключом не поддерживает множественные биндинги от одного source. Нужно решить в Фазе 1.
- **ReactiveList single-subscriber:** виртуализированный список должен заменять ElementCollectionBinding целиком (один подписчик), а не добавлять второй. Проверить при реализации Фазы 3.
- **Размер overscan-буфера:** оптимальное значение (2-4 элемента) нужно определить эмпирически через профилирование на мобильных устройствах.

## Источники

### Первичные (HIGH confidence)
- Исходный код Shtl.Mvvm (Runtime/Core/, Runtime/Utils/) -- текущая архитектура, ограничения API
- [Unity UI Optimization Guide](https://learn.unity.com/tutorial/optimizing-unity-ui) -- рекомендации по Canvas rebuild, layout
- [.NET MAUI Binding Modes](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/data-binding/binding-mode) -- стандарт режимов привязки

### Вторичные (MEDIUM confidence)
- [LoopScrollRect](https://github.com/qiankanglai/LoopScrollRect) -- паттерн recycling ScrollRect
- [RecyclableScrollRect](https://github.com/MdIqubal/Recyclable-Scroll-Rect) -- grid-поддержка, DataSource паттерн
- [UnityMvvmToolkit (LibraStack)](https://github.com/LibraStack/UnityMvvmToolkit) -- two-way binding в Unity MVVM
- [Loxodon Framework](https://github.com/vovgou/loxodon-framework) -- зрелый Unity MVVM с data binding
- [Unity-Weld](https://github.com/Real-Serious-Games/Unity-Weld) -- two-way binding через Inspector
- [MRTK VirtualizedScrollRectList](https://learn.microsoft.com/en-us/dotnet/api/mixedreality.toolkit.ux.experimental.virtualizedscrollrectlist) -- Microsoft-реализация виртуализации

### Третичные (LOW confidence)
- [Unity Forum: ScrollRect Performance](https://forum.unity.com/threads/can-the-scrollrect-performance-issue-even-be-fixed.297258/) -- проблемы производительности ScrollRect
- [Unity 6 Data Binding](https://docs.unity3d.com/6000.3/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/data-binding.html) -- UIToolkit подход (не uGUI, ограниченная применимость)

---
*Исследование завершено: 2026-04-09*
*Готово для дорожной карты: да*
