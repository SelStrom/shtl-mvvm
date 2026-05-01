# Архитектура

**Дата анализа:** 2026-04-09

## Обзор паттерна

**Общий паттерн:** MVVM (Model-View-ViewModel) для Unity

**Ключевые характеристики:**
- Реактивные привязки данных (data binding) через систему `ReactiveValue<T>` и `ReactiveList<T>`
- Однонаправленный поток данных: Model → ViewModel → View, с обратной связью через UI-события (кнопки)
- Пулирование объектов привязок (`BindingPool`) для минимизации аллокаций
- Fluent API для связывания через extension-методы (`Bind.From(...).To(...)`)
- Разделение на runtime-библиотеку (UPM-пакет) и editor-инструменты

## Слои

**Model (Модель данных):**
- Назначение: Хранит бизнес-данные приложения, не зависит от Unity UI
- Расположение: Определяется потребителем пакета (в примере: `Samples~/Sample/Assets/Scripts/Model/`)
- Содержит: Классы с полями типа `ObservableValue<T>`, события для уведомления о структурных изменениях
- Зависит от: `ObservableValue<T>` из `Runtime/Core/Types/ObservableValue.cs`
- Используется: слоем Widget (контроллер/презентер)

**ViewModel (Модель представления):**
- Назначение: Описывает состояние UI через реактивные поля, не знает о конкретных UI-элементах
- Расположение: Определяется потребителем пакета (в примере: `Samples~/Sample/Assets/Scripts/View/SampleWidgetView.cs`, `Samples~/Sample/Assets/Scripts/View/ElementView.cs`)
- Содержит: Классы-наследники `AbstractViewModel` с полями `ReactiveValue<T>`, `ReactiveList<T>`, `ReactiveAwaitable`
- Зависит от: `Runtime/Core/Types/AbstractViewModel.cs`, `Runtime/Core/Types/ReactiveValue.cs`, `Runtime/Core/Types/ReactiveList.cs`
- Используется: слоями View и Widget

**View (Представление):**
- Назначение: Unity MonoBehaviour, связывает ViewModel с конкретными UI-элементами
- Расположение: Определяется потребителем пакета (в примере: `Samples~/Sample/Assets/Scripts/View/`)
- Содержит: Классы-наследники `AbstractWidgetView<TViewModel>` с `[SerializeField]` ссылками на UI
- Зависит от: `Runtime/Core/AbstractWidgetView.cs`, система привязок из `Runtime/Core/Bindings/`
- Используется: слоем Widget для подключения ViewModel

**Widget (Контроллер/Презентер):**
- Назначение: Связывает Model и ViewModel, содержит бизнес-логику взаимодействия
- Расположение: Определяется потребителем пакета (в примере: `Samples~/Sample/Assets/Scripts/SampleWidget.cs`)
- Содержит: Логику привязки полей Model к ViewModel через `Bind.From(...).To(...)`
- Зависит от: Model, ViewModel, система привязок
- Используется: точкой входа (EntryScreen)

**Binding System (Система привязок — ядро фреймворка):**
- Назначение: Реактивное связывание источников данных с приёмниками
- Расположение: `Runtime/Core/Bindings/`, `Runtime/Utils/`
- Содержит: `AbstractEventBinding`, конкретные типы привязок, `BindingPool`, `EventBindingContext`, extension-методы
- Зависит от: Unity UI (`UnityEngine.UI.Button`, `TMPro.TMP_Text`)
- Используется: всеми слоями через fluent API

**Editor Tools (Инструменты редактора):**
- Назначение: Визуализация и редактирование ViewModel в Unity Editor
- Расположение: `Editor/`
- Содержит: Custom Inspector для `DevWidget`, окно ViewModel Viewer, рисовальщик ViewModel через рефлексию
- Зависит от: `Shtl.Mvvm` runtime assembly, Unity Editor API, Newtonsoft.Json
- Используется: разработчиками в Unity Editor

## Поток данных

**Model → ViewModel (привязка данных):**

1. `ObservableValue<T>` в Model изменяет значение через setter `Value`
2. Событие `OnChanged` вызывает callback в `ObservableValueEventBinding`
3. Callback обновляет `ReactiveValue<T>.Value` в ViewModel
4. `ReactiveValue<T>` уведомляет подключённый через `Connect()` callback

**ViewModel → View (отображение):**

1. `ReactiveValue<T>.Value` изменяется (из Widget или привязки)
2. Если новое значение отличается от текущего, вызывается `_onChanged` callback
3. Callback обновляет UI-элемент (например, `TMP_Text.text`, `GameObject.SetActive`)

**View → ViewModel (UI-события):**

1. Пользователь нажимает `Button`
2. `ButtonEventBinding` перехватывает `onClick`
3. Вызывается `Action`, хранящийся в `ReactiveValue<Action>` во ViewModel
4. Widget обрабатывает действие (изменяет Model или ViewModel)

**Управление состоянием:**
- Каждый `ReactiveValue<T>` хранит одно значение и один callback (1:1 привязка)
- `ReactiveList<T>` поддерживает callbacks для добавления, замены, удаления и общего изменения содержимого
- `ReactiveAwaitable` позволяет async/await взаимодействие между View и Widget через `TaskCompletionSource<bool>`
- `ObservableValue<T>` использует event-паттерн (множественные подписчики) для Model-слоя

## Ключевые абстракции

**IReactiveValue:**
- Назначение: Единый интерфейс для всех реактивных типов (Dispose/Unbind)
- Примеры: `Runtime/Core/Interfaces/IViewModelParameter.cs`
- Паттерн: `AbstractViewModel` автоматически собирает все `IReactiveValue` поля через рефлексию в конструкторе

**AbstractEventBinding:**
- Назначение: Базовый класс для всех привязок с lifecycle (Activate/Invoke/Dispose)
- Примеры: `Runtime/Core/Bindings/AbstractEventBinding.cs`
- Паттерн: Generic self-referencing (`AbstractEventBinding<TBinding>`) + object pooling через `BindingPool`

**AbstractWidgetView<TViewModel>:**
- Назначение: Базовый MonoBehaviour для View-компонентов
- Примеры: `Runtime/Core/AbstractWidgetView.cs`
- Паттерн: Template Method — `OnInitialized()`, `OnConnected()`, `OnDisposed()`

**BindFrom<TSource>:**
- Назначение: Fluent API entry point для создания привязок
- Примеры: `Runtime/Core/Bindings/BindFrom.cs`
- Паттерн: Builder — `Bind.From(source).To(target)` через extension-методы

**EventBindingContext:**
- Назначение: Контейнер привязок с возможностью массовой очистки
- Примеры: `Runtime/Core/Bindings/EventBindingContext.cs`
- Паттерн: Registry — хранит Dictionary<object, AbstractEventBinding>, CleanUp() возвращает все привязки в пул

## Точки входа

**Runtime (для потребителей пакета):**
- Расположение: `Runtime/Core/AbstractWidgetView.cs` — метод `Connect(TViewModel vm)`
- Триггер: Вызывается из кода приложения при инициализации экрана
- Обязанности: Инициализация View, очистка предыдущих привязок, подключение нового ViewModel

**Editor DevWidget:**
- Расположение: `Runtime/DevWidget.cs`
- Триггер: Нажатие кнопки "Open prefab" в Inspector
- Обязанности: Инстанцирование UI-префаба, создание ViewModel через рефлексию, инъекция через `Connect()`

**Editor ViewModel Viewer:**
- Расположение: `Editor/ViewModelViewerWindow.cs`
- Триггер: Меню Window → ViewModel Viewer
- Обязанности: Поиск активных WidgetView в сцене, отображение их ViewModel в реальном времени

## Обработка ошибок

**Стратегия:** Defensive + Assertions

**Паттерны:**
- `ReactiveValue<T>.Connect()` выбрасывает `InvalidOperationException` при повторном подключении (защита от двойной привязки)
- `ReactiveList<T>.Connect()` аналогично проверяет `_isBound` флаг
- `AbstractWidgetView.Connect()` использует `Assert.IsTrue()` для проверки что новый ViewModel отличается от текущего
- `EventBindingContext.AddBinding()` выбрасывает `Exception` при дублировании ключа привязки
- `ReactiveAwaitable` использует `SuppressCancellation()` для безопасной обработки отмены Task

## Сквозные аспекты

**Пулирование объектов:**
- `BindingPool` (`Runtime/Core/Bindings/BindingPool.cs`) — статический пул для всех типов `AbstractEventBinding`
- При `CleanUp()` контекста привязки возвращаются в пул через `BindingPool.Release()`
- Типизированные стеки `Stack<TBinding>` для каждого конкретного типа привязки

**Рефлексия:**
- `AbstractViewModel` конструктор сканирует поля для автоматической регистрации `IReactiveValue`
- `DevWidget` и `ViewModelDrawer` используют рефлексию для инспекции ViewModel в Editor
- `ViewModelViewerWindow` находит активные WidgetView через поиск компонентов в сцене

**Сериализация (Editor-only):**
- `DevWidgetEditor` использует Newtonsoft.Json для сохранения/загрузки ViewModel в JSON
- Настройки: `snake_case` naming strategy, `NullValueHandling.Ignore`, отступы

---

*Анализ архитектуры: 2026-04-09*
