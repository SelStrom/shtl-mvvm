# Структура кодовой базы

**Дата анализа:** 2026-04-09

## Схема директорий

```
shtl-mvvm/                          # Корень UPM-пакета
├── Runtime/                        # Runtime-код фреймворка (assembly: Shtl.Mvvm)
│   ├── Core/                       # Ядро MVVM-фреймворка
│   │   ├── Bindings/               # Все типы привязок и инфраструктура
│   │   ├── Interfaces/             # Публичные интерфейсы
│   │   ├── Types/                  # Реактивные типы данных и базовый ViewModel
│   │   ├── AbstractWidgetView.cs   # Базовый View-компонент
│   │   └── IWidgetView.cs          # Интерфейс View
│   ├── Utils/                      # Extension-методы для fluent API привязок
│   ├── DevWidget.cs                # Компонент для отладки виджетов в Editor
│   └── Shtl.Mvvm.asmdef            # Assembly definition (runtime)
├── Editor/                         # Editor-инструменты (assembly: Shtl.Mvvm.Editor)
│   ├── DevWidgetEditor.cs          # Custom Inspector для DevWidget
│   ├── ViewModelDrawer.cs          # Рисовальщик полей ViewModel через рефлексию
│   ├── ViewModelViewerWindow.cs    # EditorWindow для просмотра активных ViewModel
│   └── Shtl.Mvvm.Editor.asmdef     # Assembly definition (editor-only)
├── Tests/                          # Тесты (пустые директории)
│   ├── Runtime/                    # Runtime-тесты (пусто)
│   └── Editor/                     # Editor-тесты (пусто)
├── Samples~/                       # Примеры использования (скрыты от Unity)
│   └── Sample/                     # Полноценный Unity-проект с демонстрацией
│       └── Assets/Scripts/         # Пример MVVM-приложения
│           ├── Model/              # Модели данных
│           ├── View/               # View и ViewModel классы
│           ├── EntryScreen.cs      # Точка входа примера
│           └── SampleWidget.cs     # Widget (контроллер)
├── package.json                    # UPM-манифест пакета
├── README.md                       # Документация (EN)
├── README.ru.md                    # Документация (RU)
└── LICENSE                         # MIT License
```

## Назначение директорий

**Runtime/Core/Bindings/:**
- Назначение: Все реализации привязок между источниками и приёмниками данных
- Содержит: Конкретные классы привязок, пул объектов, контекст привязок
- Ключевые файлы:
  - `AbstractEventBinding.cs` — базовый класс всех привязок
  - `BindFrom.cs` — структура для fluent API (`Bind.From(x)`)
  - `BindingPool.cs` — статический пул объектов привязок
  - `EventBindingContext.cs` — контейнер-реестр привязок для View
  - `ButtonEventBinding.cs` — привязка Button → Action с контекстом
  - `ButtonEventSimpleBinding.cs` — привязка Button → Action без контекста
  - `ButtonCollectionEventBinding.cs` — привязка коллекции кнопок → Action
  - `WidgetViewBinding.cs` — привязка ViewModel → дочерний AbstractWidgetView
  - `ElementCollectionBinding.cs` — привязка ReactiveList → список виджетов с auto-instantiation
  - `ObservableValueEventBinding.cs` — привязка ObservableValue (Model) → callback

**Runtime/Core/Types/:**
- Назначение: Реактивные типы данных — основа системы привязок
- Содержит:
  - `AbstractViewModel.cs` — базовый класс ViewModel (auto-discovery IReactiveValue полей)
  - `ReactiveValue.cs` — реактивное значение с single-subscriber семантикой
  - `ReactiveList.cs` — реактивный список с callbacks на add/replace/remove
  - `ReactiveAwaitable.cs` — async/await мост между View и бизнес-логикой
  - `ObservableValue.cs` — observable значение с event-паттерном (multi-subscriber) для Model-слоя

**Runtime/Core/Interfaces/:**
- Назначение: Публичные интерфейсы фреймворка
- Содержит:
  - `IViewModelParameter.cs` (IReactiveValue) — `Dispose()` + `Unbind()`
  - `IEventBindingContext.cs` — контракт контекста привязок
  - `IObservableValue.cs` — read-only интерфейс ObservableValue
  - `IViewModelListCount.cs` (IReactiveListCount) — `Count` для списков
  - `IWidgetViewFactory.cs` — фабрика для создания/удаления виджетов в коллекциях

**Runtime/Utils/:**
- Назначение: Extension-методы, формирующие fluent API привязок
- Содержит:
  - `BindFromExtensions.cs` — `Bind.From(source)` и `Bind.FromUnsafe(source)`
  - `ModelToViewModelEventBindExtensions.cs` — `ObservableValue → ReactiveValue` привязки
  - `UIToViewModelEventBindExtensions.cs` — `Button → ReactiveValue<Action>` привязки
  - `ViewModelToUIEventBindExtensions.cs` — `ReactiveValue → TMP_Text/GameObject/RectTransform` и `ReactiveList → List<WidgetView>` привязки

**Editor/:**
- Назначение: Unity Editor инструменты для отладки и визуализации ViewModel
- Содержит:
  - `DevWidgetEditor.cs` — Custom Inspector: Open/Close prefab, Save/Load ViewModel как JSON
  - `ViewModelDrawer.cs` — Универсальный рисовальщик ViewModel полей через рефлексию (поддерживает int, float, long, string, bool, enum, ValueTuple, UnityEngine.Object, вложенные ViewModel, ReactiveList, ReactiveValue)
  - `ViewModelViewerWindow.cs` — EditorWindow для real-time просмотра ViewModel активной сцены

**Samples~/Sample/:**
- Назначение: Полноценный Unity-проект демонстрирующий использование фреймворка
- Содержит: Model/View/ViewModel/Widget пример с кнопками, списками, слайдерами, async-анимациями
- Генерируемый: Нет (ручной код)
- Коммитится: Да (часть репозитория)

## Расположение ключевых файлов

**Точки входа (для пользователей пакета):**
- `Runtime/Core/AbstractWidgetView.cs`: Базовый View-компонент, наследуется для создания View
- `Runtime/Core/Types/AbstractViewModel.cs`: Базовый ViewModel, наследуется для создания ViewModel

**Конфигурация:**
- `package.json`: UPM-манифест (версия, зависимости)
- `Runtime/Shtl.Mvvm.asmdef`: Runtime assembly definition
- `Editor/Shtl.Mvvm.Editor.asmdef`: Editor assembly definition
- `.editorconfig`: Стиль кода

**Ядро:**
- `Runtime/Core/Types/ReactiveValue.cs`: Реактивное значение (основной строительный блок)
- `Runtime/Core/Types/ReactiveList.cs`: Реактивный список
- `Runtime/Core/Bindings/EventBindingContext.cs`: Контекст привязок
- `Runtime/Core/Bindings/BindingPool.cs`: Пул объектов привязок

**Тестирование:**
- `Tests/Runtime/`: Runtime-тесты (пока пусто)
- `Tests/Editor/`: Editor-тесты (пока пусто)

## Соглашения об именовании

**Файлы:**
- PascalCase для всех C# файлов: `ReactiveValue.cs`, `AbstractWidgetView.cs`
- Имя файла совпадает с именем основного класса/интерфейса
- Префикс `Abstract` для абстрактных классов: `AbstractEventBinding.cs`, `AbstractWidgetView.cs`
- Префикс `I` для интерфейсов: `IWidgetView.cs`, `IEventBindingContext.cs`

**Директории:**
- PascalCase: `Core/`, `Bindings/`, `Types/`, `Utils/`, `Interfaces/`
- Группировка по назначению, не по фиче

**Assembly Definitions:**
- Формат: `Shtl.Mvvm.asmdef`, `Shtl.Mvvm.Editor.asmdef`
- Namespace совпадает с именем assembly: `Shtl.Mvvm`, `Shtl.Mvvm.Editor`

## Где размещать новый код

**Новый реактивный тип (например, ReactiveQueue, ReactiveDictionary):**
- Реализация: `Runtime/Core/Types/`
- Интерфейс (если нужен): `Runtime/Core/Interfaces/`
- Должен реализовать `IReactiveValue` для автоматической регистрации в `AbstractViewModel`

**Новый тип привязки (например, для Slider, Toggle, InputField):**
- Реализация привязки: `Runtime/Core/Bindings/` (наследовать от `AbstractEventBinding<T>`)
- Extension-метод `.To(...)`: добавить в соответствующий файл `Runtime/Utils/`:
  - `ViewModelToUIEventBindExtensions.cs` — для привязок ViewModel → UI
  - `UIToViewModelEventBindExtensions.cs` — для привязок UI → ViewModel
  - `ModelToViewModelEventBindExtensions.cs` — для привязок Model → ViewModel

**Новый Editor-инструмент:**
- Реализация: `Editor/`
- Assembly: `Shtl.Mvvm.Editor` (только Editor-платформа)
- Поддержка нового типа в `ViewModelDrawer`: расширить метод `BuildField()` или `BuildParameter()`

**Новый интерфейс:**
- Реализация: `Runtime/Core/Interfaces/`

**Тесты:**
- Runtime-тесты: `Tests/Runtime/`
- Editor-тесты: `Tests/Editor/`

## Специальные директории

**Samples~/:**
- Назначение: Примеры использования пакета. Тильда `~` в имени скрывает директорию от Unity Asset Database
- Генерируемый: Нет
- Коммитится: Да
- Внутри содержит полноценный Unity-проект `Sample/` с собственными `ProjectSettings/`, `Assets/`, `Library/`

**Tests/:**
- Назначение: Зарезервировано для тестов (пока пусто)
- Генерируемый: Нет
- Коммитится: Да

**.planning/:**
- Назначение: Документация для GSD-процесса планирования
- Генерируемый: Да (инструментами планирования)
- Коммитится: Да

---

*Анализ структуры: 2026-04-09*
