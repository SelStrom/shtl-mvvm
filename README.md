**English** | [Русский](README.ru.md)

# Shtl.Mvvm

MVVM framework for Unity with clean layer separation: **Model**, **Widget**, **ViewModel**, **View**.

- **Package:** `com.shtl.mvvm`
- **Unity:** 2020.3+
- **License:** MIT

```
Unity Package Manager → Add package from git URL →
https://github.com/SelStrom/shtl-mvvm.git
```

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
  - [Model](#model)
  - [Widget (Business Logic)](#widget-business-logic)
  - [ViewModel](#viewmodel)
  - [View](#view)
  - [Widget Composition](#widget-composition)
- [Reactive Types](#reactive-types)
  - [ObservableValue\<T\>](#observablevaluet)
  - [ReactiveValue\<T\>](#reactivevaluet)
  - [ReactiveList\<T\>](#reactivelistt)
  - [ReactiveAwaitable](#reactiveawaitable)
- [Bindings](#bindings)
  - [How It Works](#how-it-works)
  - [Built-in Bindings](#built-in-bindings)
  - [Custom Bindings](#custom-bindings)
- [Lifecycle](#lifecycle)
  - [AbstractWidgetView](#abstractwidgetview)
  - [Unbind and Dispose](#unbind-and-dispose)
- [IWidgetViewFactory](#iwidgetviewfactory)
- [Working with Animations](#working-with-animations)
- [Editor Tools](#editor-tools)
- [Sample Project](#sample-project)
- [Cookbook](#cookbook)
- [Project Structure](#project-structure)

---

## Overview

The pattern splits UI into four layers:

| Layer | Responsibility | Dependencies |
|-------|---------------|--------------|
| **Model** | Domain data. Fires events on changes | Knows nothing about UI |
| **Widget** (business logic) | Listens to models and systems, transforms data, updates the ViewModel, handles events | Knows about Model and ViewModel |
| **ViewModel** | Hierarchical UI data model. Contains only state. Notifies about changes through reactive parameters | No external dependencies |
| **View** | Rendering. Listens to ViewModel and user input. Minimal logic | Knows only about its own ViewModel |

In classic MVVM, business logic lives inside ViewModels. Here it is extracted into a separate layer (Widget), which provides:

- A single place for subscriptions and event handling
- Business logic of a screen is easier to read "horizontally" in one place
- Ability to override behavior for custom windows without inheritance
- Model and View can be connected after the fact, when the View is already built and tested
- Feature logic can be tested independently from the UI

### Data Flow

```
Model ──► Widget ──► ViewModel ──► View
                                     │
Model ◄── Widget ◄── ViewModel ◄─────┘
                      (callbacks)
```

1. **Up:** data rises from Models, gets transformed in the Widget, and is rendered by the View.
2. **Down:** user input flows from the View through callbacks into the Widget, which modifies the Model or ViewModel.

---

## Architecture

### Model

A plain class without any framework base entities. Use `ObservableValue<T>` for reactive fields — it fires events when the value changes.

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

### Widget (Business Logic)

A class that encapsulates the business logic of a visual element. Connects the Model with the ViewModel and handles events. Using `sealed` is recommended for non-base entities.

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

        // Model → ViewModel (direct binding)
        Bind.From(_model.Score).To(_viewModel.Score);

        // Model → ViewModel (with transformation)
        Bind.From(_model.IntScore).To(_viewModel.PerformedScore, PerformScore);

        // Setting button callbacks
        _viewModel.OnAddElementButtonClicked.Value = _model.AddNewElement;
    }

    private static void PerformScore(int value, ReactiveValue<int> ctx) =>
        ctx.Value = value;
}
```

#### When to put logic in a Widget?

| Situation | Approach |
|-----------|----------|
| Little logic | A single Widget object is enough |
| Lots of logic | Still better in one Widget — easier to read and maintain |
| Logic needs to be reused across Views | Extract shared logic into controller classes or static methods |
| Logic needs to be shared across windows | Shared Model + systems. Systems are listened to by Widgets, which keep window-specific logic |
| Window with tabs | Separate ViewModels for each tab, tab logic in a dedicated class |

### ViewModel

Inherits from `AbstractViewModel`. Contains only data as reactive parameters. Its simplicity enables reuse with different Views.

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

`AbstractViewModel` automatically discovers all fields implementing `IReactiveValue` and manages their `Dispose()` and `Unbind()`.

**Nested ViewModels** are declared as fields:

```csharp
public sealed class ParentViewModel : AbstractViewModel
{
    public readonly ChildViewModel Child = new();
}
```

### View

Inherits from `AbstractWidgetView<TViewModel>`, extends `MonoBehaviour`. The View cannot set ViewModel values directly, but can invoke callbacks.

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

### Widget Composition

Large visual elements (windows, HUDs) can consist of several independent Widgets. Each Widget works autonomously: it has its own Model, ViewModel, and View.

Example — a HUD assembled from independent modules:

```
HudScreen
  ├── ResourcesWidget
  ├── PromoWidget
  └── QuestWidget
```

If a large feature consists of several windows, they can share a common Model, with shared business logic extracted into one or more systems. Widgets listen to those systems, keeping only window-specific logic.

---

## Reactive Types

### ObservableValue\<T\>

A reactive value for the **Model layer**. Notifies subscribers via a standard C# event `OnChanged`. Supports multiple subscribers.

```csharp
public class ObservableValue<T> : IObservableValue<T>
{
    public event Action<T> OnChanged;
    public T Value { get; set; }
}
```

The event only fires on actual value changes (checked via `EqualityComparer<T>.Default`).

### ReactiveValue\<T\>

A reactive value for the **ViewModel layer**. Supports exactly one subscription via the `Connect()` method. A single subscription ensures:

- Simple data flow debugging
- No synchronization issues
- A direct method call is faster than event broadcasting

```csharp
public class ReactiveValue<TValue> : IReactiveValue
{
    public TValue Value { get; set; }

    public void Connect(Action<TValue> onChanged);
    public void Unbind();
    public void Dispose();
}
```

**`Connect()` behavior:** the callback is invoked immediately upon connection if the value is not `null` (for reference types) or always (for value types). This guarantees **ViewModel data independence from the order of assignment** — the View will update all valid values right after binding.

Calling `Connect()` again while an existing subscription is active throws `InvalidOperationException`.

### ReactiveList\<T\>

A reactive list for the **ViewModel**. Synchronizes collection state and fires events only on actual changes. On the business logic side, it works like a regular `IList<T>`.

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

`Connect()` accepts four callbacks for full synchronization of the UI list with data.

### ReactiveAwaitable

Allows waiting for an asynchronous operation (animation, loading). Declares the wait intent in the ViewModel, while the View handles it via `TaskCompletionSource<bool>`.

```csharp
public class ReactiveAwaitable : IReactiveValue
{
    public void Connect(Action<TaskCompletionSource<bool>> onWaitingStarted);
    public async Task<bool> StartAsync();
    public void Unbind();
}
```

**Usage in Widget:**

```csharp
await elementVm.WaitForAnimation.StartAsync();
```

**Usage in View:**

```csharp
ViewModel.WaitForAnimation.Connect(StartAnimation);

private async void StartAnimation(TaskCompletionSource<bool> promise)
{
    await PlayAnimationAsync();
    promise.TrySetResult(true);
}
```

On `Unbind()`, an unfinished promise is automatically cancelled via `TrySetCanceled()`, and `StartAsync()` returns `true` (cancelled) instead of throwing an exception.

---

## Bindings

Bindings are an abstraction over reactive types. They:

- Automatically subscribe and unsubscribe from ViewModel events
- Encapsulate common value-setting logic in Views
- Provide a uniform approach for subscribing to different event sources

### How It Works

Each binding extends `AbstractEventBinding`. A list of such objects is formed, which can be activated, deactivated, or cleaned up in a loop.

Access the API via the `Bind` getter in `AbstractWidgetView` (or by creating an `EventBindingContext` manually in a Widget):

```
Bind.From(<source>).To(<target>)
```

Where `<source>` is the entity whose changes we subscribe to, and `<target>` is the entity receiving the result.

The target can be:
- **An object** — a default method is used to transform data
- **A lambda** — an event handler

### Built-in Bindings

#### Model → ViewModel (`ModelToViewModelEventBindExtensions`)

```csharp
// Direct binding: ObservableValue<T> → ReactiveValue<T>
Bind.From(model.Score).To(viewModel.Score);

// With transformation via context:
Bind.From(model.IntScore).To(viewModel.PerformedScore, (src, dest) => dest.Value = src);

// With callback:
Bind.From(model.Score).To(value => Debug.Log(value));
```

#### UI → ViewModel (`UIToViewModelEventBindExtensions`)

```csharp
// Button → reactive Action
Bind.From(_button).To(ViewModel.OnClicked);

// Button → direct callback
Bind.From(_button).To(() => Debug.Log("Clicked"));

// Button → callback with context
Bind.From(_button).To(someContext, ctx => HandleClick(ctx));

// Button collection → reactive Action
Bind.From(buttons).To(ViewModel.OnClicked);
```

#### ViewModel → UI (`ViewModelToUIEventBindExtensions`)

```csharp
// Nested ViewModel → child View
Bind.From(ViewModel.Child).To(_childView);

// ReactiveList → View list (with prefab and container)
Bind.From(ViewModel.Elements).To(_elementList, _prefab, _container);

// ReactiveList → View list (with factory)
Bind.From(ViewModel.Elements).To(_elementList, factory);

// ReactiveValue<string> → TMP_Text
Bind.From(ViewModel.Title).To(_titleText);

// ReactiveValue<int> → TMP_Text
Bind.From(ViewModel.Count).To(_countText);

// ReactiveValue<Color> → TMP_Text (color)
Bind.From(ViewModel.TextColor).To(_text);

// ReactiveValue<bool> → GameObject (SetActive)
Bind.From(ViewModel.IsVisible).To(_panel);

// ReactiveValue<int> → RectTransform (SetSiblingIndex)
Bind.From(ViewModel.Order).To(_rectTransform);
```

### Custom Bindings

The mechanism supports new bindings via extension methods named `To`:

```csharp
public static class CustomBindExtensions
{
    public static void To(this BindFrom<ReactiveValue<float>> from, Slider slider) =>
        from.Source.Connect(value => slider.value = value);
}
```

---

## Lifecycle

### AbstractWidgetView

`AbstractWidgetView<TViewModel>` is the base View class extending `MonoBehaviour`.

| Method | When Called | Purpose |
|--------|-----------|---------|
| `OnInitialized()` | Once on first `Connect()` | Data and bindings for the object's entire lifetime |
| `OnConnected()` | Every time on `Connect(vm)` | Binding ViewModel parameters to handlers |
| `OnDisposed()` | On `Dispose()` | Cleanup before pooling |

```
Connect(vm) ─► OnInitialized() (if first time)
             ─► CleanUp bindings
             ─► Dispose old ViewModel
             ─► OnConnected()

Dispose()    ─► CleanUp bindings
             ─► Unbind ViewModel
             ─► OnDisposed()
```

Bindings set up via `Bind.From().To()` are automatically destroyed on `Dispose()` or when a new ViewModel is connected.

### Unbind and Dispose

| Operation | Effect |
|-----------|--------|
| **Unbind** | Recursively clears callbacks of all ViewModel parameters. Data is preserved. Useful for destroying/unsubscribing part of a View while keeping ViewModel consistency |
| **Dispose** | Unbind + resets values to `default`. All entities except the Model implement `IReactiveValue` and should be cleaned up before being added to a pool |

Exception: nested ViewModels are disposed automatically. Old ViewModels are disposed when a new ViewModel is connected to the View.

---

## IWidgetViewFactory

An interface for creating/removing Views for `ReactiveList` elements:

```csharp
public interface IWidgetViewFactory<in TViewModel, TWidgetView>
    where TViewModel : AbstractViewModel, new()
    where TWidgetView : AbstractWidgetView<TViewModel>, new()
{
    TWidgetView CreateWidget(TViewModel viewModel);
    void RemoveWidget(TWidgetView view);
}
```

A simple factory with pooling:

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

A factory inside a View with pooling from layout (elements already placed in the scene):

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

## Working with Animations

Animations are best thought of as an asynchronous black box.

### When you don't need to wait for an animation

Start it in the View as a reaction to a ViewModel event:

```csharp
ViewModel.ShowEffect.Connect(value => _animator.SetTrigger("show"));
```

### When you need to wait for an animation — `ReactiveAwaitable`

**ViewModel** declares the intent:

```csharp
public sealed class WidgetViewModel : AbstractViewModel
{
    public readonly ReactiveValue<Action> OnButtonClicked = new();
    public readonly ReactiveAwaitable WaitForAnimation = new();
}
```

**Widget** controls execution order:

```csharp
private async Task DoAnimateAsync()
{
    await _viewModel.WaitForAnimation.StartAsync();
    // code after animation completes
}
```

**View** performs the animation and completes the promise:

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

### Via Callbacks

A `ReactiveValue<Action>` is created in the ViewModel and invoked at the right moment. On the business logic side, the callback is handled unambiguously:

```csharp
// ViewModel
public readonly ReactiveValue<Action> OnAnimationComplete = new();

// Widget
_viewModel.OnAnimationComplete.Value = HandleAnimationComplete;
```

---

## Editor Tools

### ViewModel Viewer

A window for real-time ViewModel inspection: **Window → ViewModel Viewer**.

Automatically finds active `AbstractWidgetView` instances in the scene (with `WidgetView` or `WindowView` suffix), extracts their ViewModels, and displays a tree of reactive parameters with current values.

### DevWidget

A component for previewing prefabs with ViewModels in Edit Mode. It allows you to:

- Instantiate a UI prefab and automatically connect a ViewModel
- Edit ViewModel values through the Inspector
- Save/Load ViewModel state as JSON

---

## Sample Project

A full example is located in `Samples~/Sample/`. To import in Unity, use Package Manager → Samples.

### Structure

```
Samples~/Sample/Assets/Scripts/
├── EntryScreen.cs           # Entry point: creates Model, ViewModel, Widget
├── SampleWidget.cs          # Business logic: connects Model with ViewModel
├── Model/
│   ├── SampleModel.cs       # Model with ObservableValue and events
│   └── ElementModel.cs      # List element model
└── View/
    ├── SampleWidgetView.cs  # View + SampleViewModel
    ├── ElementView.cs       # Element View + ElementViewModel
    ├── SliderViewModel.cs   # Slider ViewModel
    ├── AutoSliderView.cs    # One-way slider (display only)
    └── ManualSliderView.cs  # Two-way slider (with input)
```

### Data Flow in the Sample

```
EntryScreen
  ├─ creates SampleModel (ObservableValue<float> Score, ObservableValue<int> IntScore, List<ElementModel>)
  ├─ creates SampleViewModel
  ├─ creates SampleWidget.Connect(model, viewModel)
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

### How to create a new window?

1. Implement a ViewModel by inheriting from `AbstractViewModel`
2. Implement a View by inheriting from `AbstractWidgetView<TViewModel>`
3. Implement a Widget (a plain class) with business logic

### How to nest one View inside another?

In the ViewModel:

```csharp
public sealed class ParentViewModel : AbstractViewModel
{
    public readonly ChildViewModel Child = new();
}
```

In the View:

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

### How to use legacy components?

**Component is a leaf in the UI tree:**

Add it as a `[SerializeField]` in the View. Pass the required data through the ViewModel:

```csharp
protected override void OnConnected()
{
    ViewModel.Price.Connect(value => _legacyPriceView.Setup(value));
}
```

**Component is the root of a UI tree (an existing Widget View):**

1. In the component, create a ViewModel instance and define a reference to the Widget View
2. Bind the ViewModel to the Widget View
3. Encapsulate the ViewModel setup logic in the component

### How to decide what goes in Widget vs View?

Ask yourself: *is the data in the ViewModel sufficient to understand the current View state or to write unit tests?*

- If the interface breaks without the code → it goes in the Widget
- If the View state can be restored from the ViewModel → it goes in the View

Any external dependency in the View will complicate testing or isolated development.

### A View parameter depends on two ViewModel parameters?

Don't handle them separately — it leads to double work and non-obvious logic. Wrap them in a Tuple:

```csharp
public readonly ReactiveValue<(string label, int count)> LabelWithCount = new();
```

`ValueTuple` is a struct, so it won't cause extra allocations.

### Can a ViewModel inherit from another ViewModel?

No. Prefer composition. A ViewModel is a simple entity — it's easy to assemble complex objects from such building blocks.

### Can ViewModels be reused?

Yes. For example, a ViewModel with `IconId` and `Text` fields can be used with any View that can render an icon and text. If View requirements change, the ViewModel is easily replaced with a custom one.

---

## Project Structure

```
shtl-mvvm/
├── package.json                            # UPM package manifest
├── LICENSE                                 # MIT
├── .editorconfig                           # Code style
│
├── Runtime/                                # Shtl.Mvvm — core library
│   ├── Shtl.Mvvm.asmdef
│   ├── DevWidget.cs                        # Editor preview component
│   ├── Core/
│   │   ├── AbstractWidgetView.cs           # Base View class (MonoBehaviour)
│   │   ├── IWidgetView.cs                  # View interface
│   │   ├── Interfaces/
│   │   │   ├── IReactiveListCount.cs
│   │   │   ├── IEventBindingContext.cs
│   │   │   ├── IObservableValue.cs
│   │   │   ├── IViewModelParameter.cs      # Contains IReactiveValue
│   │   │   └── IWidgetViewFactory.cs
│   │   ├── Types/
│   │   │   ├── AbstractViewModel.cs        # Base ViewModel class
│   │   │   ├── ReactiveValue.cs            # Reactive value (1 subscriber)
│   │   │   ├── ReactiveList.cs             # Reactive list
│   │   │   ├── ReactiveAwaitable.cs        # Async awaitable
│   │   │   └── ObservableValue.cs          # Observable value (N subscribers)
│   │   └── Bindings/
│   │       ├── AbstractEventBinding.cs     # Base binding class
│   │       ├── BindFrom.cs                 # Fluent API: BindFrom<TSource>
│   │       ├── BindingPool.cs              # Binding object pool
│   │       ├── EventBindingContext.cs       # Binding storage context
│   │       ├── WidgetViewBinding.cs         # ViewModel → AbstractWidgetView
│   │       ├── ElementCollectionBinding.cs  # ReactiveList → View list
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
├── Editor/                                 # Shtl.Mvvm.Editor — tools
│   ├── Shtl.Mvvm.Editor.asmdef
│   ├── ViewModelViewerWindow.cs            # Window → ViewModel Viewer
│   ├── ViewModelDrawer.cs                  # ViewModel tree renderer
│   └── DevWidgetEditor.cs                  # DevWidget inspector
│
└── Samples~/                               # Sample project
    └── Sample/
        ├── Assets/
        │   ├── Scripts/                    # Sample source code
        │   ├── Scenes/mvvm_demo.unity
        │   └── Prefabs/
        └── Packages/manifest.json          # com.shtl.mvvm: "file:../../../"
```

### Dependencies

| Package | Version |
|---------|---------|
| `com.unity.textmeshpro` | 3.0.7 |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 |
