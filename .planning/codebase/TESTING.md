# Паттерны тестирования

**Дата анализа:** 2026-04-09

## Фреймворк тестирования

**Runner:**
- Unity Test Framework (встроенный)
- Конфигурация: через asmdef-файлы в директории `Tests/`

**Assertion Library:**
- `UnityEngine.Assertions` (используется в runtime-коде: `Assert.IsTrue`)
- NUnit (стандарт для Unity Test Framework, но тесты пока не написаны)

**Команды запуска:**
```bash
# Через Unity Editor: Window > General > Test Runner
# Через CLI (если установлен Unity):
unity -runTests -testPlatform EditMode -projectPath .
unity -runTests -testPlatform PlayMode -projectPath .
```

## Организация тестовых файлов

**Расположение:**
- `Tests/Editor/` - тесты в режиме редактора (EditMode)
- `Tests/Runtime/` - тесты в режиме воспроизведения (PlayMode)

**Текущее состояние:**
- Директории `Tests/Editor/` и `Tests/Runtime/` существуют, но **пусты** - тестовые файлы отсутствуют
- Нет asmdef-файлов для тестовых сборок
- Нет ни одного тестового класса

## Структура тестов

**Ожидаемая структура (на основе Unity Test Framework):**

Для создания тестов необходимо:

1. Создать asmdef-файлы:
   - `Tests/Editor/Shtl.Mvvm.Editor.Tests.asmdef` с ссылками на `Shtl.Mvvm` и `Shtl.Mvvm.Editor`
   - `Tests/Runtime/Shtl.Mvvm.Tests.asmdef` с ссылкой на `Shtl.Mvvm`

2. Рекомендуемая структура тестового файла:
```csharp
using NUnit.Framework;

namespace Shtl.Mvvm.Tests
{
    [TestFixture]
    public class ReactiveValueTests
    {
        [Test]
        public void Value_WhenSet_InvokesOnChanged()
        {
            // Arrange
            var reactive = new ReactiveValue<int>(0);
            var receivedValue = -1;
            reactive.Connect(v => receivedValue = v);

            // Act
            reactive.Value = 42;

            // Assert
            Assert.AreEqual(42, receivedValue);
        }
    }
}
```

## Мокирование

**Фреймворк:** Не настроен

**Рекомендации:**
- Основные типы библиотеки (`ReactiveValue<T>`, `ReactiveList<T>`, `ObservableValue<T>`) - чистые C#-классы без зависимостей от Unity (кроме `AbstractWidgetView<T>`, который наследует `MonoBehaviour`)
- Для EditMode тестов можно тестировать core-типы без мокирования
- Для PlayMode тестов потребуется создание GameObject с компонентами

**Что можно тестировать без мокирования:**
- `ReactiveValue<T>` - подписка, уведомления, Dispose, Unbind
- `ReactiveList<T>` - добавление, удаление, замена, ResizeAndFill, Connect
- `ObservableValue<T>` - события изменения
- `AbstractViewModel` - автоматическое обнаружение полей через рефлексию
- `EventBindingContext` - управление привязками
- `BindingPool` - пулинг привязок
- `ReactiveAwaitable` - асинхронные операции

**Что требует MonoBehaviour/GameObject:**
- `AbstractWidgetView<T>` - требует `MonoBehaviour`
- Все Binding-классы с UI-элементами (`ButtonEventBinding`, `WidgetViewBinding`)
- `ElementCollectionBinding` - создание/уничтожение объектов

## Фикстуры и фабрики

**Тестовые данные:** Не определены

**Рекомендуемый подход:**
```csharp
// Тестовый ViewModel
public class TestViewModel : AbstractViewModel
{
    public readonly ReactiveValue<string> Name = new();
    public readonly ReactiveValue<int> Count = new();
    public readonly ReactiveList<TestChildViewModel> Children = new();
}

public class TestChildViewModel : AbstractViewModel
{
    public readonly ReactiveValue<string> Label = new();
}
```

**Расположение фикстур:**
- `Tests/Editor/Fixtures/` - для EditMode тестов
- `Tests/Runtime/Fixtures/` - для PlayMode тестов

## Покрытие

**Требования:** Не установлены

**Инструмент:**
- Пакет `com.unity.testtools.codecoverage` присутствует в Sample-проекте (`Samples~/Sample/ProjectSettings/Packages/com.unity.testtools.codecoverage/Settings.json`)
- Не настроен в основном пакете

**Просмотр покрытия:**
```bash
# Через Unity Editor: Window > Analysis > Code Coverage
```

## Типы тестов

**Unit-тесты (EditMode):**
- Для чистых C#-классов ядра библиотеки
- Не требуют запуска Play Mode
- Быстрые, изолированные

**Интеграционные тесты (PlayMode):**
- Для UI-привязок и MonoBehaviour-компонентов
- Требуют создания GameObjects
- Для проверки полного цикла: Model -> Widget -> ViewModel -> View

**E2E тесты:**
- Не используются

## Приоритетные области для покрытия тестами

**Высокий приоритет:**
- `ReactiveValue<T>` (`Runtime/Core/Types/ReactiveValue.cs`) - ключевой примитив
- `ReactiveList<T>` (`Runtime/Core/Types/ReactiveList.cs`) - сложная логика коллекций
- `ObservableValue<T>` (`Runtime/Core/Types/ObservableValue.cs`) - паттерн наблюдателя
- `ReactiveAwaitable` (`Runtime/Core/Types/ReactiveAwaitable.cs`) - асинхронная логика
- `EventBindingContext` (`Runtime/Core/Bindings/EventBindingContext.cs`) - управление жизненным циклом привязок

**Средний приоритет:**
- `BindingPool` (`Runtime/Core/Bindings/BindingPool.cs`) - пулинг объектов
- `AbstractViewModel` (`Runtime/Core/Types/AbstractViewModel.cs`) - рефлексия полей
- Extension-методы в `Runtime/Utils/`

**Низкий приоритет:**
- Editor-инструменты (`Editor/ViewModelDrawer.cs`, `Editor/DevWidgetEditor.cs`)

## Тестируемые сценарии для ключевых типов

**ReactiveValue<T>:**
```csharp
// Connect вызывает callback с текущим значением для value-типов
// Connect бросает InvalidOperationException при повторном вызове
// Установка Value вызывает callback только при изменении
// Unbind отписывает callback
// Dispose сбрасывает значение и отписывает
```

**ReactiveList<T>:**
```csharp
// Add вызывает onElementAdded
// Remove вызывает onElementRemoved
// Индексатор вызывает onElementReplaced
// ResizeAndFill корректно добавляет/удаляет элементы
// Clear удаляет элементы по одному с уведомлениями
// Connect бросает InvalidOperationException при повторном вызове
// Операции до Connect не вызывают callback-ов
```

**ReactiveAwaitable:**
```csharp
// StartAsync создает TaskCompletionSource
// Повторный StartAsync возвращает тот же Task
// Unbind отменяет ожидание (TrySetCanceled)
// SuppressCancellation возвращает true при отмене
```

---

*Анализ тестирования: 2026-04-09*
