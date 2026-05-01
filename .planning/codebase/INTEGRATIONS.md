# Внешние интеграции

**Дата анализа:** 2026-04-09

## API и внешние сервисы

**Отсутствуют.** Проект является автономным Unity-пакетом (UPM-библиотекой) без обращений к внешним API, сетевым сервисам или облачным платформам.

## Хранение данных

**Базы данных:**
- Не используются

**Файловое хранилище:**
- Только локальная файловая система — для сохранения/загрузки JSON-файлов ViewModel через DevWidget Editor
  - Реализация: `Editor/DevWidgetEditor.cs` (методы `SaveViewModel`, `LoadViewModel`)
  - Использует `EditorUtility.SaveFilePanel` / `EditorUtility.OpenFilePanel`
  - Формат: JSON через `Newtonsoft.Json` с настройками `snake_case` именования

**Кэширование:**
- Не используется на уровне пакета
- Внутренний пул объектов для биндингов: `Runtime/Core/Bindings/BindingPool.cs`

## Аутентификация и идентификация

- Не применимо — библиотечный пакет не управляет аутентификацией

## Мониторинг и наблюдаемость

**Отслеживание ошибок:**
- Не используется

**Логирование:**
- Стандартный `UnityEngine.Debug` — через `Debug.Log`, `Debug.LogError` (в примерах)
- Фреймворк не добавляет собственной системы логирования

## CI/CD и деплой

**Хостинг репозитория:**
- GitHub: `https://github.com/SelStrom/shtl-mvvm.git`

**CI-пайплайн:**
- Не обнаружен (нет `.github/workflows/`, `Jenkinsfile`, `.gitlab-ci.yml` и т.д.)

**Дистрибуция:**
- Через git URL в Unity Package Manager
- Пример подключения (из `Samples~/Sample/Packages/manifest.json`):
  ```json
  "com.shtl.mvvm": "file:../../../"
  ```
- Для конечных пользователей:
  ```
  Unity Package Manager → Add package from git URL →
  https://github.com/SelStrom/shtl-mvvm.git
  ```

## Конфигурация окружения

**Переменные окружения:**
- Не требуются — пакет не использует переменные окружения
- Файлы `.env` не обнаружены

**Секреты:**
- Не применимо

## Вебхуки и колбэки

**Входящие:**
- Не применимо

**Исходящие:**
- Не применимо

## Зависимости от Unity-пакетов

Полный список зависимостей пакета `com.shtl.mvvm`:

| Пакет | Версия | Назначение | Где используется |
|-------|--------|------------|------------------|
| `com.unity.ugui` | 1.0.0 | UI-система Unity (Button, RectTransform и др.) | `Runtime/Utils/UIToViewModelEventBindExtensions.cs`, `Runtime/Utils/ViewModelToUIEventBindExtensions.cs` |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 | JSON-сериализация | `Editor/DevWidgetEditor.cs` |
| `Unity.TextMeshPro` | (через asmdef) | Компонент текста TMP_Text | `Runtime/Utils/ViewModelToUIEventBindExtensions.cs` |

**Примечание:** В `package.json` зависимость от TextMeshPro не объявлена напрямую (после коммита `5802404` заменена на `com.unity.ugui`), но ссылка `Unity.TextMeshPro` остаётся в `Runtime/Shtl.Mvvm.asmdef`. Это потенциальная проблема совместимости — см. CONCERNS.md.

## Интеграция с Unity Editor

**Кастомные окна:**
- `Editor/ViewModelViewerWindow.cs` — окно для инспекции ViewModel в реальном времени (Window → ViewModel Viewer)

**Кастомные инспекторы:**
- `Editor/DevWidgetEditor.cs` — кастомный инспектор для `DevWidget`, использует UI Toolkit (`VisualElement`, `PropertyField`, `Button`, `Toggle`, `HelpBox`)

**Кастомные отрисовщики:**
- `Editor/ViewModelDrawer.cs` — отрисовка дерева ViewModel-параметров

---

*Аудит интеграций: 2026-04-09*
