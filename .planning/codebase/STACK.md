# Технологический стек

**Дата анализа:** 2026-04-09

## Языки

**Основной:**
- C# — весь исходный код Runtime, Editor и Samples

**Вспомогательный:**
- JSON — конфигурация пакета (`package.json`), манифесты Unity, сериализация ViewModel

## Среда выполнения

**Движок:**
- Unity 2020.3+ (минимальная поддерживаемая версия указана в `package.json`)
- Совместимость с Unity 6 (коммит `5802404` заменил TextMeshPro на ugui для совместимости)

**Формат пакета:**
- Unity Package Manager (UPM) — установка через git URL
- Имя пакета: `com.shtl.mvvm`
- Версия: `1.1.0`

## Фреймворки

**Ядро:**
- Unity Engine — игровой движок, MonoBehaviour как базовый класс для View
- Собственный MVVM-фреймворк — `Shtl.Mvvm` (это и есть данный проект)

**UI:**
- Unity uGUI (`com.unity.ugui` 1.0.0) — `UnityEngine.UI.Button`, `RectTransform`, `GameObject.SetActive`
- TextMeshPro (`Unity.TextMeshPro`) — `TMP_Text` для биндингов текста (ссылка в `Runtime/Shtl.Mvvm.asmdef`)

**Редакторские инструменты:**
- Unity Editor UI Toolkit (`UnityEngine.UIElements`, `UnityEditor.UIElements`) — для кастомных инспекторов

**Сериализация:**
- Newtonsoft.Json (`com.unity.nuget.newtonsoft-json` 3.2.2) — сериализация/десериализация ViewModel в DevWidget Editor

**Тестирование:**
- Директории `Tests/Runtime/` и `Tests/Editor/` существуют, но пусты — тесты не реализованы

## Ключевые зависимости

**Критические (объявлены в `package.json`):**
- `com.unity.ugui` 1.0.0 — система Unity UI, используется для биндингов кнопок и UI-элементов
  - Документация: `Samples~/Sample/Library/PackageCache/com.unity.ugui@1.0.0/`
- `com.unity.nuget.newtonsoft-json` 3.2.2 — JSON-сериализация для DevWidget (сохранение/загрузка ViewModel)
  - Используется в: `Editor/DevWidgetEditor.cs`

**Неявные зависимости (через asmdef):**
- `Unity.TextMeshPro` — ссылка в `Runtime/Shtl.Mvvm.asmdef`, используется в биндингах `ViewModelToUIEventBindExtensions`
  - Файлы: `Runtime/Utils/ViewModelToUIEventBindExtensions.cs` (импорт `TMPro`)
  - Документация: `Samples~/Sample/Library/PackageCache/com.unity.textmeshpro@3.0.7/`

**Аннотации:**
- `JetBrains.Annotations` — используется в `Runtime/DevWidget.cs`, `Runtime/Core/Types/ReactiveValue.cs`, `Runtime/Core/Types/ReactiveList.cs`
  - Поставляется вместе с Unity (встроена в движок)

## Сборки (Assembly Definitions)

**Runtime:**
- `Runtime/Shtl.Mvvm.asmdef` — корневое пространство имён `Shtl.Mvvm`
  - Ссылки: `Unity.TextMeshPro`
  - `autoReferenced: false` — пакет нужно явно подключать в asmdef проекта
  - `allowUnsafeCode: false`

**Editor:**
- `Editor/Shtl.Mvvm.Editor.asmdef` — пространство имён `Shtl.Mvvm.Editor`
  - Ссылки: `Shtl.Mvvm`
  - Платформа: только `Editor`
  - `autoReferenced: true`

**Пример:**
- `Samples~/Sample/Assets/Scripts/Sample.asmdef`

## Конфигурация

**Стиль кода:**
- `.editorconfig` — Allman brace style, indent 4 пробела, `csharp_prefer_braces = true:warning`
- Полный набор C# правил форматирования

**Пакетный манифест:**
- `package.json` — UPM манифест с метаданными пакета

**Лицензия:**
- MIT (`LICENSE`)

## Требования к платформе

**Разработка:**
- Unity 2020.3 или новее
- Любая ОС, поддерживаемая Unity Editor

**Продакшен:**
- Все платформы, поддерживаемые Unity (пакет не ограничивает `includePlatforms` в Runtime asmdef)

---

*Анализ стека: 2026-04-09*
