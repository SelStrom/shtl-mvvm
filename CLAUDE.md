<!-- GSD:project-start source:PROJECT.md -->
## Project

**Shtl.Mvvm**

MVVM-фреймворк для Unity с чётким разделением слоёв: Model, Widget, ViewModel, View. UPM-пакет (`com.shtl.mvvm`), совместимый с Unity 2020.3+. Используется в личном проекте с небольшой командой, в перспективе — публичный open source пакет.

**Core Value:** Простой и предсказуемый fluent API биндингов (`Bind.From().To()`), который позволяет декларативно связывать данные с UI без бойлерплейта.

### Constraints

- **Совместимость**: Unity 2020.3+ — нельзя использовать API, недоступные в старых версиях
- **uGUI**: Работа с uGUI (UnityEngine.UI), не UIToolkit
- **Zero-alloc**: Минимизация аллокаций в hot path (binding pool, структуры вместо классов где возможно)
- **Обратная совместимость API**: Существующий `Bind.From().To()` API не должен ломаться
<!-- GSD:project-end -->

<!-- GSD:stack-start source:codebase/STACK.md -->
## Technology Stack

## Языки
- C# — весь исходный код Runtime, Editor и Samples
- JSON — конфигурация пакета (`package.json`), манифесты Unity, сериализация ViewModel
## Среда выполнения
- Unity 2020.3+ (минимальная поддерживаемая версия указана в `package.json`)
- Совместимость с Unity 6 (коммит `5802404` заменил TextMeshPro на ugui для совместимости)
- Unity Package Manager (UPM) — установка через git URL
- Имя пакета: `com.shtl.mvvm`
- Версия: `1.1.0`
## Фреймворки
- Unity Engine — игровой движок, MonoBehaviour как базовый класс для View
- Собственный MVVM-фреймворк — `Shtl.Mvvm` (это и есть данный проект)
- Unity uGUI (`com.unity.ugui` 1.0.0) — `UnityEngine.UI.Button`, `RectTransform`, `GameObject.SetActive`
- TextMeshPro (`Unity.TextMeshPro`) — `TMP_Text` для биндингов текста (ссылка в `Runtime/Shtl.Mvvm.asmdef`)
- Unity Editor UI Toolkit (`UnityEngine.UIElements`, `UnityEditor.UIElements`) — для кастомных инспекторов
- Newtonsoft.Json (`com.unity.nuget.newtonsoft-json` 3.2.2) — сериализация/десериализация ViewModel в DevWidget Editor
- Директории `Tests/Runtime/` и `Tests/Editor/` существуют, но пусты — тесты не реализованы
## Ключевые зависимости
- `com.unity.ugui` 1.0.0 — система Unity UI, используется для биндингов кнопок и UI-элементов
- `com.unity.nuget.newtonsoft-json` 3.2.2 — JSON-сериализация для DevWidget (сохранение/загрузка ViewModel)
- `Unity.TextMeshPro` — ссылка в `Runtime/Shtl.Mvvm.asmdef`, используется в биндингах `ViewModelToUIEventBindExtensions`
- `JetBrains.Annotations` — используется в `Runtime/DevWidget.cs`, `Runtime/Core/Types/ReactiveValue.cs`, `Runtime/Core/Types/ReactiveList.cs`
## Сборки (Assembly Definitions)
- `Runtime/Shtl.Mvvm.asmdef` — корневое пространство имён `Shtl.Mvvm`
- `Editor/Shtl.Mvvm.Editor.asmdef` — пространство имён `Shtl.Mvvm.Editor`
- `Samples~/Sample/Assets/Scripts/Sample.asmdef`
## Конфигурация
- `.editorconfig` — Allman brace style, indent 4 пробела, `csharp_prefer_braces = true:warning`
- Полный набор C# правил форматирования
- `package.json` — UPM манифест с метаданными пакета
- MIT (`LICENSE`)
## Требования к платформе
- Unity 2020.3 или новее
- Любая ОС, поддерживаемая Unity Editor
- Все платформы, поддерживаемые Unity (пакет не ограничивает `includePlatforms` в Runtime asmdef)
<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->
## Conventions

## Именование
- Один класс/интерфейс = один файл. Имя файла совпадает с именем типа: `AbstractWidgetView.cs`, `ReactiveValue.cs`
- Исключение: ViewModel и View могут быть в одном файле, если View привязан к конкретному ViewModel (см. `Samples~/Sample/Assets/Scripts/View/ElementView.cs` - содержит `ElementViewModel` и `ElementView`)
- Корневое: `Shtl.Mvvm`
- Редактор: `Shtl.Mvvm.Editor`
- Примеры: `Shtl.Mvvm.Samples`
- Пространство имен соответствует расположению в структуре asmdef
- PascalCase для всех типов: `ReactiveValue`, `AbstractWidgetView`, `ElementCollectionBinding`
- Абстрактные классы начинаются с `Abstract`: `AbstractViewModel`, `AbstractWidgetView`, `AbstractEventBinding`
- Интерфейсы начинаются с `I`: `IReactiveValue`, `IObservableValue`, `IWidgetView`, `IEventBindingContext`
- Суффиксы по назначению:
- PascalCase: `Connect()`, `Dispose()`, `Unbind()`, `Activate()`
- Приватные методы тоже PascalCase: `AddInternal()`, `RemoveAtInternal()`, `GetOrCreateListInternal()`
- Суффикс `Internal` для приватных вспомогательных методов: `AddInternal`, `RemoveAtInternal`, `GetOrCreateListInternal`
- Суффикс `Async` для асинхронных методов: `DoAnimateAsync()`, `StartAsync()`
- Обработчики событий с префиксом `On`: `OnConnected()`, `OnDisposed()`, `OnElementAdded()`, `OnContentChanged()`
- Приватные поля с префиксом `_` и camelCase: `_value`, `_onChanged`, `_isBound`, `_list`
- Публичные readonly поля ViewModel в PascalCase: `Score`, `Elements`, `Title`, `OnButtonClicked`
- Статические приватные поля с префиксом `_`: `_defaultFactory`, `_typeToPoolHolder`, `_targetPerformedScope`
- SerializeField поля с префиксом `_`: `_scoreTitle`, `_button`, `_slider`
- PascalCase: `Value`, `Count`, `ViewModel`, `Key`
- Префикс `T`: `TValue`, `TElement`, `TViewModel`, `TWidgetView`, `TBinding`, `TSource`, `TContext`
## Стиль кода
- Конфигурация: `.editorconfig` в корне проекта
- Отступы: 4 пробела
- Кодировка: UTF-8
- Конец строки: LF
- Финальный перевод строки: обязателен
- Удаление хвостовых пробелов: да
- Allman (скобка на новой строке) для всех конструкций
- Исключение: expression-bodied members для однострочных методов/свойств
- Уровень: `warning` (`.editorconfig`: `csharp_prefer_braces = true:warning`)
- Даже для однострочных `if`/`while`/`foreach` используются фигурные скобки
- Предпочтительно `var` везде: `csharp_style_var_for_built_in_types = true:suggestion`
- Когда тип очевиден: `var binding = new EventBindingContext();`
- Для встроенных типов: `var index = _widgets.Count;`
- Размещение: вне пространства имен (`csharp_using_directive_placement = outside_namespace:warning`)
## Организация импортов
- Нет алиасов - используются asmdef ссылки для разрешения зависимостей
## Обработка ошибок
- `InvalidOperationException` для нарушения контракта (двойной bind):
- `Exception` для дублирования ключей в контексте привязок:
- `Assert.IsTrue` для runtime проверок в Unity:
- Null-conditional оператор `?.` для безопасных вызовов callback-ов:
- TaskCanceledException подавляется в `ReactiveAwaitable.SuppressCancellation()`
- Исключения бросаются только при программных ошибках (двойной bind, дублирование ключей)
- Для опциональных операций используется null-safe вызов через `?.`
- Нет глобальной обработки ошибок - каждый компонент управляет своими ошибками локально
## Логирование
- `Debug.Log()` для информационных сообщений в примерах: `Debug.Log("Animation animation started")`
- Строковая интерполяция: `Debug.Log($"StartAnimation in '{name}'")`
- В ядре библиотеки логирование не используется
## Комментарии
- TODO-комментарии с авторством: `//TODO @a.shatalov: pass something keyable instead of object`
- TODO со ссылкой на задачу: `//TODO make editable https://app.asana.com/...`
- Пояснения сложной логики: `// Only unbinds the view model, does not destroy it`
- Комментарии к привязкам в бизнес-логике: `// Bind model value to view model`
- Используется крайне редко. Единственный пример - `/// <returns>true if a structural rebuild is needed</returns>` в `Editor/ViewModelDrawer.cs`
- Для публичного API библиотеки документация отсутствует
## Дизайн функций
- Методы `Connect()` и `SetContext()` возвращают `this` для fluent API:
- Для однострочных методов: `public int Count => _list?.Count ?? 0;`
- Для делегирующих вызовов: `public static void Release(...) => ...`
- Именованные параметры при множественных callback-ах:
## Дизайн модулей
- `Runtime/Shtl.Mvvm.asmdef` - основная сборка
- `Editor/Shtl.Mvvm.Editor.asmdef` - редакторская сборка (зависит от Shtl.Mvvm, ограничена платформой Editor)
- Публичные типы для API библиотеки: `ReactiveValue<T>`, `AbstractWidgetView<T>`, `AbstractViewModel`
- `internal` для деталей реализации: `BindingPool`, `ButtonEventSimpleBinding`
- Extension-методы сгруппированы по направлению потока данных:
## Паттерны проектирования
- `BindingPool` - пул привязок для повторного использования через `Get<T>()` / `Release()`
- Фабричный метод `GetOrCreate()` на абстрактном классе
- `ReactiveValue<T>` - одиночный подписчик через `Connect()` (бросает исключение при повторном bind)
- `ObservableValue<T>` - множественные подписчики через событие `OnChanged`
- `ReactiveList<T>` - уведомления о добавлении/удалении/замене элементов
- Model (чистые данные + ObservableValue) -> Widget (контроллер) -> ViewModel (ReactiveValue) -> View (MonoBehaviour)
- View привязывает ViewModel-поля к UI-элементам в `OnConnected()`
- `#if UNITY_EDITOR` для Editor-only кода в Runtime-классах (см. `DevWidget.cs`)
- `#if UNITY_2023_1_OR_NEWER` для совместимости API между версиями Unity
## Атрибуты
- `[SerializeField]` для приватных полей MonoBehaviour
- `[DefaultExecutionOrder(-1)]` для порядка инициализации
- `[ExecuteInEditMode]`, `[DisallowMultipleComponent]` для Editor-специфичных компонентов
- `[CustomEditor(typeof(...))]` для кастомных инспекторов
- `[NotNull]` на классе `ReactiveValue<T>`
- `[CanBeNull]` на nullable полях: `[CanBeNull] private List<TElement> _list`
- `[UsedImplicitly]` для методов, вызываемых через рефлексию: `[UsedImplicitly, InitializeOnLoadMethod]`
- `[MethodImpl(MethodImplOptions.AggressiveInlining)]` для горячих приватных методов: `AddInternal`, `RemoveAtInternal`
- `[Serializable]` для классов, сериализуемых Unity
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->
## Architecture

## Обзор паттерна
- Реактивные привязки данных (data binding) через систему `ReactiveValue<T>` и `ReactiveList<T>`
- Однонаправленный поток данных: Model → ViewModel → View, с обратной связью через UI-события (кнопки)
- Пулирование объектов привязок (`BindingPool`) для минимизации аллокаций
- Fluent API для связывания через extension-методы (`Bind.From(...).To(...)`)
- Разделение на runtime-библиотеку (UPM-пакет) и editor-инструменты
## Слои
- Назначение: Хранит бизнес-данные приложения, не зависит от Unity UI
- Расположение: Определяется потребителем пакета (в примере: `Samples~/Sample/Assets/Scripts/Model/`)
- Содержит: Классы с полями типа `ObservableValue<T>`, события для уведомления о структурных изменениях
- Зависит от: `ObservableValue<T>` из `Runtime/Core/Types/ObservableValue.cs`
- Используется: слоем Widget (контроллер/презентер)
- Назначение: Описывает состояние UI через реактивные поля, не знает о конкретных UI-элементах
- Расположение: Определяется потребителем пакета (в примере: `Samples~/Sample/Assets/Scripts/View/SampleWidgetView.cs`, `Samples~/Sample/Assets/Scripts/View/ElementView.cs`)
- Содержит: Классы-наследники `AbstractViewModel` с полями `ReactiveValue<T>`, `ReactiveList<T>`, `ReactiveAwaitable`
- Зависит от: `Runtime/Core/Types/AbstractViewModel.cs`, `Runtime/Core/Types/ReactiveValue.cs`, `Runtime/Core/Types/ReactiveList.cs`
- Используется: слоями View и Widget
- Назначение: Unity MonoBehaviour, связывает ViewModel с конкретными UI-элементами
- Расположение: Определяется потребителем пакета (в примере: `Samples~/Sample/Assets/Scripts/View/`)
- Содержит: Классы-наследники `AbstractWidgetView<TViewModel>` с `[SerializeField]` ссылками на UI
- Зависит от: `Runtime/Core/AbstractWidgetView.cs`, система привязок из `Runtime/Core/Bindings/`
- Используется: слоем Widget для подключения ViewModel
- Назначение: Связывает Model и ViewModel, содержит бизнес-логику взаимодействия
- Расположение: Определяется потребителем пакета (в примере: `Samples~/Sample/Assets/Scripts/SampleWidget.cs`)
- Содержит: Логику привязки полей Model к ViewModel через `Bind.From(...).To(...)`
- Зависит от: Model, ViewModel, система привязок
- Используется: точкой входа (EntryScreen)
- Назначение: Реактивное связывание источников данных с приёмниками
- Расположение: `Runtime/Core/Bindings/`, `Runtime/Utils/`
- Содержит: `AbstractEventBinding`, конкретные типы привязок, `BindingPool`, `EventBindingContext`, extension-методы
- Зависит от: Unity UI (`UnityEngine.UI.Button`, `TMPro.TMP_Text`)
- Используется: всеми слоями через fluent API
- Назначение: Визуализация и редактирование ViewModel в Unity Editor
- Расположение: `Editor/`
- Содержит: Custom Inspector для `DevWidget`, окно ViewModel Viewer, рисовальщик ViewModel через рефлексию
- Зависит от: `Shtl.Mvvm` runtime assembly, Unity Editor API, Newtonsoft.Json
- Используется: разработчиками в Unity Editor
## Поток данных
- Каждый `ReactiveValue<T>` хранит одно значение и один callback (1:1 привязка)
- `ReactiveList<T>` поддерживает callbacks для добавления, замены, удаления и общего изменения содержимого
- `ReactiveAwaitable` позволяет async/await взаимодействие между View и Widget через `TaskCompletionSource<bool>`
- `ObservableValue<T>` использует event-паттерн (множественные подписчики) для Model-слоя
## Ключевые абстракции
- Назначение: Единый интерфейс для всех реактивных типов (Dispose/Unbind)
- Примеры: `Runtime/Core/Interfaces/IViewModelParameter.cs`
- Паттерн: `AbstractViewModel` автоматически собирает все `IReactiveValue` поля через рефлексию в конструкторе
- Назначение: Базовый класс для всех привязок с lifecycle (Activate/Invoke/Dispose)
- Примеры: `Runtime/Core/Bindings/AbstractEventBinding.cs`
- Паттерн: Generic self-referencing (`AbstractEventBinding<TBinding>`) + object pooling через `BindingPool`
- Назначение: Базовый MonoBehaviour для View-компонентов
- Примеры: `Runtime/Core/AbstractWidgetView.cs`
- Паттерн: Template Method — `OnInitialized()`, `OnConnected()`, `OnDisposed()`
- Назначение: Fluent API entry point для создания привязок
- Примеры: `Runtime/Core/Bindings/BindFrom.cs`
- Паттерн: Builder — `Bind.From(source).To(target)` через extension-методы
- Назначение: Контейнер привязок с возможностью массовой очистки
- Примеры: `Runtime/Core/Bindings/EventBindingContext.cs`
- Паттерн: Registry — хранит Dictionary<object, AbstractEventBinding>, CleanUp() возвращает все привязки в пул
## Точки входа
- Расположение: `Runtime/Core/AbstractWidgetView.cs` — метод `Connect(TViewModel vm)`
- Триггер: Вызывается из кода приложения при инициализации экрана
- Обязанности: Инициализация View, очистка предыдущих привязок, подключение нового ViewModel
- Расположение: `Runtime/DevWidget.cs`
- Триггер: Нажатие кнопки "Open prefab" в Inspector
- Обязанности: Инстанцирование UI-префаба, создание ViewModel через рефлексию, инъекция через `Connect()`
- Расположение: `Editor/ViewModelViewerWindow.cs`
- Триггер: Меню Window → ViewModel Viewer
- Обязанности: Поиск активных WidgetView в сцене, отображение их ViewModel в реальном времени
## Обработка ошибок
- `ReactiveValue<T>.Connect()` выбрасывает `InvalidOperationException` при повторном подключении (защита от двойной привязки)
- `ReactiveList<T>.Connect()` аналогично проверяет `_isBound` флаг
- `AbstractWidgetView.Connect()` использует `Assert.IsTrue()` для проверки что новый ViewModel отличается от текущего
- `EventBindingContext.AddBinding()` выбрасывает `Exception` при дублировании ключа привязки
- `ReactiveAwaitable` использует `SuppressCancellation()` для безопасной обработки отмены Task
## Сквозные аспекты
- `BindingPool` (`Runtime/Core/Bindings/BindingPool.cs`) — статический пул для всех типов `AbstractEventBinding`
- При `CleanUp()` контекста привязки возвращаются в пул через `BindingPool.Release()`
- Типизированные стеки `Stack<TBinding>` для каждого конкретного типа привязки
- `AbstractViewModel` конструктор сканирует поля для автоматической регистрации `IReactiveValue`
- `DevWidget` и `ViewModelDrawer` используют рефлексию для инспекции ViewModel в Editor
- `ViewModelViewerWindow` находит активные WidgetView через поиск компонентов в сцене
- `DevWidgetEditor` использует Newtonsoft.Json для сохранения/загрузки ViewModel в JSON
- Настройки: `snake_case` naming strategy, `NullValueHandling.Ignore`, отступы
<!-- GSD:architecture-end -->

<!-- GSD:skills-start source:skills/ -->
## Project Skills

No project skills found. Add skills to any of: `.claude/skills/`, `.agents/skills/`, `.cursor/skills/`, or `.github/skills/` with a `SKILL.md` index file.
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->
## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:
- `/gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd-debug` for investigation and bug fixing
- `/gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->



<!-- GSD:profile-start -->
## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
