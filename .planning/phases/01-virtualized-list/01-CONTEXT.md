# Phase 1: Виртуализированный список - Context

**Gathered:** 2026-04-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Разработчик может отображать большие коллекции данных (1000+ элементов) с плавным скроллом и минимальным расходом памяти. Виртуализированный список с recycling элементов, viewport culling, overscan-буфером, поддержкой переменных размеров и интеграцией с ReactiveList.

</domain>

<decisions>
## Implementation Decisions

### API использования
- **D-01:** Виртуализированный список подключается через единый fluent API метод `.To()` — без отдельного `.ToVirtualized()`. Консистентно с существующим API биндингов.
- **D-02:** На стороне ViewModel создаётся новый реактивный тип (аналог ReactiveList), который содержит данные коллекции + viewport state (scroll position, visible range). Этот тип — параметр для `Bind.From()`.
- **D-03:** Вставка/удаление элементов — через существующие события ReactiveList (add/remove/replace/contentChanged).
- **D-04:** Анимации элементов — возможно через существующий паттерн ReactiveAwaitable.
- **D-05:** Выделение элементов (selection) — НЕ входит в виртуализированный список, отдельная ответственность.

### Размеры элементов
- **D-06:** Переменная высота элементов задаётся через callback-функцию `Func<int, float>`. Для фиксированной высоты — константа. Zero-alloc, предсказуемо, позволяет кэшировать позиции.

### Интеграция со скроллом
- **D-07:** Кастомный скролл-механизм, не зависящий от Unity ScrollRect. Мотивация — независимость от ограничений ScrollRect. Инерция, elastic bounce, scrollbar реализуются самостоятельно.

### ReactiveList подключение
- **D-08:** Виртуализация работает внутри биндинга — занимает единственный слот `Connect()` на ReactiveList (аналогично ElementCollectionBinding). Widget работает с ReactiveList напрямую (Add/Remove/Clear), не подписываясь на события.
- **D-09:** Новый ViewModel-тип (ReactiveVirtualList<T>) содержит ReactiveList<T> внутри. Биндинг подключается к ReactiveList через Connect(). Viewport state (scroll position, visible range) живёт в отдельных ReactiveValue полях и синхронизируется биндингом в обе стороны.

### Claude's Discretion
- Внутренняя архитектура recycling pool (переиспользование View-элементов)
- Алгоритм viewport culling и расчёта видимых элементов
- Размер overscan-буфера (может быть параметром)
- Реализация инерции и elastic bounce в кастомном скролле
- Стратегия кэширования позиций элементов для переменных размеров

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Существующая система биндингов
- `Runtime/Core/Bindings/ElementCollectionBinding.cs` — текущая реализация списка без виртуализации, паттерн для нового биндинга
- `Runtime/Core/Bindings/AbstractEventBinding.cs` — базовый класс для биндингов с lifecycle и pooling
- `Runtime/Core/Bindings/BindingPool.cs` — пул переиспользования биндингов
- `Runtime/Core/Bindings/EventBindingContext.cs` — контейнер привязок с CleanUp()
- `Runtime/Core/Bindings/BindFrom.cs` — entry point fluent API

### Реактивные типы
- `Runtime/Core/Types/ReactiveList.cs` — single-subscriber reactive list, события add/replace/remove/contentChanged
- `Runtime/Core/Types/ReactiveValue.cs` — single-subscriber reactive value
- `Runtime/Core/Types/ReactiveAwaitable.cs` — async/await паттерн для анимаций
- `Runtime/Core/Types/AbstractViewModel.cs` — базовый класс ViewModel с автосбором IReactiveValue полей

### View система
- `Runtime/Core/AbstractWidgetView.cs` — базовый MonoBehaviour для View, lifecycle Connect/Dispose
- `Runtime/Core/Interfaces/IWidgetViewFactory.cs` — фабрика для создания/удаления View элементов

### Extension-методы
- `Runtime/Utils/ViewModelToUIEventBindExtensions.cs` — примеры extension-методов для `.To()`

### Архитектурная документация
- `.planning/codebase/ARCHITECTURE.md` — обзор слоёв и потока данных
- `.planning/codebase/CONVENTIONS.md` — стиль кода и именование

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ElementCollectionBinding<TVM, TView>` — паттерн подключения к ReactiveList через Connect(), создание/удаление View. Архитектурный шаблон для нового виртуализированного биндинга.
- `IWidgetViewFactory<TVM, TView>` — фабрика создания View, переиспользуется для recycling pool.
- `BindingPool` — статический пул для переиспользования объектов биндингов (Stack<TBinding> per type).
- `AbstractEventBinding<T>` — generic self-referencing base с Activate/Invoke/Dispose + pooling.

### Established Patterns
- Fluent API: `Bind.From(source).To(target)` через extension-методы в `Runtime/Utils/`
- Single-subscriber: ReactiveValue и ReactiveList бросают при повторном Connect()
- Lifecycle: Activate → Invoke → Dispose, с возвратом в BindingPool
- `[MethodImpl(AggressiveInlining)]` на hot path методах (AddInternal, RemoveAtInternal)

### Integration Points
- Новый extension-метод `.To()` для виртуализированного списка в `Runtime/Utils/`
- Новый реактивный тип `ReactiveVirtualList<T>` в `Runtime/Core/Types/`
- Новый биндинг в `Runtime/Core/Bindings/`
- Регистрация нового типа в `AbstractViewModel` автосборе IReactiveValue полей

</code_context>

<specifics>
## Specific Ideas

- ViewModel-тип по аналогии с ReactiveList — пользователь упомянул "более сложный параметр вью модели". ReactiveVirtualList<T> должен быть самодостаточным типом с коллекцией + viewport state.
- Use case анализ необходим на этапе исследования: программный скролл к элементу, определение видимых элементов, вставка/удаление с сохранением позиции, анимации элементов.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 01-virtualized-list*
*Context gathered: 2026-04-09*
