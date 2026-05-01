# Архитектурные паттерны

**Домен:** MVVM-фреймворк для Unity (uGUI), builder-биндинги, two-way binding, виртуализированный список
**Дата исследования:** 2026-04-09

## Текущая архитектура (as-is)

Перед проектированием новых компонентов важно зафиксировать существующую структуру, от которой отталкиваются все изменения.

### Слои и поток данных (текущий)

```
Model (ObservableValue<T>)
  │  event OnChanged
  ▼
ViewModel (AbstractViewModel, ReactiveValue<T>, ReactiveList<T>)
  │  Connect(callback)
  ▼
View (AbstractWidgetView<TVM>)
  │  Bind.From(source).To(target)
  ▼
uGUI (Text, Button, GameObject, ...)
```

**Однонаправленный поток:** Model -> ViewModel -> View. Обратная связь (UI -> ViewModel) реализована только через callbacks на Button, а не через двустороннюю привязку данных.

### Ключевые компоненты (текущие)

| Компонент | Ответственность | Связи |
|-----------|-----------------|-------|
| `BindFrom<TSource>` | Readonly struct, точка входа fluent API | Создается через `IEventBindingContext.From()`, вызывает `LinkTo()` |
| `AbstractEventBinding` | Абстрактный биндинг: Activate/Invoke/Dispose | Управляется `EventBindingContext`, пулируется через `BindingPool` |
| `EventBindingContext` | Хранит Dictionary<object, binding>, lifecycle | Владеет всеми биндингами View, CleanUp при реконнекте |
| `BindingPool` | Статический пул типизированных биндингов | Stack<T> per type, Get/Release |
| `ReactiveValue<T>` | Одиночное реактивное значение (1:1 Connect) | Единственный подписчик, throws при повторном Connect |
| `ObservableValue<T>` | Наблюдаемое значение (N подписчиков) | event Action<T>, используется в Model-слое |
| `ReactiveList<T>` | Реактивная коллекция с add/replace/remove | Connect с 4 callbacks, единственный подписчик |
| `ElementCollectionBinding` | Синхронизация ReactiveList -> List<WidgetView> | Создает/удаляет View через factory или Instantiate |
| `AbstractWidgetView<TVM>` | MonoBehaviour базовый класс View | Владеет EventBindingContext, Connect(vm)/Dispose lifecycle |

### Критическое ограничение текущего API

`BindFrom<TSource>` -- **readonly struct**. Методы `.To()` реализованы как extension-методы, каждый из которых **немедленно создает binding и вызывает Activate()**. Нет промежуточного этапа конфигурации. Это ключевое ограничение, которое меняет рефакторинг билдера.

---

## Рекомендуемая архитектура (to-be)

### 1. Builder-паттерн биндингов

#### Проблема

Текущий `BindFrom<T>.To()` немедленно создает и активирует биндинг. Нет способа добавить конфигурацию (TwoWay, WithConverter, WithCondition) между From и To.

#### Решение: BindingBuilder<TSource> как промежуточная структура

```
Bind.From(source)           -> возвращает BindingBuilder<TSource> (struct)
    .WithConverter(fn)      -> возвращает тот же BindingBuilder<TSource> (мутация struct)
    .TwoWay()               -> устанавливает флаг, возвращает BindingBuilder<TSource>
    .To(target)             -> ФИНАЛИЗАЦИЯ: создает binding, вызывает Activate
```

**Ключевое архитектурное решение:** `BindingBuilder<TSource>` должен быть **ref struct** (или readonly struct с мутабельными полями через Unsafe, но ref struct чище). Ref struct гарантирует, что билдер не "утечет" за пределы метода, и финализация произойдет в том же scope.

**ВНИМАНИЕ: Unity 2020.3 ограничение.** Ref struct не может реализовывать интерфейсы и не может быть захвачен в лямбды. Для Unity 2020.3+ совместимости безопаснее использовать **обычный readonly struct** как в текущем `BindFrom<TSource>`, но с дополнительными полями конфигурации. Финализация -- по вызову `.To()`.

#### Рекомендуемая структура компонентов

```
BindingBuilder<TSource>          (readonly struct, замена BindFrom<TSource>)
├── Source: TSource               (что наблюдаем)
├── Context: IEventBindingContext (куда регистрируем)
├── Direction: BindingDirection   (OneWay | TwoWay)
├── Converter: object             (опциональный конвертер, nullable)
└── .To(target) methods           (extension-методы, финализируют builder)
```

#### Обратная совместимость

`BindFrom<TSource>` сохраняется как deprecated alias или `BindingBuilder<TSource>` получает implicit conversion из `BindFrom<TSource>`. Лучший вариант: **переименовать `BindFrom<TSource>` в `BindingBuilder<TSource>`**, добавить поля, существующие `.To()` extension-методы продолжают работать без изменений, потому что сигнатура `this BindingBuilder<TSource> from` совместима.

#### Диаграмма компонентов (Builder)

```
IEventBindingContext
  │
  │ .From(source)
  ▼
BindingBuilder<TSource>   [readonly struct]
  │
  │ .TwoWay() / .WithConverter() / ...  (опциональная конфигурация)
  │
  │ .To(target)            [extension methods -- финализация]
  ▼
AbstractEventBinding       [создан из пула, Activate()]
  │
  │ зарегистрирован в
  ▼
EventBindingContext         [lifecycle management]
```

### 2. Two-way биндинги

#### Проблема

Все биндинги однонаправленные. InputField, Slider, Toggle -- пользователь меняет значение в UI, но оно не отражается в ViewModel.

#### Архитектурный подход

Two-way binding -- это **два однонаправленных биндинга** с guard от бесконечного цикла:

```
ViewModel.Property ──OnChanged──> View.InputField.text     (forward)
View.InputField ──onValueChanged──> ViewModel.Property.Value (reverse)
```

**Guard от цикла:** при обновлении из reverse-биндинга, forward-биндинг НЕ должен заново записывать в UI. Реализация: флаг `_isUpdating` внутри биндинга.

#### Компонент: TwoWayEventBinding<TSource>

```csharp
// Псевдо-структура нового биндинга
public class TwoWayEventBinding<TValue> : AbstractEventBinding<TwoWayEventBinding<TValue>>
{
    private ReactiveValue<TValue> _vmProperty;
    private Func<TValue> _viewGetter;           // чтение из UI
    private Action<TValue> _viewSetter;         // запись в UI
    private Action<Action<TValue>> _viewSubscribe;   // подписка на UI-событие
    private Action<Action<TValue>> _viewUnsubscribe; // отписка
    private bool _isUpdating;                   // guard от цикла

    // Forward: VM -> View
    private void OnVmChanged(TValue value)
    {
        if (_isUpdating) { return; }
        _isUpdating = true;
        _viewSetter(value);
        _isUpdating = false;
    }

    // Reverse: View -> VM
    private void OnViewChanged(TValue value)
    {
        if (_isUpdating) { return; }
        _isUpdating = true;
        _vmProperty.Value = value;
        _isUpdating = false;
    }
}
```

#### Паттерн подписки на uGUI-виджеты

uGUI-компоненты имеют разные API подписки:

| Компонент | Событие | Тип значения |
|-----------|---------|--------------|
| `InputField` | `onValueChanged` | `string` |
| `TMP_InputField` | `onValueChanged` | `string` |
| `Slider` | `onValueChanged` | `float` |
| `Toggle` | `onValueChanged` | `bool` |
| `Dropdown` | `onValueChanged` | `int` |
| `Scrollbar` | `onValueChanged` | `float` |

Все используют `UnityEvent<T>`, что позволяет унифицировать подписку через `AddListener`/`RemoveListener`.

#### Extension-методы для TwoWay

```csharp
// Использование (после builder-рефакторинга):
Bind.From(vm.PlayerName).TwoWay().To(inputField);

// Без builder (обратная совместимость):
Bind.From(vm.PlayerName).ToTwoWay(inputField);
```

#### Диаграмма потока данных (Two-Way)

```
ReactiveValue<string> vm.Name
  │                          ▲
  │ OnChanged(value)         │ .Value = newValue
  │                          │
  ▼                          │
TwoWayEventBinding<string>   │
  │  [_isUpdating guard]     │
  │                          │
  ▼                          │
InputField.text = value      InputField.onValueChanged
                              (reverse path)
```

### 3. Виртуализированный список

#### Проблема

`ElementCollectionBinding` создает View для **каждого** элемента ReactiveList. 1000 элементов = 1000 GameObjects, что неприемлемо по производительности.

#### Архитектура виртуализированного списка

Основана на паттерне recycling pool, применяемом во всех зрелых UI-фреймворках (iOS UITableView, Android RecyclerView, Unity UIToolkit ListView).

##### Компоненты

```
VirtualizedList<TVM, TView>       [MonoBehaviour, главный компонент]
├── ScrollRect                     [uGUI, контейнер прокрутки]
├── RectTransform content          [контент-контейнер, задает total size]
├── ILayoutStrategy                [стратегия расположения элементов]
│   ├── VerticalLayoutStrategy
│   ├── HorizontalLayoutStrategy
│   └── GridLayoutStrategy
├── RecyclingPool<TView>           [пул переиспользуемых View]
├── IItemSizeProvider              [опционально: произвольные размеры]
└── ReactiveList<TVM>              [источник данных]
```

##### ILayoutStrategy -- стратегия расположения

```
interface ILayoutStrategy
{
    // Вычислить общий размер контента
    float GetContentSize(int itemCount);

    // Какие индексы видимы при данном scroll offset?
    (int startIndex, int endIndex) GetVisibleRange(float scrollOffset, float viewportSize);

    // Позиция элемента по индексу
    Vector2 GetItemPosition(int index);

    // Размер элемента по индексу (для uniform -- одинаковый)
    Vector2 GetItemSize(int index);
}
```

Стратегия позволяет менять layout (vertical/horizontal/grid) без изменения логики recycling.

##### Цикл recycling

```
OnScroll(scrollPosition):
  1. Вычислить (newStartIdx, newEndIdx) через ILayoutStrategy
  2. Для каждого активного View за пределами нового диапазона:
     - view.Dispose()
     - Вернуть в RecyclingPool
  3. Для каждого нового индекса в диапазоне:
     - view = RecyclingPool.Get() или создать новый
     - view.Connect(reactiveList[index])
     - Установить позицию через ILayoutStrategy
  4. Обновить content size в ScrollRect
```

##### Связь с ReactiveList

Виртуализированный список подписывается на ReactiveList тем же Connect API:

```
reactiveList.Connect(
    onContentChanged: RefreshVisibleRange,
    onElementAdded: OnItemAdded,
    onElementReplaced: OnItemReplaced,
    onElementRemoved: OnItemRemoved
);
```

При add/remove пересчитывается content size и обновляется видимый диапазон. Критично: при удалении элемента ДО видимой области нужно скорректировать scroll offset, чтобы не было визуального скачка.

##### Поддержка произвольных размеров элементов

**Рекомендация: начать с фиксированного размера.** Произвольные размеры значительно усложняют:
- Нужен кэш высот (prefix sum array для O(1) позиционирования)
- При изменении высоты элемента -- пересчет всех последующих позиций
- Нужен механизм измерения высоты до показа (ContentSizeFitter или LayoutUtility.GetPreferredHeight)

Для фазы 1: `float itemSize` -- фиксированный, для всех элементов одинаковый.
Для фазы 2 (опционально): `IItemSizeProvider` с кэшированием.

##### Диаграмма компонентов (Virtualized List)

```
ReactiveList<TViewModel>
  │
  │ Connect(callbacks)
  ▼
VirtualizedListBinding<TVM, TView>  [AbstractEventBinding]
  │
  │ управляет
  ▼
VirtualizedList<TVM, TView>         [MonoBehaviour]
  ├── ScrollRect                    [uGUI]
  │     │ onValueChanged
  │     ▼
  │   OnScroll() -> пересчет видимого диапазона
  │
  ├── ILayoutStrategy               [стратегия позиционирования]
  │
  ├── RecyclingPool<TView>          [пул View]
  │     ├── Get() -> TView
  │     └── Release(TView)
  │
  └── Dictionary<int, TView>        [activeViews: index -> view]
        активные View в viewport
```

---

## Границы компонентов

### Что с чем взаимодействует

```
                    ┌─────────────────────────┐
                    │   Extension Methods      │
                    │ (ViewModelToUI, UIToVM,  │
                    │  ModelToVM)              │
                    └──────────┬──────────────┘
                               │ создают биндинги
                               ▼
┌──────────┐     ┌──────────────────┐     ┌─────────────┐
│ Binding  │◄────│ BindingBuilder   │◄────│ IEventBind- │
│ Pool     │     │ <TSource>        │     │ ingContext   │
│ (static) │     │ (readonly struct)│     │ (per View)  │
└──────────┘     └──────────────────┘     └─────────────┘
                               │
              ┌────────────────┼────────────────┐
              ▼                ▼                 ▼
     OneWay bindings    TwoWay bindings   VirtualizedList
     (existing)         (NEW)             binding (NEW)
              │                │                 │
              ▼                ▼                 ▼
         ReactiveValue   ReactiveValue     ReactiveList
              │           + UI event            │
              ▼                ▼                 ▼
          uGUI widget    uGUI InputField   VirtualizedList
                         /Slider/Toggle    MonoBehaviour
```

### Правила изоляции

1. **BindingBuilder** знает только о `IEventBindingContext` и `TSource`. Не знает о конкретных биндингах.
2. **Extension-методы** знают о конкретных типах (ReactiveValue + InputField) и создают правильные биндинги.
3. **TwoWayEventBinding** знает о ReactiveValue и uGUI UnityEvent API. Не знает о ViewModel.
4. **VirtualizedList** знает о ScrollRect, RecyclingPool и ILayoutStrategy. Привязка к ReactiveList -- через отдельный binding.
5. **BindingPool** -- чистый статический пул, не знает о типах биндингов.

---

## Рекомендуемый порядок реализации

Основан на зависимостях между компонентами:

### Фаза 1: Builder-рефакторинг

**Зависимости:** Нет (основа для остального)
**Что делать:**
1. Переименовать `BindFrom<T>` в `BindingBuilder<T>` (или сделать BindingBuilder оберткой)
2. Добавить поля Direction, Converter в структуру
3. Добавить chain-методы `.TwoWay()`, `.WithConverter()`
4. Адаптировать все существующие extension-методы `.To()` для работы с новой структурой
5. Убедиться, что `Bind.From(x).To(y)` работает идентично текущему поведению

**Критерий готовности:** все существующие `.To()` вызовы работают без изменений в клиентском коде.

### Фаза 2: Two-way биндинги

**Зависимости:** Фаза 1 (нужен builder с `.TwoWay()` конфигурацией)
**Что делать:**
1. Создать `TwoWayEventBinding<TValue>` с guard-логикой
2. Extension-методы `.To()` для InputField, Slider, Toggle, Dropdown, которые проверяют `Direction == TwoWay`
3. Конвертеры для типов (string -> int, int -> string и т.д.)

**Альтернативный путь (без builder):** можно реализовать `.ToTwoWay()` extension-методы параллельно фазе 1. Но это приведет к дублированию, когда builder будет готов.

### Фаза 3: Виртуализированный список

**Зависимости:** Нет прямых зависимостей от Фаз 1-2, но логически идет после стабилизации API
**Что делать:**
1. `RecyclingPool<TView>` -- пул View-элементов
2. `ILayoutStrategy` + `VerticalLayoutStrategy` (базовый, фиксированный размер)
3. `VirtualizedList<TVM, TView>` MonoBehaviour -- основной компонент
4. `VirtualizedListBinding` -- AbstractEventBinding для интеграции с `Bind.From().To()` API
5. `HorizontalLayoutStrategy`, `GridLayoutStrategy` -- расширения
6. (Опционально) Поддержка произвольных размеров элементов

---

## Паттерны для соблюдения

### Паттерн 1: Deferred Finalization (Builder)

**Что:** Конфигурация накапливается в struct, финализация -- по `.To()`.
**Когда:** Любой новый chain-метод.
**Почему:** Struct на стеке, zero-alloc, естественная точка финализации.

```csharp
// Правильно: конфигурация без аллокаций, финализация в To()
Bind.From(vm.Health)
    .WithConverter(h => h.ToString())
    .To(healthLabel);

// Неправильно: промежуточный объект-билдер в куче
var builder = new BindingBuilderClass(vm.Health); // аллокация!
builder.Convert(h => h.ToString());
builder.To(healthLabel);
```

### Паттерн 2: Guard Flag для Two-Way

**Что:** `_isUpdating` bool флаг предотвращает бесконечный цикл VM->View->VM->View...
**Когда:** Любой two-way биндинг.
**Почему:** uGUI компоненты могут вызывать onValueChanged при программной установке value.

```csharp
private void OnVmChanged(TValue value)
{
    if (_isUpdating) { return; }
    _isUpdating = true;
    try { _viewSetter(value); }
    finally { _isUpdating = false; }
}
```

### Паттерн 3: Strategy для Layout

**Что:** ILayoutStrategy инкапсулирует логику расположения элементов.
**Когда:** Виртуализированный список.
**Почему:** Позволяет добавлять horizontal, grid, staggered без изменения recycling-логики.

---

## Анти-паттерны для избегания

### Анти-паттерн 1: Eager Binding Creation в Builder

**Что:** Создавать биндинг при вызове `.TwoWay()` или `.WithConverter()`.
**Почему плохо:** Нарушает deferred finalization. Если пользователь вызовет `.TwoWay()` но не `.To()`, биндинг утечет.
**Вместо этого:** Накапливать конфигурацию в полях struct, создавать биндинг только в `.To()`.

### Анти-паттерн 2: Прямая мутация ReactiveValue из View без Guard

**Что:** `inputField.onValueChanged += v => vm.Name.Value = v;` без защиты от цикла.
**Почему плохо:** ReactiveValue.Value setter вызывает OnChanged, что обновит inputField.text, что вызовет onValueChanged снова.
**Вместо этого:** Всегда использовать TwoWayEventBinding с `_isUpdating` guard.

### Анти-паттерн 3: Создание всех View при инициализации VirtualizedList

**Что:** Предсоздавать View для всех элементов ReactiveList.
**Почему плохо:** Теряет весь смысл виртуализации. 10000 элементов = 10000 GameObjects.
**Вместо этого:** Создавать View только для видимого диапазона + буфер (2-4 элемента сверху и снизу).

### Анти-паттерн 4: Ломать ReactiveValue для поддержки множественных подписчиков

**Что:** Убирать ограничение "Already bound" в ReactiveValue для two-way.
**Почему плохо:** ReactiveValue намеренно 1:1 -- это гарантия предсказуемости. Множественные подписчики -- ObservableValue.
**Вместо этого:** TwoWayBinding подписывается как единственный listener ReactiveValue, внутри себя управляет обоими направлениями.

---

## Масштабируемость

| Аспект | 10 элементов | 1000 элементов | 100000 элементов |
|--------|--------------|----------------|------------------|
| Без виртуализации | OK | Тормоза Canvas rebuild | Невозможно |
| С виртуализацией (fixed size) | Оверхед бессмыслен | OK | OK |
| С виртуализацией (variable size) | Оверхед бессмыслен | OK | OK, нужен кэш высот |
| Two-way биндинги | Нет проблем | Нет проблем (1 binding = 1 UI element) | N/A (не бывает 100k InputField) |
| Builder overhead | Нулевой (struct) | Нулевой | Нулевой |

**Порог включения виртуализации:** Рекомендовать виртуализированный список при >50 элементов. Для <50 стандартный `ElementCollectionBinding` работает адекватно.

---

## Источники

- Текущий исходный код Shtl.Mvvm (Runtime/Core/, Runtime/Utils/) -- HIGH confidence
- [UnityRecyclingListView](https://github.com/sinbad/UnityRecyclingListView) -- архитектура recycling pool, фиксированный размер -- MEDIUM confidence
- [Recyclable-Scroll-Rect](https://github.com/MdIqubal/Recyclable-Scroll-Rect) -- паттерн DataSource + Cell, vertical/horizontal/grid -- MEDIUM confidence
- [UnityMvvmToolkit](https://github.com/LibraStack/UnityMvvmToolkit) -- подходы к two-way binding в Unity MVVM -- MEDIUM confidence
- [Unity-Weld](https://github.com/Real-Serious-Games/Unity-Weld) -- two-way data binding для uGUI -- MEDIUM confidence
- [Loxodon Framework](https://vovgou.github.io/loxodon-framework/) -- полноценный MVVM с databinding для Unity -- MEDIUM confidence
- [Unity 6 Data Binding docs](https://docs.unity3d.com/6000.3/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/data-binding.html) -- референс UIToolkit подхода -- LOW confidence (UIToolkit, не uGUI)
