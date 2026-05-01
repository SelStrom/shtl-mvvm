# Phase 1: Виртуализированный список - Research

**Researched:** 2026-04-09
**Domain:** Unity uGUI виртуализация списков, recycling, viewport culling, кастомный скролл
**Confidence:** HIGH

## Summary

Виртуализированный список для Unity uGUI — хорошо изученная задача с чётким набором алгоритмов: prefix sum массив для позиционирования элементов переменной высоты, binary search для определения видимого диапазона по scroll position, recycling pool для переиспользования View-элементов. Кастомный скролл-механизм (решение D-07) требует реализации инерции через экспоненциальное затухание скорости и elastic bounce через `SmoothDamp` — оба алгоритма хорошо документированы в исходниках Unity ScrollRect.

Существующая архитектура Shtl.Mvvm предоставляет готовые строительные блоки: `ElementCollectionBinding` как архитектурный шаблон, `IWidgetViewFactory` для создания/удаления View, `BindingPool` для пулирования биндингов, `ReactiveList` с событиями add/remove/replace/contentChanged. Новый `ReactiveVirtualList<T>` (решение D-09) содержит `ReactiveList<T>` внутри, а виртуализация реализуется в биндинге, подключающемся к ReactiveList через единственный слот `Connect()`.

**Primary recommendation:** Реализовать виртуализацию как новый тип биндинга `VirtualCollectionBinding<TVM, TView>`, управляющий пулом View-элементов и viewport culling. Кастомный скролл — отдельный MonoBehaviour с IBeginDragHandler/IDragHandler/IEndDragHandler/IScrollHandler. Prefix sum array + binary search для позиционирования элементов переменной высоты.

<user_constraints>

## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Виртуализированный список подключается через единый fluent API метод `.To()` — без отдельного `.ToVirtualized()`. Консистентно с существующим API биндингов.
- **D-02:** На стороне ViewModel создаётся новый реактивный тип (аналог ReactiveList), который содержит данные коллекции + viewport state (scroll position, visible range). Этот тип — параметр для `Bind.From()`.
- **D-03:** Вставка/удаление элементов — через существующие события ReactiveList (add/remove/replace/contentChanged).
- **D-04:** Анимации элементов — возможно через существующий паттерн ReactiveAwaitable.
- **D-05:** Выделение элементов (selection) — НЕ входит в виртуализированный список, отдельная ответственность.
- **D-06:** Переменная высота элементов задаётся через callback-функцию `Func<int, float>`. Для фиксированной высоты — константа. Zero-alloc, предсказуемо, позволяет кэшировать позиции.
- **D-07:** Кастомный скролл-механизм, не зависящий от Unity ScrollRect. Инерция, elastic bounce, scrollbar реализуются самостоятельно.
- **D-08:** Виртуализация работает внутри биндинга — занимает единственный слот `Connect()` на ReactiveList (аналогично ElementCollectionBinding). Widget работает с ReactiveList напрямую (Add/Remove/Clear), не подписываясь на события.
- **D-09:** Новый ViewModel-тип (ReactiveVirtualList<T>) содержит ReactiveList<T> внутри. Биндинг подключается к ReactiveList через Connect(). Viewport state (scroll position, visible range) живёт в отдельных ReactiveValue полях и синхронизируется биндингом в обе стороны.

### Claude's Discretion
- Внутренняя архитектура recycling pool (переиспользование View-элементов)
- Алгоритм viewport culling и расчёта видимых элементов
- Размер overscan-буфера (может быть параметром)
- Реализация инерции и elastic bounce в кастомном скролле
- Стратегия кэширования позиций элементов для переменных размеров

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope

</user_constraints>

<phase_requirements>

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| VLIST-01 | Виртуализированный список рендерит только видимые элементы + overscan-буфер | Viewport culling через prefix sum + binary search; overscan как параметр (рекомендация: 2-4 элемента) |
| VLIST-02 | Элементы переиспользуются через recycling pool при скролле | ViewRecyclingPool<TVM, TView> с Stack-based хранилищем, аналогично BindingPool |
| VLIST-03 | Поддержка вертикальной прокрутки | Кастомный скролл-механизм через IBeginDragHandler/IDragHandler/IEndDragHandler + LateUpdate |
| VLIST-04 | Поддержка элементов переменной высоты/ширины | Func<int, float> callback + prefix sum массив для O(log n) позиционирования |
| VLIST-05 | Интеграция с ReactiveList<T> через события add/replace/remove | VirtualCollectionBinding подключается к ReactiveList.Connect() аналогично ElementCollectionBinding |
| VLIST-06 | Корректная работа с IWidgetViewFactory для создания/удаления View | Recycling pool использует IWidgetViewFactory.CreateWidget/RemoveWidget |
| VLIST-07 | Zero-alloc в hot path скролла | Struct-based промежуточные данные, предаллоцированные буферы, без LINQ, без boxing |
| TEST-04 | Unit-тесты для recycling, viewport culling, переменные размеры | NUnit через Unity Test Framework; тесты логики без MonoBehaviour зависимостей |

</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **Язык:** Русский для всех комментариев и документации
- **Стиль:** Allman brace style, 4 пробела отступ, фигурные скобки обязательны даже для однострочных if/while/foreach
- **Совместимость:** Unity 2020.3+ — нельзя использовать API, недоступные в старых версиях
- **uGUI:** Работа с uGUI (UnityEngine.UI), не UIToolkit
- **Zero-alloc:** Минимизация аллокаций в hot path
- **Обратная совместимость API:** Существующий `Bind.From().To()` API не должен ломаться
- **Пространство имён:** `Shtl.Mvvm` — все новые типы в том же корневом пространстве
- **Именование:** PascalCase типы, `_camelCase` приватные поля, суффикс `Internal` для приватных вспомогательных методов
- **`[MethodImpl(AggressiveInlining)]`** на hot path методах
- **Single-subscriber:** ReactiveValue и ReactiveList — один Connect(), бросает при повторном
- **var предпочтительно везде**
- **Expression-bodied members** для однострочных методов/свойств

## Architecture Patterns

### Recommended Project Structure
```
Runtime/
├── Core/
│   ├── Types/
│   │   └── ReactiveVirtualList.cs       # Новый ViewModel-тип (D-09)
│   ├── Bindings/
│   │   └── VirtualCollectionBinding.cs  # Основной биндинг виртуализации
│   ├── Interfaces/
│   │   └── IVirtualScrollRect.cs        # Интерфейс для кастомного скролла
│   └── VirtualScroll/
│       ├── VirtualScrollRect.cs         # MonoBehaviour: кастомный скролл (D-07)
│       ├── ViewRecyclingPool.cs         # Пул переиспользования View-элементов
│       └── LayoutCalculator.cs          # Prefix sum + binary search для позиций
├── Utils/
│   └── ViewModelToUIEventBindExtensions.cs  # +extension .To() для ReactiveVirtualList
Tests/
├── Runtime/
│   ├── Shtl.Mvvm.Tests.asmdef
│   ├── LayoutCalculatorTests.cs
│   ├── ViewRecyclingPoolTests.cs
│   ├── ReactiveVirtualListTests.cs
│   └── VirtualCollectionBindingTests.cs
```

### Pattern 1: Prefix Sum Array + Binary Search для позиционирования
**What:** Массив кумулятивных высот элементов (`_prefixHeights[i]` = сумма высот элементов 0..i-1). Для определения видимого диапазона по scroll position — binary search по этому массиву.
**When to use:** Всегда при переменной высоте элементов. Для фиксированной высоты — упрощается до `index = scrollPosition / itemHeight` (O(1)).
**Example:**
```csharp
// [VERIFIED: алгоритм из web-исследования виртуализации списков]
// Prefix sum: _prefixHeights[0] = 0, _prefixHeights[i] = _prefixHeights[i-1] + height(i-1)
// Поиск первого видимого элемента: binary search по _prefixHeights для scrollPosition
// Поиск последнего видимого: binary search для scrollPosition + viewportHeight

// Пример: найти индекс первого элемента, видимого при scrollPosition
private int FindFirstVisibleIndex(float scrollPosition)
{
    var lo = 0;
    var hi = _itemCount;
    while (lo < hi)
    {
        var mid = lo + (hi - lo) / 2;
        if (_prefixHeights[mid + 1] <= scrollPosition)
        {
            lo = mid + 1;
        }
        else
        {
            hi = mid;
        }
    }
    return lo;
}
```

### Pattern 2: View Recycling Pool
**What:** Stack-based пул для переиспользования View-элементов (MonoBehaviour). При выходе элемента из viewport — Dispose ViewModel, деактивация GameObject, помещение в стек. При появлении нового элемента — извлечение из стека, Connect нового ViewModel, активация.
**When to use:** Всегда при виртуализации.
**Example:**
```csharp
// Аналогично BindingPool, но для View-элементов
internal class ViewRecyclingPool<TViewModel, TWidgetView>
    where TViewModel : AbstractViewModel, new()
    where TWidgetView : AbstractWidgetView<TViewModel>, new()
{
    private readonly Stack<TWidgetView> _pool = new();
    private readonly IWidgetViewFactory<TViewModel, TWidgetView> _factory;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TWidgetView Get(TViewModel viewModel)
    {
        if (_pool.TryPop(out var view))
        {
            view.gameObject.SetActive(true);
            view.Connect(viewModel);
            return view;
        }
        var newView = _factory.CreateWidget(viewModel);
        newView.Connect(viewModel);
        return newView;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Release(TWidgetView view)
    {
        view.Dispose();
        view.gameObject.SetActive(false);
        _pool.Push(view);
    }
}
```

### Pattern 3: Кастомный скролл с инерцией и elastic bounce
**What:** MonoBehaviour реализующий IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler. LateUpdate для инерции и elastic bounce.
**When to use:** Решение D-07 — обязательно для этой фазы.
**Example:**
```csharp
// [VERIFIED: алгоритмы из исходников Unity ScrollRect]
// Инерция: velocity *= Mathf.Pow(decelerationRate, deltaTime)
// Elastic: position = Mathf.SmoothDamp(position, target, ref speed, elasticity, Infinity, deltaTime)
// RubberDelta: (1 - 1 / (Abs(overStretching) * 0.55f / viewSize + 1)) * viewSize * Sign(overStretching)
```

### Pattern 4: ReactiveVirtualList<T> — ViewModel-тип
**What:** Новый реактивный тип, содержащий ReactiveList<T> и viewport state (ReactiveValue<float> ScrollPosition, ReactiveValue<Vector2Int> VisibleRange).
**When to use:** В ViewModel для объявления виртуализированного списка.
**Example:**
```csharp
// Использование в ViewModel:
public class MyViewModel : AbstractViewModel
{
    public readonly ReactiveVirtualList<ItemViewModel> Items = new();
}

// В Widget:
_viewModel.Items.Add(new ItemViewModel());
_viewModel.Items.RemoveAt(0);

// В View:
Bind.From(ViewModel.Items).To(_prefab, _scrollRect);
```

### Anti-Patterns to Avoid
- **Создание/уничтожение GameObject при каждом скролле:** Вместо Instantiate/Destroy использовать recycling pool с SetActive(true/false)
- **Пересчёт всех позиций каждый кадр:** Кэшировать prefix sum array, пересчитывать только при изменении данных
- **LINQ в hot path:** Никакого `.Where()`, `.Select()`, `.ToList()` при обработке скролла — только for-циклы и массивы
- **Boxing value types:** Не передавать int/float как object — использовать generic методы
- **Обновление всех View при каждом скролле:** Обновлять только элементы, входящие/выходящие из viewport
- **Подписка на ReactiveList из Widget:** Widget работает с ReactiveList напрямую (D-08), биндинг занимает единственный слот Connect()
- **Аллокация массивов/списков в Update/LateUpdate:** Предаллоцировать все буферы в Connect()

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Drag/Touch input | Свой InputSystem polling | `IBeginDragHandler` + `IDragHandler` + `IEndDragHandler` + `IScrollHandler` | Unity EventSystem обрабатывает touch/mouse, multi-touch, event routing через GraphicRaycaster |
| View creation | Ручной Instantiate | `IWidgetViewFactory<TVM, TView>` | Уже есть в проекте, поддерживает кастомные фабрики |
| Binding lifecycle | Ручное управление Connect/Dispose | `AbstractEventBinding<T>` + `BindingPool` | Уже есть pooling и lifecycle management |
| Smooth dampening | Свой spring physics | `Mathf.SmoothDamp` | Проверенная реализация Unity, frame-rate independent |
| Velocity smoothing | Свой фильтр | `Vector3.Lerp(velocity, newVelocity, deltaTime * 10)` | Паттерн ScrollRect, проверен на миллионах устройств |

**Key insight:** Кастомный скролл — это замена ScrollRect, но внутренняя физика (инерция, elastic bounce) должна использовать те же проверенные формулы из Unity ScrollRect. Не изобретать физику, а изолировать layout management.

## Common Pitfalls

### Pitfall 1: Гонка между скроллом и изменением данных
**What goes wrong:** Пользователь скроллит список, а Widget одновременно добавляет/удаляет элементы. Visible range рассчитывается на основе устаревших данных — IndexOutOfRange или визуальные артефакты.
**Why it happens:** ReactiveList events приходят в произвольный момент, а LateUpdate рассчитывает viewport на основе текущего scroll position.
**How to avoid:** Пересчитывать prefix sum и visible range синхронно при каждом событии ReactiveList (onElementAdded/Removed/ContentChanged). Dirty flag + пересчёт в LateUpdate до обновления View.
**Warning signs:** Мерцание элементов при быстром Add/Remove во время скролла.

### Pitfall 2: Off-by-one в overscan буфере
**What goes wrong:** Элемент на границе viewport то появляется, то исчезает (мерцание) при медленном скролле.
**Why it happens:** Overscan буфер считается неправильно — элемент входит в overscan на одном кадре и выходит на следующем.
**How to avoid:** Asymmetric hysteresis — расширять overscan при добавлении элемента на 2-4, но удалять из overscan только когда элемент вышел за overscan + 1. Или фиксированный overscan в обе стороны (рекомендация: 2 элемента).
**Warning signs:** Мерцание элементов на краях viewport при медленном скролле.

### Pitfall 3: Assert в AbstractWidgetView.Connect() при recycling
**What goes wrong:** `Assert.IsTrue(vm != ViewModel)` в `AbstractWidgetView.Connect()` срабатывает при переиспользовании View с тем же ViewModel.
**Why it happens:** При recycling View может получить тот же ViewModel, если элемент вышел из viewport и вернулся.
**How to avoid:** Перед Connect() всегда вызывать Dispose() на View (очистит ViewModel до default), либо проверять что ViewModel отличается и пропускать Connect() если совпадает.
**Warning signs:** Assert failure в runtime при быстром скролле вперёд-назад.

### Pitfall 4: GC аллокации от делегатов ReactiveList.Connect()
**What goes wrong:** Каждый вызов Connect() с lambda-выражениями создаёт новые delegate-объекты.
**Why it happens:** C# компилятор создаёт closure для lambda, если она захватывает переменные.
**How to avoid:** Использовать instance-методы биндинга как callback-и вместо lambda. Connect() вызывается один раз при Activate(), так что однократная аллокация допустима — но при каждом вызове OnElementAdded/Removed не должно быть аллокаций.
**Warning signs:** Профайлер показывает GC.Alloc в `<>c__DisplayClass` при скролле.

### Pitfall 5: Canvas rebuild при SetActive
**What goes wrong:** Вызов `SetActive(true)` на View-элементе вызывает dirty flag на Canvas, что приводит к rebuild всего Canvas при каждом скролле.
**Why it happens:** Unity Canvas пересчитывает layout при любом изменении иерархии активных элементов.
**How to avoid:** Использовать `CanvasGroup.alpha = 0` + `CanvasGroup.blocksRaycasts = false` вместо SetActive для скрытия элементов, или использовать отдельный Canvas (nested canvas) для каждого элемента, или перемещать элементы за viewport вместо деактивации. Альтернатива: RectTransform позиционирование — элемент всегда активен, но находится за пределами viewport mask.
**Warning signs:** Профайлер показывает Canvas.SendWillRenderCanvases каждый кадр при скролле.

### Pitfall 6: Unity 2020.3 совместимость
**What goes wrong:** Использование API недоступных в Unity 2020.3 (например, `Stack.TryPop` требует .NET Standard 2.1).
**Why it happens:** Проект требует совместимости с Unity 2020.3, который использует .NET Standard 2.0.
**How to avoid:** Проверять каждый API на доступность в .NET Standard 2.0. `Stack.TryPop` — `#if UNITY_2021_2_OR_NEWER` или ручная проверка `Count > 0` + `Pop()`. Впрочем, в BindingPool уже используется `Stack.TryPop()` — значит проект уже использует .NET Standard 2.1 или Unity 2021+.
**Warning signs:** Ошибки компиляции при сборке в Unity 2020.3.

## Code Examples

### Extension-метод .To() для ReactiveVirtualList (интеграция с существующим API)
```csharp
// Новый extension-метод в ViewModelToUIEventBindExtensions.cs или отдельном файле
// Паттерн полностью повторяет существующий .To() для ReactiveList
public static void To<TViewModel, TWidgetView>(
    this BindFrom<ReactiveVirtualList<TViewModel>> from,
    TWidgetView prefab,
    VirtualScrollRect scrollRect
)
    where TViewModel : AbstractViewModel, new()
    where TWidgetView : AbstractWidgetView<TViewModel>, new()
{
    var binding = VirtualCollectionBinding<TViewModel, TWidgetView>.GetOrCreate()
        .Connect(from.Source, prefab, scrollRect);
    from.LinkTo(binding);
}

// Перегрузка с IWidgetViewFactory
public static void To<TViewModel, TWidgetView>(
    this BindFrom<ReactiveVirtualList<TViewModel>> from,
    IWidgetViewFactory<TViewModel, TWidgetView> factory,
    VirtualScrollRect scrollRect
)
    where TViewModel : AbstractViewModel, new()
    where TWidgetView : AbstractWidgetView<TViewModel>, new()
{
    var binding = VirtualCollectionBinding<TViewModel, TWidgetView>.GetOrCreate()
        .Connect(from.Source, factory, scrollRect);
    from.LinkTo(binding);
}
```

### ReactiveVirtualList<T> — структура ViewModel-типа
```csharp
// Аналогично ReactiveList, но содержит viewport state
public class ReactiveVirtualList<TElement> : IReactiveValue
    where TElement : AbstractViewModel, new()
{
    // Данные коллекции — внутренний ReactiveList
    public readonly ReactiveList<TElement> Items = new();

    // Viewport state — синхронизируется биндингом
    public readonly ReactiveValue<float> ScrollPosition = new(0f);
    public readonly ReactiveValue<int> FirstVisibleIndex = new(0);
    public readonly ReactiveValue<int> VisibleCount = new(0);

    // Конфигурация высоты элементов
    private Func<int, float> _heightProvider;
    private float _fixedHeight;

    // Фиксированная высота
    public ReactiveVirtualList(float itemHeight)
    {
        _fixedHeight = itemHeight;
        _heightProvider = null;
    }

    // Переменная высота
    public ReactiveVirtualList(Func<int, float> heightProvider)
    {
        _heightProvider = heightProvider;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetItemHeight(int index)
    {
        return _heightProvider != null ? _heightProvider(index) : _fixedHeight;
    }

    // Проксирование мутаций к ReactiveList
    public void Add(TElement item) => Items.Add(item);
    public void RemoveAt(int index) => Items.RemoveAt(index);
    public void Clear() => Items.Clear();
    public int Count => Items.Count;
    public TElement this[int index] => Items[index];

    // IReactiveValue implementation
    public void Dispose() { Items.Dispose(); ScrollPosition.Dispose(); /* ... */ }
    public void Unbind() { Items.Unbind(); ScrollPosition.Unbind(); /* ... */ }
}
```

### Алгоритм инерции и elastic bounce для кастомного скролла
```csharp
// [VERIFIED: алгоритмы извлечены из исходников Unity ScrollRect]
private void LateUpdate()
{
    var deltaTime = Time.unscaledDeltaTime;

    if (!_isDragging && _velocity != 0f)
    {
        // Elastic bounce — spring recovery к границам
        var offset = CalculateOffset();
        if (offset != 0f)
        {
            _scrollPosition = Mathf.SmoothDamp(
                _scrollPosition,
                _scrollPosition - offset,
                ref _velocity,
                _elasticity,
                Mathf.Infinity,
                deltaTime);

            if (Mathf.Abs(_velocity) < 1f)
            {
                _velocity = 0f;
            }
        }
        else if (_inertia)
        {
            // Инерция — экспоненциальное затухание
            _velocity *= Mathf.Pow(_decelerationRate, deltaTime);
            if (Mathf.Abs(_velocity) < 1f)
            {
                _velocity = 0f;
            }
            _scrollPosition += _velocity * deltaTime;
        }
        else
        {
            _velocity = 0f;
        }
    }

    UpdateVisibleRange();
}

// RubberDelta для elastic перетягивания за границы
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static float RubberDelta(float overStretching, float viewSize)
{
    return (1f - 1f / (Mathf.Abs(overStretching) * 0.55f / viewSize + 1f))
        * viewSize * Mathf.Sign(overStretching);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| ScrollRect + LayoutGroup для всех элементов | Виртуализация: только видимые элементы в иерархии | Стандартная практика с ~2018 | 10000 элементов вместо ~100 без просадок |
| Object.Instantiate/Destroy при скролле | Recycling Pool с SetActive или repositioning | Стандартная практика | Устраняет GC pressure от Instantiate |
| UIToolkit ListView.DynamicHeight | Для нового кода; uGUI для legacy/совместимости | Unity 2022+ | Не применимо — проект на uGUI |

**Deprecated/outdated:**
- `VerticalLayoutGroup` / `HorizontalLayoutGroup` для больших списков: приводит к O(n) layout rebuild, неприемлемо для 1000+ элементов
- `ContentSizeFitter` для контента виртуализированного списка: конфликтует с ручным управлением размером content

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `Stack.TryPop()` доступен в проекте (используется в BindingPool) — значит .NET Standard 2.1 или Unity 2021+ | Common Pitfalls | Нужно будет заменить на `Count > 0 ? Pop() : default` для Unity 2020.3 |
| A2 | `CanvasGroup.alpha = 0` эффективнее `SetActive(false)` для предотвращения Canvas rebuild | Common Pitfalls | Нужен бенчмарк; repositioning за viewport может быть ещё лучше |
| A3 | Overscan 2-4 элемента достаточен для плавного скролла | Architecture Patterns | Может потребоваться настройка; параметризовать |

## Open Questions (RESOLVED)

1. **Стратегия скрытия элементов за viewport** — RESOLVED
   - What we know: `SetActive(false)` вызывает Canvas rebuild; `CanvasGroup.alpha=0` менее затратно; repositioning за viewport — ещё один вариант
   - Resolution: UI-SPEC определяет подход SetActive(false) для скрытия элементов. Plan 02 реализует через ViewRecyclingPool с SetActive.

2. **Совместимость AbstractWidgetView.Connect() с recycling** — RESOLVED
   - What we know: `Assert.IsTrue(vm != ViewModel)` бросит при повторном Connect с тем же VM
   - Resolution: Всегда вызывать Dispose() на View перед помещением в recycling pool (обнуляет ViewModel). Plan 02 реализует в ViewRecyclingPool.Release().

3. **ReactiveVirtualList как IReactiveValue** — RESOLVED
   - What we know: AbstractViewModel автоматически собирает IReactiveValue поля через рефлексию
   - Resolution: ReactiveVirtualList реализует IReactiveValue с делегированием Dispose/Unbind ко всем внутренним полям. Plan 01 реализует в ReactiveVirtualList.cs.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Unity Test Framework (NUnit 3.5) via `com.unity.test-framework` |
| Config file | Нет — нужно создать `Tests/Runtime/Shtl.Mvvm.Tests.asmdef` |
| Quick run command | `Unity -runTests -testPlatform EditMode -testFilter Shtl.Mvvm` (CLI) |
| Full suite command | `Unity -runTests -testPlatform EditMode` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| VLIST-01 | Viewport culling: только видимые + overscan в результате | unit | NUnit тест LayoutCalculator | -- Wave 0 |
| VLIST-02 | Recycling pool: Get/Release корректно переиспользует | unit | NUnit тест ViewRecyclingPool | -- Wave 0 |
| VLIST-03 | Вертикальная прокрутка: scroll position обновляет visible range | unit | NUnit тест VirtualScrollRect logic | -- Wave 0 |
| VLIST-04 | Переменная высота: prefix sum корректно считает позиции | unit | NUnit тест LayoutCalculator с variable heights | -- Wave 0 |
| VLIST-05 | ReactiveList интеграция: add/remove/replace обновляет виртуализацию | unit | NUnit тест VirtualCollectionBinding | -- Wave 0 |
| VLIST-06 | IWidgetViewFactory: создание/удаление через фабрику | unit | NUnit тест с mock factory | -- Wave 0 |
| VLIST-07 | Zero-alloc hot path | manual | Unity Profiler — GC.Alloc в кадре скролла = 0 | -- manual |
| TEST-04 | Все unit-тесты проходят | integration | Full test suite | -- Wave 0 |

### Sampling Rate
- **Per task commit:** NUnit тесты через Unity Test Runner (Edit Mode)
- **Per wave merge:** Full test suite
- **Phase gate:** Full suite green + manual profiler check для VLIST-07

### Wave 0 Gaps
- [ ] `Tests/Runtime/Shtl.Mvvm.Tests.asmdef` — assembly definition для тестов
- [ ] `Tests/Runtime/LayoutCalculatorTests.cs` — тесты prefix sum, binary search, variable heights
- [ ] `Tests/Runtime/ViewRecyclingPoolTests.cs` — тесты Get/Release, pool size
- [ ] `Tests/Runtime/ReactiveVirtualListTests.cs` — тесты ViewModel-типа
- [ ] `Tests/Runtime/VirtualCollectionBindingTests.cs` — тесты интеграции с ReactiveList

## Security Domain

> Данная фаза — UI-компонент без сетевого взаимодействия, аутентификации, хранения данных или пользовательского ввода (кроме scroll). Секьюрити-домен не применим.

## Sources

### Primary (HIGH confidence)
- Исходный код проекта Shtl.Mvvm — все файлы в `Runtime/Core/` (прочитаны и проанализированы)
- [Unity ScrollRect исходники](https://github.com/liuqiaosz/Unity/blob/master/UGUI%E6%BA%90%E4%BB%A3%E7%A0%81/UnityEngine.UI/UI/Core/ScrollRect.cs) — алгоритмы инерции, elastic bounce, RubberDelta
- [Unity ScrollRect docs](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/script-ScrollRect.html) — параметры и конфигурация

### Secondary (MEDIUM confidence)
- [Build your own virtual scroll](https://dev.to/adamklein/build-your-own-virtual-scroll-part-i-11ib) — prefix sum + binary search алгоритм для переменной высоты
- [Frontend System Design: Virtualization](https://dev.to/zeeshanali0704/frontend-system-design-virtualization-handling-large-data-sets-29nf) — общие паттерны виртуализации
- [Unity Test Framework docs](https://docs.unity3d.com/Packages/com.unity.test-framework@1.3/manual/course/running-test.html) — настройка тестов в UPM-пакете

### Tertiary (LOW confidence)
- [GitHub: disruptorbeaminc/VirtualList](https://github.com/disruptorbeaminc/VirtualList) — uGUI-решение для виртуализации (не верифицировано детально)
- [GitHub: sinbad/UnityRecyclingListView](https://github.com/sinbad/UnityRecyclingListView) — recycling list для uGUI (не верифицировано детально)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — все компоненты используют существующие паттерны проекта, нет внешних зависимостей
- Architecture: HIGH — алгоритмы (prefix sum, binary search, recycling pool) хорошо изучены; архитектура следует существующим паттернам проекта (ElementCollectionBinding, BindingPool)
- Pitfalls: MEDIUM — Canvas rebuild и recycling edge cases требуют практической верификации через профайлер

**Research date:** 2026-04-09
**Valid until:** 2026-05-09 (стабильный домен, Unity uGUI не меняется)
