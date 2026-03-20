[English](README.md) | **Русский**

# Shtl.Mvvm

MVVM-фреймворк для Unity с чистым разделением слоев: **Модель**, **Виджет**, **Вью-модель**, **Вью**.

- **Пакет:** `com.shtl.mvvm`
- **Unity:** 2020.3+
- **Лицензия:** MIT

```
Unity Package Manager → Add package from git URL →
https://github.com/SelStrom/shtl-mvvm.git
```

---

## Содержание

- [Общий принцип работы](#общий-принцип-работы)
- [Архитектура](#архитектура)
  - [Модель](#модель)
  - [Виджет (бизнес-логика)](#виджет-бизнес-логика)
  - [Вью-модель](#вью-модель)
  - [Вью](#вью)
  - [Композиция виджетов](#композиция-виджетов)
- [Реактивные типы](#реактивные-типы)
  - [ObservableValue\<T\>](#observablevaluet)
  - [ReactiveValue\<T\>](#reactivevaluet)
  - [ReactiveList\<T\>](#reactivelistt)
  - [ReactiveAwaitable](#reactiveawaitable)
- [Биндинги](#биндинги)
  - [Принцип работы](#принцип-работы)
  - [Доступные биндинги](#доступные-биндинги)
  - [Кастомные биндинги](#кастомные-биндинги)
- [Жизненный цикл](#жизненный-цикл)
  - [AbstractWidgetView](#abstractwidgetview)
  - [Unbind и Dispose](#unbind-и-dispose)
- [IWidgetViewFactory](#iwidgetviewfactory)
- [Работа с анимациями](#работа-с-анимациями)
- [Editor-инструменты](#editor-инструменты)
- [Sample-проект](#sample-проект)
- [Cookbook](#cookbook)
- [Структура проекта](#структура-проекта)

---

## Общий принцип работы

Паттерн разбивает UI на четыре слоя:

| Слой | Ответственность | Зависимости |
|------|----------------|-------------|
| **Модель** | Данные предметной области. При изменении рассылает события | Ничего не знает об UI |
| **Виджет** (бизнес-логика) | Слушает модели и системы, преобразует данные, обновляет вью-модель, обрабатывает события | Знает о модели и вью-модели |
| **Вью-модель** | Иерархическая модель данных UI-элемента. Содержит только состояние. Сообщает об изменениях через реактивные параметры | Не имеет внешних зависимостей |
| **Вью** | Отрисовка. Слушает вью-модель и пользовательский ввод. Минимум логики | Знает только о своей вью-модели |

В классическом MVVM бизнес-логика реализуется во вью-моделях. Здесь она вынесена в отдельный слой (Виджет), что дает:

- Единое место подписки и обработки событий
- Бизнес-логику окна удобнее читать «горизонтально» в одном месте
- Возможность переопределять поведение для кастомных окон без наследования
- Модель и вью можно связать постфактум, когда вью уже готова и протестирована
- Логику фичи можно тестировать отдельно от UI

### Движение данных

```
Модель ──► Виджет ──► Вью-модель ──► Вью
                                       │
Модель ◄── Виджет ◄── Вью-модель ◄─────┘
                        (callbacks)
```

1. **Вверх:** данные поднимаются от моделей, преобразуются в виджете и отрисовываются вью.
2. **Вниз:** ввод пользователя спускается от вью через колбеки в бизнес-логику, которая изменяет модель или вью-модель.

---

## Архитектура

### Модель

Обычный класс без базовых сущностей фреймворка. Для реактивных полей используется `ObservableValue<T>`, который рассылает события при изменении значения.

```csharp
public class SampleModel
{
    public event Action<ElementModel> OnElementAdded;
    public event Action<int> OnElementRemoved;

    public ObservableValue<float> Score;
    public ObservableValue<int> IntScore;
    public List<ElementModel> Elements;

    public void AddNewElement()
    {
        var element = new ElementModel
        {
            Score = new ObservableValue<int>(42)
        };
        Elements.Add(element);
        OnElementAdded?.Invoke(element);
    }
}

public class ElementModel
{
    public ObservableValue<int> Score;
}
```

### Виджет (бизнес-логика)

Класс, инкапсулирующий бизнес-логику визуального элемента. Связывает модель с вью-моделью, обрабатывает события. Для не-базовых сущностей рекомендуется `sealed`.

```csharp
public sealed class SampleWidget
{
    private SampleViewModel _viewModel;
    private SampleModel _model;

    private IEventBindingContext _bindingContext;
    protected IEventBindingContext Bind => _bindingContext ??= new EventBindingContext();

    public void Connect(SampleModel model, SampleViewModel viewModel)
    {
        _model = model;
        _viewModel = viewModel;

        // Модель → Вью-модель (прямая привязка)
        Bind.From(_model.Score).To(_viewModel.Score);

        // Модель → Вью-модель (с трансформацией)
        Bind.From(_model.IntScore).To(_viewModel.PerformedScore, PerformScore);

        // Установка колбеков для кнопок
        _viewModel.OnAddElementButtonClicked.Value = _model.AddNewElement;
    }

    private static void PerformScore(int value, ReactiveValue<int> ctx) =>
        ctx.Value = value;
}
```

#### Когда что реализовывать в виджете?

| Ситуация | Подход |
|----------|--------|
| Мало логики | Достаточно одного объекта виджета |
| Много логики | Все еще удобнее в одном виджете — проще читать и поддерживать |
| Логику нужно реюзать между вью | Общая логика выносится в классы-контроллеры или статические методы |
| Логику нужно шарить между окнами | Общая модель + системы. Систему слушают виджеты, в них остается специфическая логика |
| Окно с вкладками | Отдельные вью-модели для вкладок, логика вкладки — в отдельном классе |

### Вью-модель

Наследуется от `AbstractViewModel`. Содержит только данные в виде реактивных параметров. За счет простоты может реюзаться с различными вью.

```csharp
public sealed class SampleViewModel : AbstractViewModel
{
    public readonly ReactiveValue<int> PerformedScore = new();
    public readonly ReactiveValue<float> Score = new();
    public readonly ReactiveList<ElementViewModel> Elements = new();

    public readonly ReactiveValue<Action> OnAddElementButtonClicked = new();
    public readonly ReactiveValue<Action> OnRemoveElementButtonClicked = new();

    public readonly SliderViewModel Slider = new();
    public readonly SliderViewModel ManualSlider = new();
}
```

`AbstractViewModel` автоматически обнаруживает все поля, реализующие `IReactiveValue`, и управляет их `Dispose()` и `Unbind()`.

**Вложенные вью-модели** объявляются как поля:

```csharp
public sealed class ParentViewModel : AbstractViewModel
{
    public readonly ChildViewModel Child = new();
}
```

### Вью

Наследуется от `AbstractWidgetView<TViewModel>`, расширяет `MonoBehaviour`. Вью не может устанавливать значения вью-модели напрямую, но может дергать колбеки.

```csharp
public sealed class SaleBuyButtonView : AbstractWidgetView<SaleBuyButtonViewModel>
{
    [SerializeField] private Button _button;
    [SerializeField] private TMP_Text _title;

    protected override void OnInitialized()
    {
        _button.interactable = false;
        _title.text = "";
    }

    protected override void OnConnected()
    {
        ViewModel.Price.Connect(value => SetupPrice(value));
        ViewModel.IsInteractable.Connect(value => _button.interactable = value);
        Bind.From(_button).To(ViewModel.OnClicked);
    }

    protected override void OnDisposed()
    {
        _title.text = string.Empty;
    }
}
```

### Композиция виджетов

Крупные визуальные элементы (окна, HUD) могут состоять из нескольких независимых виджетов. Каждый виджет работает автономно: у него своя модель, своя вью-модель и своя вью.

Пример — HUD, собранный из независимых модулей:

```
HudScreen
  ├── ResourcesWidget
  ├── PromoWidget
  └── QuestWidget
```

Если большая фича состоит из нескольких окон, у них может быть общая модель, а общая бизнес-логика выносится в одну или несколько систем. Систему слушают виджеты, в которых остается логика, специфическая для конкретного окна.

---

## Реактивные типы

### ObservableValue\<T\>

Реактивное значение для **слоя модели**. Уведомляет подписчиков через стандартный C# event `OnChanged`. Допускает множественных подписчиков.

```csharp
public class ObservableValue<T> : IObservableValue<T>
{
    public event Action<T> OnChanged;
    public T Value { get; set; }
}
```

Событие срабатывает только при фактическом изменении значения (проверка через `EqualityComparer<T>.Default`).

### ReactiveValue\<T\>

Реактивное значение для **слоя вью-модели**. Поддерживает ровно одну подписку через метод `Connect()`. Единственная подписка обеспечивает:

- Простую отладку движения данных
- Отсутствие проблем с синхронизацией
- Прямой вызов метода быстрее рассылки событий

```csharp
public class ReactiveValue<TValue> : IReactiveValue
{
    public TValue Value { get; set; }

    public void Connect(Action<TValue> onChanged);
    public void Unbind();
    public void Dispose();
}
```

**Поведение `Connect()`:** колбек вызывается сразу при подключении, если значение не `null` (для ссылочных типов) или всегда (для значимых типов). Это гарантирует **независимость данных вью-модели от порядка установки** — вью обновит все валидные значения сразу после связывания.

Повторный вызов `Connect()` при наличии существующей подписки бросит `InvalidOperationException`.

### ReactiveList\<T\>

Реактивный список для **вью-модели**. Синхронизирует состояние коллекции, отстреливает события только по факту изменений. На стороне бизнес-логики работает как обычный `IList<T>`.

```csharp
public class ReactiveList<TElement> : IReactiveValue, IList<TElement>
{
    public void Connect(
        Action<ReactiveList<TElement>> onContentChanged,
        Action<int, TElement> onElementAdded,
        Action<int, TElement> onElementReplaced,
        Action<int, TElement> onElementRemoved
    );

    public void ResizeAndFill(int newSize, Func<int, TElement> factoryMethod = null);
    public void AddRange(IEnumerable<TElement> range);
    public void Sort(IComparer<TElement> comparer);
}
```

`Connect()` принимает четыре колбека для полной синхронизации UI-списка с данными.

### ReactiveAwaitable

Позволяет подождать асинхронную операцию (анимацию, загрузку). Декларирует намерение ожидания во вью-модели, а вью обрабатывает его через `TaskCompletionSource<bool>`.

```csharp
public class ReactiveAwaitable : IReactiveValue
{
    public void Connect(Action<TaskCompletionSource<bool>> onWaitingStarted);
    public async Task<bool> StartAsync();
    public void Unbind();
}
```

**Использование в виджете:**

```csharp
await elementVm.WaitForAnimation.StartAsync();
```

**Использование во вью:**

```csharp
ViewModel.WaitForAnimation.Connect(StartAnimation);

private async void StartAnimation(TaskCompletionSource<bool> promise)
{
    await PlayAnimationAsync();
    promise.TrySetResult(true);
}
```

При `Unbind()` незавершенный промис автоматически отменяется через `TrySetCanceled()`, а `StartAsync()` в таком случае возвращает `true` (cancelled) вместо бросания исключения.

---

## Биндинги

Биндинги — надстройка над реактивными типами. Они:

- Автоматически подписываются и отписываются от событий вью-модели
- Инкапсулируют типовую логику установки значений во вью
- Обеспечивают единообразный подход для подписки на различные источники событий

### Принцип работы

Каждый биндинг расширяет `AbstractEventBinding`. Формируется список таких объектов, которые можно активировать, деактивировать или подчистить в цикле.

Доступ к API через геттер `Bind` в `AbstractWidgetView` (или создание `EventBindingContext` вручную в виджете):

```
Bind.From(<источник>).To(<приемник>)
```

Где `<источник>` — сущность, на изменение которой подписываемся, `<приемник>` — сущность, получающая результат.

Приемником может быть:
- **Объект** — используется метод по умолчанию для преобразования данных
- **Лямбда** — обработчик события

### Доступные биндинги

#### Модель → Вью-модель (`ModelToViewModelEventBindExtensions`)

```csharp
// Прямая привязка: ObservableValue<T> → ReactiveValue<T>
Bind.From(model.Score).To(viewModel.Score);

// С трансформацией через контекст:
Bind.From(model.IntScore).To(viewModel.PerformedScore, (src, dest) => dest.Value = src);

// С колбеком:
Bind.From(model.Score).To(value => Debug.Log(value));
```

#### UI → Вью-модель (`UIToViewModelEventBindExtensions`)

```csharp
// Кнопка → реактивный Action
Bind.From(_button).To(ViewModel.OnClicked);

// Кнопка → прямой колбек
Bind.From(_button).To(() => Debug.Log("Clicked"));

// Кнопка → колбек с контекстом
Bind.From(_button).To(someContext, ctx => HandleClick(ctx));

// Коллекция кнопок → реактивный Action
Bind.From(buttons).To(ViewModel.OnClicked);
```

#### Вью-модель → UI (`ViewModelToUIEventBindExtensions`)

```csharp
// Вложенная вью-модель → дочерняя вью
Bind.From(ViewModel.Child).To(_childView);

// ReactiveList → список вью (с префабом и контейнером)
Bind.From(ViewModel.Elements).To(_elementList, _prefab, _container);

// ReactiveList → список вью (с фабрикой)
Bind.From(ViewModel.Elements).To(_elementList, factory);

// ReactiveValue<string> → TMP_Text
Bind.From(ViewModel.Title).To(_titleText);

// ReactiveValue<int> → TMP_Text
Bind.From(ViewModel.Count).To(_countText);

// ReactiveValue<Color> → TMP_Text (цвет)
Bind.From(ViewModel.TextColor).To(_text);

// ReactiveValue<bool> → GameObject (SetActive)
Bind.From(ViewModel.IsVisible).To(_panel);

// ReactiveValue<int> → RectTransform (SetSiblingIndex)
Bind.From(ViewModel.Order).To(_rectTransform);
```

### Кастомные биндинги

Механизм поддерживает создание новых биндингов через extension-методы с именем `To`:

```csharp
public static class CustomBindExtensions
{
    public static void To(this BindFrom<ReactiveValue<float>> from, Slider slider) =>
        from.Source.Connect(value => slider.value = value);
}
```

---

## Жизненный цикл

### AbstractWidgetView

`AbstractWidgetView<TViewModel>` — базовый класс вью, расширяющий `MonoBehaviour`.

| Метод | Когда вызывается | Назначение |
|-------|-----------------|------------|
| `OnInitialized()` | Один раз при первом `Connect()` | Данные и связи на всю жизнь объекта |
| `OnConnected()` | Каждый раз при `Connect(vm)` | Связывание параметров вью-модели с обработчиками |
| `OnDisposed()` | При `Dispose()` | Очистка зависимостей перед пулингом |

```
Connect(vm) ─► OnInitialized() (если первый раз)
             ─► CleanUp биндингов
             ─► Dispose старой вью-модели
             ─► OnConnected()

Dispose()    ─► CleanUp биндингов
             ─► Unbind вью-модели
             ─► OnDisposed()
```

Связи, установленные через `Bind.From().To()`, автоматически разрушаются при `Dispose()` объекта или установке новой вью-модели.

### Unbind и Dispose

| Операция | Что делает |
|----------|-----------|
| **Unbind** | Рекурсивно подчищает колбеки всех параметров вью-модели. Данные сохраняются. Полезно для уничтожения/отписки части вьюхи с сохранением консистентности вью-модели |
| **Dispose** | Unbind + сброс значений в `default`. Все сущности кроме модели являются `IReactiveValue` и должны очищаться перед добавлением в пул |

Исключение: вложенные вью-модели диспозятся автоматически. Старые вью-модели диспозятся при инжекте новой модели во вью.

---

## IWidgetViewFactory

Интерфейс для создания/удаления вью для элементов `ReactiveList`:

```csharp
public interface IWidgetViewFactory<in TViewModel, TWidgetView>
    where TViewModel : AbstractViewModel, new()
    where TWidgetView : AbstractWidgetView<TViewModel>, new()
{
    TWidgetView CreateWidget(TViewModel viewModel);
    void RemoveWidget(TWidgetView view);
}
```

Простейшая фабрика с пулом:

```csharp
public class PoolableFactory<TViewModel, TWidgetView> : IWidgetViewFactory<TViewModel, TWidgetView>
    where TViewModel : AbstractViewModel, new()
    where TWidgetView : AbstractWidgetView<TViewModel>, new()
{
    private readonly PrefabPool<TWidgetView> _pool;

    public PoolableFactory(GameObject prefab, Transform parent)
    {
        _pool = new PrefabPool<TWidgetView>(prefab, parent);
    }

    public TWidgetView CreateWidget(TViewModel viewModel) => _pool.Get();
    public void RemoveWidget(TWidgetView view) => _pool.Release(view);
}
```

Фабрика во вью с пулом из верстки (элементы уже лежат на сцене):

```csharp
public class TabsPanelView : AbstractWidgetView<TabsPanelViewModel>,
    IWidgetViewFactory<TabItemViewModel, TabItemView>
{
    [SerializeField] private Transform _container;
    [SerializeField] private GameObject _prefab;
    private Stack<TabItemView> _viewCache;
    private readonly List<TabItemView> _visibleViews = new();

    protected override void OnInitialized()
    {
        _viewCache = new Stack<TabItemView>(
            _container.GetComponentsInChildren<TabItemView>());
    }

    protected override void OnConnected()
    {
        Bind.From(ViewModel.Tabs).To(_visibleViews, this);
    }

    public TabItemView CreateWidget(TabItemViewModel viewModel)
    {
        return !_viewCache.TryPop(out var view)
            ? Instantiate(_prefab, _container).GetComponent<TabItemView>()
            : view;
    }

    public void RemoveWidget(TabItemView view)
    {
        view.gameObject.SetActive(false);
        _viewCache.Push(view);
    }
}
```

---

## Работа с анимациями

Анимации проще всего представлять как асинхронный черный ящик.

### Если анимацию ждать не нужно

Запускаем во вью как реакцию на событие вью-модели:

```csharp
ViewModel.ShowEffect.Connect(value => _animator.SetTrigger("show"));
```

### Если анимацию нужно подождать — `ReactiveAwaitable`

**Вью-модель** декларирует намерение:

```csharp
public sealed class WidgetViewModel : AbstractViewModel
{
    public readonly ReactiveValue<Action> OnButtonClicked = new();
    public readonly ReactiveAwaitable WaitForAnimation = new();
}
```

**Виджет** управляет порядком выполнения:

```csharp
private async Task DoAnimateAsync()
{
    await _viewModel.WaitForAnimation.StartAsync();
    // код после завершения анимации
}
```

**Вью** выполняет анимацию и комплитит промис:

```csharp
protected override void OnConnected()
{
    ViewModel.WaitForAnimation.Connect(StartAnimation);
    Bind.From(_button).To(ViewModel.OnButtonClicked);
}

private async void StartAnimation(TaskCompletionSource<bool> promise)
{
    await PlayAnimationCoroutine();
    promise.TrySetResult(true);
}
```

### Через колбеки

Во вью-модели создается `ReactiveValue<Action>`, который дергается в удобный момент. На уровне бизнес-логики колбек обрабатывается однозначно:

```csharp
// Вью-модель
public readonly ReactiveValue<Action> OnAnimationComplete = new();

// Виджет
_viewModel.OnAnimationComplete.Value = HandleAnimationComplete;
```

---

## Editor-инструменты

### ViewModel Viewer

Окно для инспекции вью-моделей в реальном времени: **Window → ViewModel Viewer**.

Автоматически находит активные `AbstractWidgetView` в сцене (с суффиксом `WidgetView` или `WindowView`), извлекает их вью-модели и отображает дерево реактивных параметров с текущими значениями.

### DevWidget

Компонент для превью префабов с вью-моделями в Edit Mode. Позволяет:

- Инстанцировать UI-префаб и автоматически подключить вью-модель
- Редактировать значения вью-модели через инспектор
- Сохранять/загружать состояние вью-модели в JSON

---

## Sample-проект

Полный пример находится в `Samples~/Sample/`. Для импорта в Unity используйте Package Manager → Samples.

### Структура

```
Samples~/Sample/Assets/Scripts/
├── EntryScreen.cs           # Точка входа: создает модель, вью-модель, виджет
├── SampleWidget.cs          # Бизнес-логика: связывает модель с вью-моделью
├── Model/
│   ├── SampleModel.cs       # Модель с ObservableValue и событиями
│   └── ElementModel.cs      # Модель элемента списка
└── View/
    ├── SampleWidgetView.cs  # Вью + SampleViewModel (вью-модель)
    ├── ElementView.cs       # Вью элемента + ElementViewModel
    ├── SliderViewModel.cs   # Вью-модель слайдера
    ├── AutoSliderView.cs    # Однонаправленный слайдер (только отображение)
    └── ManualSliderView.cs  # Двунаправленный слайдер (с вводом)
```

### Поток данных в sample

```
EntryScreen
  ├─ создает SampleModel (ObservableValue<float> Score, ObservableValue<int> IntScore, List<ElementModel>)
  ├─ создает SampleViewModel
  ├─ создает SampleWidget.Connect(model, viewModel)
  │   ├─ Bind.From(model.Score).To(viewModel.Score)
  │   ├─ Bind.From(model.IntScore).To(viewModel.PerformedScore, PerformScore)
  │   └─ viewModel.OnAddElementButtonClicked.Value = model.AddNewElement
  └─ SampleWidgetView.Connect(viewModel)
      ├─ viewModel.Score.Connect(score => _scoreTitle.text = ...)
      ├─ Bind.From(_addButton).To(viewModel.OnAddElementButtonClicked)
      ├─ Bind.From(viewModel.Elements).To(_elementList, _prefab, _container)
      └─ Bind.From(viewModel.Slider).To(_autoSliderView)
```

---

## Cookbook

### Как создать новое окно?

1. Реализовать вью-модель, наследуясь от `AbstractViewModel`
2. Реализовать вью, наследуясь от `AbstractWidgetView<TViewModel>`
3. Реализовать виджет (обычный класс) с бизнес-логикой

### Как вложить одну вью в другую?

Во вью-модели:

```csharp
public sealed class ParentViewModel : AbstractViewModel
{
    public readonly ChildViewModel Child = new();
}
```

Во вью:

```csharp
public sealed class ParentView : AbstractWidgetView<ParentViewModel>
{
    [SerializeField] private ChildView _childView;

    protected override void OnConnected()
    {
        Bind.From(ViewModel.Child).To(_childView);
    }
}
```

### Как использовать легаси-компоненты?

**Компонент — лист в дереве UI:**

Подключите как `[SerializeField]` во вью. Через вью-модель пробросьте необходимые данные:

```csharp
protected override void OnConnected()
{
    ViewModel.Price.Connect(value => _legacyPriceView.Setup(value));
}
```

**Компонент — корень дерева UI (уже готовый виджет вью):**

1. В компоненте создайте экземпляр вью-модели и определите ссылку на виджет вью
2. Забиндите вью-модель с виджет вью
3. Инкапсулируйте логику установки вью-модели в компоненте

### Как понять, что класть в Widget, а что во View?

Ответьте на вопрос: *достаточно ли данных во вью-модели, чтобы понять текущее состояние вью или написать юнит-тесты?*

- Если без кода интерфейс ломается → код идет в виджет
- Если из вью-модели можно восстановить стейт → код во вью

Любая внешняя зависимость во вью осложнит тестирование или изолированную разработку.

### Параметр вью завязан на два параметра вью-модели?

Не обрабатывайте их раздельно — это приводит к двойной работе и неочевидной логике. Заверните в Tuple:

```csharp
public readonly ReactiveValue<(string label, int count)> LabelWithCount = new();
```

`ValueTuple` — структура, не вызывает лишних аллокаций.

### Можно ли наследовать вью-модель от вью-модели?

Нет. Предпочитайте композицию. Вью-модель — простая сущность, из таких легко собирать комплексные объекты.

### Можно ли реюзать вью-модели?

Да. Например, вью-модель с полями `IconId` и `Text` может использоваться с любой вью, умеющей рисовать иконку и текст. При изменении требований к вью модель легко заменяется на кастомную.

---

## Структура проекта

```
shtl-mvvm/
├── package.json                            # UPM-манифест пакета
├── LICENSE                                 # MIT
├── .editorconfig                           # Code style
│
├── Runtime/                                # Shtl.Mvvm — основная библиотека
│   ├── Shtl.Mvvm.asmdef
│   ├── DevWidget.cs                        # Компонент для превью в Editor
│   ├── Core/
│   │   ├── AbstractWidgetView.cs           # Базовый класс вью (MonoBehaviour)
│   │   ├── IWidgetView.cs                  # Интерфейс вью
│   │   ├── Interfaces/
│   │   │   ├── IReactiveListCount.cs
│   │   │   ├── IEventBindingContext.cs
│   │   │   ├── IObservableValue.cs
│   │   │   ├── IViewModelParameter.cs      # Содержит IReactiveValue
│   │   │   └── IWidgetViewFactory.cs
│   │   ├── Types/
│   │   │   ├── AbstractViewModel.cs        # Базовый класс вью-модели
│   │   │   ├── ReactiveValue.cs            # Реактивное значение (1 подписчик)
│   │   │   ├── ReactiveList.cs             # Реактивный список
│   │   │   ├── ReactiveAwaitable.cs        # Асинхронное ожидание
│   │   │   └── ObservableValue.cs          # Observable-значение (N подписчиков)
│   │   └── Bindings/
│   │       ├── AbstractEventBinding.cs     # Базовый класс биндинга
│   │       ├── BindFrom.cs                 # Fluent API: BindFrom<TSource>
│   │       ├── BindingPool.cs              # Пул объектов биндингов
│   │       ├── EventBindingContext.cs       # Контекст хранения биндингов
│   │       ├── WidgetViewBinding.cs         # ViewModel → AbstractWidgetView
│   │       ├── ElementCollectionBinding.cs  # ReactiveList → список вью
│   │       ├── ButtonEventBinding.cs        # Button → Action<T>
│   │       ├── ButtonEventSimpleBinding.cs  # Button → Action
│   │       ├── ButtonCollectionEventBinding.cs  # Button[] → Action<T>
│   │       └── ObservableValueEventBinding.cs   # ObservableValue → callback
│   └── Utils/
│       ├── BindFromExtensions.cs                    # ctx.From(source)
│       ├── ModelToViewModelEventBindExtensions.cs   # ObservableValue → ReactiveValue
│       ├── UIToViewModelEventBindExtensions.cs      # Button → ReactiveValue<Action>
│       └── ViewModelToUIEventBindExtensions.cs      # ReactiveValue → TMP_Text, etc.
│
├── Editor/                                 # Shtl.Mvvm.Editor — инструменты
│   ├── Shtl.Mvvm.Editor.asmdef
│   ├── ViewModelViewerWindow.cs            # Window → ViewModel Viewer
│   ├── ViewModelDrawer.cs                  # Отрисовка дерева вью-модели
│   └── DevWidgetEditor.cs                  # Инспектор DevWidget
│
└── Samples~/                               # Sample-проект
    └── Sample/
        ├── Assets/
        │   ├── Scripts/                    # Исходный код примера
        │   ├── Scenes/mvvm_demo.unity
        │   └── Prefabs/
        └── Packages/manifest.json          # com.shtl.mvvm: "file:../../../"
```

### Зависимости

| Пакет | Версия |
|-------|--------|
| `com.unity.textmeshpro` | 3.0.7 |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 |
