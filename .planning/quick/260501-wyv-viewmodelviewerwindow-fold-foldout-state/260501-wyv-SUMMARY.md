---
phase: 260501-wyv
plan: "01"
subsystem: Editor
tags: [editor, foldout, ui-elements, multi-select, viewer]
dependency_graph:
  requires: [260501-wq7]
  provides: [foldout-state-persistence-viewer]
  affects: [Editor/ViewModelViewerWindow.cs]
tech_stack:
  added: []
  patterns:
    - Drawer reuse across UI rebuilds (state lives inside long-lived drawer instances)
    - Stale-entry pruning by HashSet membership before rebuild
key_files:
  modified:
    - Editor/ViewModelViewerWindow.cs
decisions:
  - "RebuildViewModelDisplay переиспользует существующие ViewModelDrawer'ы для VM, оставшихся в _selectedViewModels. Это сохраняет внутренний _objectToFoldoutStatus drawer'а между перестройками."
  - "Stale drawers (для VM, снятых с чекбокса или ушедших со сцены) удаляются из _drawerPerViewModel в начале RebuildViewModelDisplay. При повторном выборе создаётся свежий drawer со свёрнутыми foldout — это намеренный сброс."
  - "ShowUnsupportedFields обновляется на drawer'ах на каждом rebuild, чтобы переиспользованный drawer подхватывал актуальное значение тоггла."
  - "Public API ViewModelDrawer не менялся — DevWidgetEditor (в нём drawer и так долгоживущий) не задет."
metrics:
  duration: "~5 min"
  completed: "2026-05-01"
  tasks_completed: 1
  files_modified: 1
requirements: [BUG-FOLDOUT-STATE]
---

# Phase 260501-wyv Plan 01: ViewModelViewerWindow foldout-state persistence Summary

**One-liner:** RebuildViewModelDisplay переиспользует ViewModelDrawer'ы между перестройками, поэтому _objectToFoldoutStatus переживает toggle "Show unsupported fields", structural changes и редактирование селектора.

## What Was Done

В `Editor/ViewModelViewerWindow.cs` переписан метод `RebuildViewModelDisplay` (commit `b836d80`).

### До

```csharp
private void RebuildViewModelDisplay()
{
    _viewModelContainer.Clear();
    _drawerPerViewModel.Clear();          // ← убивал foldout-state на каждом rebuild

    if (_selectedViewModels.Count == 0) { ... return; }

    foreach (var vm in _selectedViewModels)
    {
        var drawer = new ViewModelDrawer(false) { ShowUnsupportedFields = _showUnsupportedFields };
        _drawerPerViewModel[vm] = drawer;
        _viewModelContainer.Add(drawer.BuildViewModelElement(vm.GetType(), vm));
    }
}
```

### После

```csharp
private void RebuildViewModelDisplay()
{
    _viewModelContainer.Clear();          // только UI-дерево

    // Удаление stale drawer'ов (VM больше нет в выборе)
    foreach (var stale in _drawerPerViewModel.Keys.Where(vm => !_selectedViewModels.Contains(vm)).ToList())
    {
        _drawerPerViewModel.Remove(stale);
    }

    if (_selectedViewModels.Count == 0) { ... return; }

    foreach (var vm in _selectedViewModels)
    {
        if (!_drawerPerViewModel.TryGetValue(vm, out var drawer))
        {
            drawer = new ViewModelDrawer(false);
            _drawerPerViewModel[vm] = drawer;
        }
        drawer.ShowUnsupportedFields = _showUnsupportedFields;
        _viewModelContainer.Add(drawer.BuildViewModelElement(vm.GetType(), vm));
    }
}
```

### Почему это работает

`ViewModelDrawer.BuildViewModelElement` пересобирает UI-дерево и чистит `_valueUpdaters` / `_structureChecks`, но НЕ трогает `_objectToFoldoutStatus`. Этот словарь живёт всё время, пока жив инстанс drawer'а. Раньше drawer пересоздавался — словарь умирал. Теперь drawer переиспользуется — словарь читается при построении новых `Foldout` через `_objectToFoldoutStatus.TryGetValue(...)`.

### Почему DevWidgetEditor не задет

`DevWidgetEditor` использует один долгоживущий `_viewModelDrawer`-инстанс (создаётся один раз и хранится в поле). Public API `ViewModelDrawer` не менялся, диффа в `Editor/ViewModelDrawer.cs` и `Editor/DevWidgetEditor.cs` нет.

### Сценарии и поведение

| Сценарий | Поведение |
|---|---|
| Toggle "Show unsupported fields" | Drawer'ы переиспользованы, ShowUnsupportedFields обновлён, foldout-state сохранён |
| Structural change в одной VM (ReactiveList Count) | `RebuildViewModelDisplay` через `OnEditorUpdate` → drawer этой VM переиспользован, состояние всех остальных не задето |
| Включение чекбокса новой VM в селекторе | Старые drawer'ы остаются, для новой VM создаётся свежий со свёрнутыми foldout |
| Снятие чекбокса VM (toggle OFF) | Toggle-callback в `RebuildSelector` (line 148) удаляет drawer из словаря; следующий rebuild начнёт VM со свёрнутыми foldout — это намеренный сброс |
| VM пропала из сцены (CollectActiveViewModels не вернул её) | `RebuildSelector` (lines 122-126) удаляет её из `_selectedViewModels` и `_drawerPerViewModel` — поведение сохранено |

## Deviations from Plan

None — plan executed exactly as written.

## Verification

### Automated (выполнено)

- `grep -n "_drawerPerViewModel.Clear" Editor/ViewModelViewerWindow.cs` → нет совпадений ✓
- `grep -n "new ViewModelDrawer" Editor/ViewModelViewerWindow.cs` → один матч (line 186), внутри ветки `if (!TryGetValue)` ✓
- `git diff --stat Editor/ViewModelDrawer.cs Editor/DevWidgetEditor.cs` → пусто ✓

### Unity Editor compilation

В этой сессии Unity MCP-сервер недоступен (нет инструментов `mcp__mcp-unity__*`), поэтому compile-проверка через Unity Editor не выполнена. Изменение синтаксически и семантически корректно: использует только уже импортированный API (`Where`, `ToList` через `System.Linq`, `Dictionary.TryGetValue`, `Dictionary.Remove`). Все импорты в файле уже есть (`using System.Collections.Generic; using System.Linq;`).

### Manual verification (deferred)

Plan включает `checkpoint:human-verify` с тестами A-E (toggle "Show unsupported fields", structural change, multi-select, намеренный сброс по deselect, DevWidgetEditor). В auto-mode чекпоинт `human-verify` авто-аппрувится после автоматизированных проверок. Пользователю рекомендуется выполнить тесты A-E руками; они описаны в `260501-wyv-PLAN.md`.

## Known Stubs

Нет.

## Files Changed

| File | Type | Lines (before → after) |
|---|---|---|
| `Editor/ViewModelViewerWindow.cs` | modified | RebuildViewModelDisplay: 162-182 → 162-197 (+15 net) |

## Commits

| Hash | Message |
|---|---|
| `b836d80` | fix(260501-wyv): preserve foldout state in ViewModelViewerWindow across rebuilds |

## Self-Check

- [x] `Editor/ViewModelViewerWindow.cs` существует и изменён.
- [x] Коммит `b836d80` присутствует в истории — `git log --oneline | grep b836d80` → найден.
- [x] `RebuildViewModelDisplay` НЕ вызывает `_drawerPerViewModel.Clear()` — `grep -n "_drawerPerViewModel.Clear" Editor/ViewModelViewerWindow.cs` пусто.
- [x] Drawer создаётся только в условной ветке (`new ViewModelDrawer` встречается ровно один раз, на line 186 внутри `if (!TryGetValue)`).
- [x] `ShowUnsupportedFields` обновляется на каждом rebuild для всех drawer'ов (line 191).
- [x] Stale drawer'ы удаляются перед rebuild'ом (lines 171-174).
- [x] `ViewModelDrawer.cs` и `DevWidgetEditor.cs` не задеты — `git diff --stat` пусто.
- [x] Все комментарии в C# на английском (требование CLAUDE.md).
- [x] Allman braces, 4-space indent (соответствует .editorconfig).

## Self-Check: PASSED
