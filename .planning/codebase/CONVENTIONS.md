# Соглашения по коду

**Дата анализа:** 2026-04-09

## Именование

**Файлы:**
- Один класс/интерфейс = один файл. Имя файла совпадает с именем типа: `AbstractWidgetView.cs`, `ReactiveValue.cs`
- Исключение: ViewModel и View могут быть в одном файле, если View привязан к конкретному ViewModel (см. `Samples~/Sample/Assets/Scripts/View/ElementView.cs` - содержит `ElementViewModel` и `ElementView`)

**Пространства имен:**
- Корневое: `Shtl.Mvvm`
- Редактор: `Shtl.Mvvm.Editor`
- Примеры: `Shtl.Mvvm.Samples`
- Пространство имен соответствует расположению в структуре asmdef

**Классы и интерфейсы:**
- PascalCase для всех типов: `ReactiveValue`, `AbstractWidgetView`, `ElementCollectionBinding`
- Абстрактные классы начинаются с `Abstract`: `AbstractViewModel`, `AbstractWidgetView`, `AbstractEventBinding`
- Интерфейсы начинаются с `I`: `IReactiveValue`, `IObservableValue`, `IWidgetView`, `IEventBindingContext`
- Суффиксы по назначению:
  - `*ViewModel` - модели представления: `SampleViewModel`, `ElementViewModel`, `SliderViewModel`
  - `*View` - представления (MonoBehaviour): `SampleWidgetView`, `ElementView`, `AutoSliderView`
  - `*Widget` - контроллеры/связующие классы: `SampleWidget`, `DevWidget`
  - `*Binding` - привязки событий: `ButtonEventBinding`, `WidgetViewBinding`, `ElementCollectionBinding`
  - `*Model` - модели данных: `SampleModel`, `ElementModel`
  - `*Extensions` - классы расширений: `BindFromExtensions`, `ViewModelToUIEventBindingsExtensions`

**Методы:**
- PascalCase: `Connect()`, `Dispose()`, `Unbind()`, `Activate()`
- Приватные методы тоже PascalCase: `AddInternal()`, `RemoveAtInternal()`, `GetOrCreateListInternal()`
- Суффикс `Internal` для приватных вспомогательных методов: `AddInternal`, `RemoveAtInternal`, `GetOrCreateListInternal`
- Суффикс `Async` для асинхронных методов: `DoAnimateAsync()`, `StartAsync()`
- Обработчики событий с префиксом `On`: `OnConnected()`, `OnDisposed()`, `OnElementAdded()`, `OnContentChanged()`

**Поля:**
- Приватные поля с префиксом `_` и camelCase: `_value`, `_onChanged`, `_isBound`, `_list`
- Публичные readonly поля ViewModel в PascalCase: `Score`, `Elements`, `Title`, `OnButtonClicked`
- Статические приватные поля с префиксом `_`: `_defaultFactory`, `_typeToPoolHolder`, `_targetPerformedScope`
- SerializeField поля с префиксом `_`: `_scoreTitle`, `_button`, `_slider`

**Свойства:**
- PascalCase: `Value`, `Count`, `ViewModel`, `Key`

**Параметры типов:**
- Префикс `T`: `TValue`, `TElement`, `TViewModel`, `TWidgetView`, `TBinding`, `TSource`, `TContext`

## Стиль кода

**Форматирование:**
- Конфигурация: `.editorconfig` в корне проекта
- Отступы: 4 пробела
- Кодировка: UTF-8
- Конец строки: LF
- Финальный перевод строки: обязателен
- Удаление хвостовых пробелов: да

**Стиль фигурных скобок:**
- Allman (скобка на новой строке) для всех конструкций
- Исключение: expression-bodied members для однострочных методов/свойств
```csharp
// Многострочные блоки - Allman
public void Connect(Action<TValue> onChanged)
{
    var bound = _onChanged != null;
    if (bound)
    {
        throw new InvalidOperationException("Already bound");
    }
    _onChanged = onChanged;
}

// Однострочные - expression body
public override string ToString() => $"'{_value?.ToString()}'";
private List<TElement> GetOrCreateListInternal() => _list ??= new List<TElement>();
private void ClickHandler() => _buttonClickHandler?.Invoke(_context);
```

**Фигурные скобки обязательны:**
- Уровень: `warning` (`.editorconfig`: `csharp_prefer_braces = true:warning`)
- Даже для однострочных `if`/`while`/`foreach` используются фигурные скобки

**Использование `var`:**
- Предпочтительно `var` везде: `csharp_style_var_for_built_in_types = true:suggestion`
- Когда тип очевиден: `var binding = new EventBindingContext();`
- Для встроенных типов: `var index = _widgets.Count;`

**`using` директивы:**
- Размещение: вне пространства имен (`csharp_using_directive_placement = outside_namespace:warning`)

## Организация импортов

**Порядок:**
1. Системные (`System`, `System.Collections`, `System.Collections.Generic`, `System.Linq`, `System.Reflection`)
2. JetBrains аннотации (`JetBrains.Annotations`)
3. Unity (`UnityEngine`, `UnityEngine.UI`, `UnityEngine.UIElements`, `UnityEditor`)
4. Библиотеки (`TMPro`, `Newtonsoft.Json`)
5. Внутренние (`Shtl.Mvvm`)

**Пример из** `Runtime/Core/Types/ReactiveList.cs`:
```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
```

**Алиасы путей:**
- Нет алиасов - используются asmdef ссылки для разрешения зависимостей

## Обработка ошибок

**Паттерны:**
- `InvalidOperationException` для нарушения контракта (двойной bind):
```csharp
if (_isBound)
{
    throw new InvalidOperationException("Already bound");
}
```
- `Exception` для дублирования ключей в контексте привязок:
```csharp
if (!_keyToBinding.TryAdd(bindingKey, binding))
{
    throw new Exception($"The binding has already exists key: {bindingKey.ToString()}");
}
```
- `Assert.IsTrue` для runtime проверок в Unity:
```csharp
Assert.IsTrue(vm != ViewModel, "Something wrong. Connected view model must be not equals reset view model");
```
- Null-conditional оператор `?.` для безопасных вызовов callback-ов:
```csharp
_onChanged?.Invoke(value);
_onContentChanged?.Invoke(this);
```
- TaskCanceledException подавляется в `ReactiveAwaitable.SuppressCancellation()`

**Общий подход:**
- Исключения бросаются только при программных ошибках (двойной bind, дублирование ключей)
- Для опциональных операций используется null-safe вызов через `?.`
- Нет глобальной обработки ошибок - каждый компонент управляет своими ошибками локально

## Логирование

**Фреймворк:** `UnityEngine.Debug`

**Паттерны:**
- `Debug.Log()` для информационных сообщений в примерах: `Debug.Log("Animation animation started")`
- Строковая интерполяция: `Debug.Log($"StartAnimation in '{name}'")`
- В ядре библиотеки логирование не используется

## Комментарии

**Когда комментировать:**
- TODO-комментарии с авторством: `//TODO @a.shatalov: pass something keyable instead of object`
- TODO со ссылкой на задачу: `//TODO make editable https://app.asana.com/...`
- Пояснения сложной логики: `// Only unbinds the view model, does not destroy it`
- Комментарии к привязкам в бизнес-логике: `// Bind model value to view model`

**XML-документация:**
- Используется крайне редко. Единственный пример - `/// <returns>true if a structural rebuild is needed</returns>` в `Editor/ViewModelDrawer.cs`
- Для публичного API библиотеки документация отсутствует

## Дизайн функций

**Размер:** Методы компактные, обычно 5-20 строк. Длинные методы разбиваются на приватные хелперы (пример: `ElementCollectionBinding` с `OnContentChanged`, `OnElementAdded` и т.д.)

**Паттерн Fluent (цепочечные вызовы):**
- Методы `Connect()` и `SetContext()` возвращают `this` для fluent API:
```csharp
var binding = ButtonEventBinding<TContext>.GetOrCreate()
    .Connect(from.Source, onButtonClicked)
    .SetContext(actionContext);
```

**Expression-bodied methods:**
- Для однострочных методов: `public int Count => _list?.Count ?? 0;`
- Для делегирующих вызовов: `public static void Release(...) => ...`

**Параметры:**
- Именованные параметры при множественных callback-ах:
```csharp
_vmList.Connect(
    onContentChanged : OnContentChanged,
    onElementAdded : OnElementAdded,
    onElementReplaced : OnElementReplaced,
    onElementRemoved : OnElementRemoved
);
```

## Дизайн модулей

**Assembly definitions (asmdef):**
- `Runtime/Shtl.Mvvm.asmdef` - основная сборка
- `Editor/Shtl.Mvvm.Editor.asmdef` - редакторская сборка (зависит от Shtl.Mvvm, ограничена платформой Editor)

**Экспорт:**
- Публичные типы для API библиотеки: `ReactiveValue<T>`, `AbstractWidgetView<T>`, `AbstractViewModel`
- `internal` для деталей реализации: `BindingPool`, `ButtonEventSimpleBinding`
- Extension-методы сгруппированы по направлению потока данных:
  - `ViewModelToUIEventBindingsExtensions` (ViewModel -> UI)
  - `UIToViewModelEventBindingsExtensions` (UI -> ViewModel)
  - `ModelToViewModelEventBindingsExtensions` (Model -> ViewModel)
  - `BindFromExtensions` (общие)

**Barrel-файлы:** Не используются. Все типы в одном пространстве имён `Shtl.Mvvm`.

## Паттерны проектирования

**Object Pool:**
- `BindingPool` - пул привязок для повторного использования через `Get<T>()` / `Release()`
- Фабричный метод `GetOrCreate()` на абстрактном классе

**Observer/Reactive:**
- `ReactiveValue<T>` - одиночный подписчик через `Connect()` (бросает исключение при повторном bind)
- `ObservableValue<T>` - множественные подписчики через событие `OnChanged`
- `ReactiveList<T>` - уведомления о добавлении/удалении/замене элементов

**MVVM:**
- Model (чистые данные + ObservableValue) -> Widget (контроллер) -> ViewModel (ReactiveValue) -> View (MonoBehaviour)
- View привязывает ViewModel-поля к UI-элементам в `OnConnected()`

**Условная компиляция:**
- `#if UNITY_EDITOR` для Editor-only кода в Runtime-классах (см. `DevWidget.cs`)
- `#if UNITY_2023_1_OR_NEWER` для совместимости API между версиями Unity

## Атрибуты

**Unity атрибуты:**
- `[SerializeField]` для приватных полей MonoBehaviour
- `[DefaultExecutionOrder(-1)]` для порядка инициализации
- `[ExecuteInEditMode]`, `[DisallowMultipleComponent]` для Editor-специфичных компонентов
- `[CustomEditor(typeof(...))]` для кастомных инспекторов

**JetBrains атрибуты:**
- `[NotNull]` на классе `ReactiveValue<T>`
- `[CanBeNull]` на nullable полях: `[CanBeNull] private List<TElement> _list`
- `[UsedImplicitly]` для методов, вызываемых через рефлексию: `[UsedImplicitly, InitializeOnLoadMethod]`

**Системные атрибуты:**
- `[MethodImpl(MethodImplOptions.AggressiveInlining)]` для горячих приватных методов: `AddInternal`, `RemoveAtInternal`
- `[Serializable]` для классов, сериализуемых Unity

---

*Анализ соглашений: 2026-04-09*
