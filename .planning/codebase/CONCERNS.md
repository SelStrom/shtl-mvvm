# Проблемы и технический долг

**Дата анализа:** 2026-04-09

## Технический долг

**Противоречие между package.json и asmdef по зависимости TextMeshPro:**
- Проблема: `package.json` объявляет зависимость `com.unity.ugui`, но `Runtime/Shtl.Mvvm.asmdef` ссылается на `Unity.TextMeshPro`. Файл `Runtime/Utils/ViewModelToUIEventBindExtensions.cs` использует `using TMPro` и типы `TMP_Text`. Последний коммит (`5802404`) заявляет "replace textmeshpro dependency with ugui for Unity 6 compatibility", но замена выполнена не полностью — asmdef и код всё ещё зависят от TextMeshPro.
- Файлы: `Runtime/Shtl.Mvvm.asmdef`, `Runtime/Utils/ViewModelToUIEventBindExtensions.cs`, `package.json`
- Влияние: Пакет не компилируется в проектах без TextMeshPro (особенно Unity 6, где TMP встроен в ugui). Указанная в `package.json` зависимость `com.unity.ugui` не покрывает реальную потребность в TMP API.
- Путь решения: Либо полностью заменить `TMP_Text` на `UnityEngine.UI.Text` / новый Text компонент из ugui в Unity 6, либо вернуть `com.unity.textmeshpro` в `package.json` и использовать `#if` директивы для поддержки обеих версий.

**Рефлексия в конструкторе AbstractViewModel:**
- Проблема: `AbstractViewModel` в конструкторе обходит все поля через `GetType().GetFields()` с помощью рефлексии. Это вызывается при каждом создании ViewModel.
- Файлы: `Runtime/Core/Types/AbstractViewModel.cs` (строки 11-17)
- Влияние: Потенциально медленная операция при частом создании ViewModel (например, в коллекциях элементов). `GetFields()` без `BindingFlags` возвращает только `public instance` поля, что ограничивает гибкость — protected/private поля с `IReactiveValue` не обнаруживаются.
- Путь решения: Кэшировать `FieldInfo[]` по типу в статическом `Dictionary<Type, FieldInfo[]>`. Рассмотреть source generators для compile-time регистрации полей.

**BindingPool — статический пул без очистки:**
- Проблема: `BindingPool` использует статические словари `_typeToPoolHolder`, которые никогда не очищаются. Объекты в пуле накапливаются за время жизни приложения.
- Файлы: `Runtime/Core/Bindings/BindingPool.cs`
- Влияние: Утечка памяти при долгом сеансе, особенно если создаётся множество привязок разных типов. Статические коллекции также не потокобезопасны.
- Путь решения: Добавить метод `ClearAll()` для вызова при смене сцен. Рассмотреть ограничение размера пула.

**TODO в IEventBindingContext — нетипизированный ключ привязки:**
- Проблема: Ключ привязки имеет тип `object`, что допускает коллизии и отсутствие типобезопасности.
- Файлы: `Runtime/Core/Interfaces/IEventBindingContext.cs` (строка 5)
- Влияние: Возможны runtime-ошибки при дублировании ключей разных типов. Отладка затруднена.
- Путь решения: Ввести строго типизированный ключ привязки (struct или generic constraint).

**TODO в ViewModelDrawer — ValueTuple не редактируемый:**
- Проблема: ValueTuple-поля в ViewModel отображаются в инспекторе только для чтения.
- Файлы: `Editor/ViewModelDrawer.cs` (строка 335)
- Влияние: Невозможно изменять ValueTuple-поля через DevWidget редактор.
- Путь решения: Реализовать запись через рефлексию с boxing/unboxing ValueTuple (ссылка на задачу в Asana в коде).

## Известные баги

**ReactiveValue допускает только одну подписку:**
- Симптомы: `InvalidOperationException("Already bound")` при попытке повторного `Connect`.
- Файлы: `Runtime/Core/Types/ReactiveValue.cs` (строки 36-39)
- Триггер: Вызов `Connect()` второй раз без предварительного `Unbind()`.
- Обходной путь: Вручную вызывать `Unbind()` перед повторным `Connect()`. Но это поведение by design — один ReactiveValue привязывается к одному UI-элементу.

**NullReferenceException в ReactiveList при обращении к _list:**
- Симптомы: NRE при вызове `Contains`, `IndexOf`, `CopyTo` на пустом `ReactiveList` (когда `_list == null`).
- Файлы: `Runtime/Core/Types/ReactiveList.cs` (строки 158-161)
- Триггер: Вызов `Contains()`, `IndexOf()`, `CopyTo()` до первого добавления элемента. Используется `_list!` (null-forgiving operator) без проверки.
- Обходной путь: Всегда добавлять элемент перед вызовом этих методов.

**Assert.IsTrue с инвертированной логикой в AbstractWidgetView.Connect:**
- Симптомы: Assertion срабатывает когда подключается тот же ViewModel, но сообщение говорит об обратном.
- Файлы: `Runtime/Core/AbstractWidgetView.cs` (строка 31)
- Триггер: `Assert.IsTrue(vm != ViewModel, ...)` — срабатывает только когда `vm == ViewModel`, но текст ошибки сбивает с толку ("must be not equals reset view model"). Семантически непонятно, что такое "reset view model".
- Обходной путь: Нет, это проверка корректности.

**IEnumerable GetEnumerator потенциальный NRE:**
- Симптомы: NRE при итерации `ReactiveList` как `IEnumerable` (non-generic) когда `_list == null`.
- Файлы: `Runtime/Core/Types/ReactiveList.cs` (строка 174)
- Триггер: `((IEnumerable)_list)?.GetEnumerator()` — каст `null` к `IEnumerable` даст `null`, но `?.` защищает. Однако generic версия на строке 173 правильно обрабатывает null через `??`.

## Вопросы безопасности

**Рефлексия в DevWidget для инъекции ViewModel:**
- Риск: `DevWidget.InjectViewModel()` использует `MakeGenericType` и `GetMethod("Connect")` через рефлексию, что обходит типобезопасность. Null-forgiving `injectMethod!.Invoke(...)` может упасть.
- Файлы: `Runtime/DevWidget.cs` (строки 79-83)
- Текущая защита: Код обёрнут в `#if UNITY_EDITOR` — не попадает в билд.
- Рекомендации: Добавить null-проверки для `injectMethod` и `ViewModelType`.

**Сериализация/десериализация ViewModel в JSON:**
- Риск: `DevWidgetEditor` сериализует/десериализует ViewModel через Newtonsoft.Json без валидации содержимого. Вредоносный JSON-файл может инжектировать неожиданные данные.
- Файлы: `Editor/DevWidgetEditor.cs` (строки 133-161)
- Текущая защита: Только Editor-код, не попадает в билд. Используется `TypeNameHandling` по умолчанию (None), что безопасно.
- Рекомендации: Минимальный риск, т.к. только Editor-код.

## Узкие места производительности

**Рефлексия при создании каждого ViewModel:**
- Проблема: `AbstractViewModel` конструктор вызывает `GetType().GetFields()` на каждый экземпляр.
- Файлы: `Runtime/Core/Types/AbstractViewModel.cs` (строки 11-17)
- Причина: Отсутствие кэширования метаданных рефлексии по типу.
- Путь улучшения: Статический `Dictionary<Type, FieldInfo[]>` для кэша. При 100+ элементах в коллекции рефлексия вызывается 100+ раз для одного и того же типа.

**ViewModelViewerWindow опрашивает сцену каждый кадр:**
- Проблема: `OnEditorUpdate` вызывает `CollectActiveViewModels()`, который делает `SceneManager.GetActiveScene().GetRootGameObjects()` + `GetComponentsInChildren<MonoBehaviour>()` + LINQ фильтрацию с рефлексией на каждый Editor update.
- Файлы: `Editor/ViewModelViewerWindow.cs` (строки 80-104, 163-181)
- Причина: Нет механизма подписки на изменения — используется polling.
- Путь улучшения: Использовать `EditorApplication.hierarchyChanged` для обновления списка виджетов вместо постоянного опроса.

**Activator.CreateInstance в ReactiveList.ResizeAndFill:**
- Проблема: Дефолтная фабрика `_defaultFactory` использует `Activator.CreateInstance` при каждом создании элемента.
- Файлы: `Runtime/Core/Types/ReactiveList.cs` (строка 12)
- Причина: Рефлексия вместо `new()` constraint или cached delegate.
- Путь улучшения: Использовать `new TElement()` через generic constraint или кэшированный `Expression.Lambda`.

## Хрупкие участки

**ElementCollectionBinding — связь коллекции с UI:**
- Файлы: `Runtime/Core/Bindings/ElementCollectionBinding.cs`
- Почему хрупкий: Сложная логика синхронизации между `ReactiveList<TViewModel>` и `List<TWidgetView>`. Метод `OnContentChanged` вручную синхронизирует два списка через циклы while/for. Вставка элемента в середину (`Insert`) не обрабатывает сдвиг существующих виджетов — `OnElementAdded` при `index < _widgets.Count` делает замену вместо вставки.
- Безопасная модификация: Тестировать все сценарии: добавление, удаление, вставку в середину, замену, очистку, сортировку. Особенно проверять соответствие индексов между VM-списком и виджетами.
- Покрытие тестами: Отсутствует полностью.

**AbstractWidgetView — жизненный цикл:**
- Файлы: `Runtime/Core/AbstractWidgetView.cs`
- Почему хрупкий: `Connect()` вызывает `ViewModel?.Dispose()` на предыдущем ViewModel, но `Dispose()` вызывает `ViewModel?.Unbind()` — разное поведение при переподключении vs отключении. `Dispose` не вызывается автоматически при `OnDestroy` MonoBehaviour.
- Безопасная модификация: Убедиться, что `Connect` и `Dispose` вызываются в правильном порядке. При уничтожении GameObject необходимо вручную вызвать `Dispose`.
- Покрытие тестами: Отсутствует.

**Дублирование IsSubclassOfAbstractWidgetView:**
- Файлы: `Runtime/DevWidget.cs` (строки 99-112), `Editor/ViewModelViewerWindow.cs` (строки 195-207)
- Почему хрупкий: Один и тот же вспомогательный метод скопирован в двух местах. Изменение в одном месте может быть забыто в другом.
- Безопасная модификация: Вынести в общий утилитарный класс.

## Ограничения масштабирования

**Одна подписка на ReactiveValue:**
- Текущая ёмкость: Ровно 1 подписчик через `Connect()`.
- Граница: Невозможно привязать один ReactiveValue к нескольким UI-элементам.
- Путь масштабирования: Использовать `Action` multicast delegate или список подписчиков. Это архитектурное решение — фреймворк намеренно ограничивает до 1:1 привязки.

**Отсутствие поддержки async/await кроме ReactiveAwaitable:**
- Текущая ёмкость: Единственный async-паттерн — `ReactiveAwaitable` через `TaskCompletionSource`.
- Граница: Нет поддержки `CancellationToken`, нет интеграции с UniTask.
- Путь масштабирования: Добавить overload с `CancellationToken`. Рассмотреть интеграцию с UniTask для лучшей производительности в Unity.

## Зависимости под риском

**TextMeshPro (com.unity.textmeshpro):**
- Риск: В Unity 6+ TextMeshPro интегрирован в ugui. Отдельный пакет `com.unity.textmeshpro` может быть deprecated. Текущая ссылка на `Unity.TextMeshPro` в asmdef может не работать в Unity 6.
- Влияние: Компиляция пакета невозможна в Unity 6 без адаптации.
- План миграции: Использовать `#if` директивы для условной компиляции, или полностью перейти на ugui API в Unity 6.

**Newtonsoft.Json (com.unity.nuget.newtonsoft-json):**
- Риск: Низкий. Используется только в Editor-коде (`DevWidgetEditor.cs`). Пакет поддерживается Unity.
- Влияние: Только редактор, не влияет на runtime.
- План миграции: Не требуется.

**JetBrains.Annotations:**
- Риск: Минимальный. Используется для атрибутов `[NotNull]`, `[CanBeNull]`, `[UsedImplicitly]`.
- Файлы: `Runtime/Core/Types/ReactiveValue.cs`, `Runtime/Core/Types/ReactiveList.cs`, `Runtime/DevWidget.cs`
- Влияние: Только аннотации, не влияет на runtime-поведение.

## Отсутствующая критическая функциональность

**Полное отсутствие тестов:**
- Проблема: Директории `Tests/Editor` и `Tests/Runtime` существуют, но пусты (только `.meta` файлы). Ни одного unit-теста во всём проекте.
- Блокирует: Уверенность в корректности при рефакторинге. Невозможно проверить регрессии.

**Нет автоматической отписки при уничтожении GameObject:**
- Проблема: `AbstractWidgetView` не реализует `OnDestroy()`. Если GameObject уничтожается без вызова `Dispose()`, привязки остаются активными.
- Файлы: `Runtime/Core/AbstractWidgetView.cs`
- Блокирует: Корректная работа при динамическом создании/уничтожении UI-элементов.

**Нет валидации в BindFrom при null Source:**
- Проблема: `BindFrom<TSource>` не проверяет `Source` на null. `FromUnsafe` возвращает nullable, но `From` нет.
- Файлы: `Runtime/Utils/BindFromExtensions.cs`, `Runtime/Core/Bindings/BindFrom.cs`
- Блокирует: Graceful handling при отсутствующих компонентах UI.

**autoReferenced: false в Runtime asmdef:**
- Проблема: `Runtime/Shtl.Mvvm.asmdef` имеет `"autoReferenced": false`, что означает, что проекты-потребители должны вручную добавлять ссылку на этот assembly.
- Файлы: `Runtime/Shtl.Mvvm.asmdef`
- Блокирует: Может сбивать с толку новых пользователей пакета.

## Пробелы в покрытии тестами

**Всё:**
- Что не тестируется: Весь Runtime-код (ReactiveValue, ReactiveList, AbstractViewModel, все Bindings, все Extension-методы)
- Файлы: все файлы в `Runtime/`
- Риск: Любое изменение может сломать существующую функциональность незаметно
- Приоритет: Высокий

**Критические сценарии без тестов:**
- ReactiveValue: Connect/Unbind/Dispose цикл, поведение при повторном Connect, EqualityComparer для reference-типов
- ReactiveList: ResizeAndFill, Insert с уведомлениями, Remove/Clear, порядок callback'ов
- ElementCollectionBinding: синхронизация списков, добавление/удаление/замена элементов
- AbstractWidgetView: жизненный цикл Connect/Dispose, повторный Connect
- BindingPool: получение/возврат объектов, корректная типизация
- Приоритет: Высокий

---

*Аудит проблем: 2026-04-09*
