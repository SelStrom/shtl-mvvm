---
phase: 260501-wyv
plan: "01"
type: execute
wave: 1
depends_on: []
files_modified:
  - Editor/ViewModelViewerWindow.cs
autonomous: true
requirements: [BUG-FOLDOUT-STATE]
tags: [editor, foldout, ui-elements, multi-select]
must_haves:
  truths:
    - "При раскрытии foldout в одной из активных ViewModel и последующей структурной перестройке (изменение Count в ReactiveList, ShowUnsupportedFields toggle, добавление/удаление другой ViewModel в селекторе) состояние раскрытия именно этого foldout сохраняется"
    - "Состояние foldout каждой выбранной ViewModel хранится независимо (мульти-селект)"
    - "При снятии чекбокса с ViewModel в селекторе её состояние foldout сбрасывается (drawer удаляется); при повторном выборе foldout-ы стартуют свёрнутыми (это допустимое поведение, так как сетевая ссылка на ViewModel уже не та же)"
  artifacts:
    - path: "Editor/ViewModelViewerWindow.cs"
      provides: "Метод RebuildViewModelDisplay переиспользует существующие ViewModelDrawer для всё ещё выбранных ViewModel"
      contains: "_drawerPerViewModel"
  key_links:
    - from: "ViewModelViewerWindow.RebuildViewModelDisplay"
      to: "ViewModelDrawer._objectToFoldoutStatus"
      via: "Сохранение инстанса ViewModelDrawer между перестройками — словарь foldout-state живёт внутри drawer'а"
      pattern: "_drawerPerViewModel\\[vm\\]"
---

<objective>
Сохранять foldout-state в ViewModelViewerWindow между структурными перестройками UI-дерева.

Purpose: Сейчас при любом изменении (toggle "Show unsupported fields", переключение чекбокса в селекторе, изменение размера ReactiveList, любое срабатывание структурной перестройки) метод `RebuildViewModelDisplay` пересоздаёт все `ViewModelDrawer`-инстансы и тем самым теряет `_objectToFoldoutStatus`. Пользователь снова и снова руками раскрывает дерево.

Output: `RebuildViewModelDisplay` переиспользует drawer'ы для уже выбранных ViewModel и пересоздаёт UI-дерево внутри живого drawer'а — так `_objectToFoldoutStatus` сохраняется. Каждая VM имеет независимое foldout-состояние.

**Note:** `DevWidgetEditor` использует один долгоживущий `_viewModelDrawer`, поэтому в нём этот баг не проявляется. Public API `ViewModelDrawer` не меняется.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md
@.planning/STATE.md
@.planning/quick/260501-wq7-viewmodelviewerwindow-toggle/260501-wq7-01-SUMMARY.md
@Editor/ViewModelViewerWindow.cs
@Editor/ViewModelDrawer.cs
@Editor/DevWidgetEditor.cs

<interfaces>
<!-- Ключевые контракты, нужные исполнителю; извлечены напрямую из кода. -->

From Editor/ViewModelDrawer.cs:
```csharp
public class ViewModelDrawer
{
    public bool ShowUnsupportedFields { get; set; }
    public Action OnStructureChanged { get; set; }

    // Foldout state — живёт всё время, пока жив инстанс drawer'а.
    // Чистится только при пересоздании drawer'а, НЕ при BuildViewModelElement.
    private readonly Dictionary<object, bool> _objectToFoldoutStatus = new();

    public ViewModelDrawer(bool isEditable = true);

    // Пересобирает UI-дерево, очищает _valueUpdaters / _structureChecks,
    // но _objectToFoldoutStatus остаётся — foldout-state выживает.
    public VisualElement BuildViewModelElement(Type viewModelType, object viewModel);

    // true => нужен структурный rebuild (изменился Count коллекции и т.п.)
    public bool UpdateValues();
}
```

From Editor/ViewModelViewerWindow.cs (текущее состояние):
```csharp
private readonly HashSet<AbstractViewModel> _selectedViewModels = new();
private readonly Dictionary<AbstractViewModel, ViewModelDrawer> _drawerPerViewModel = new();
private bool _showUnsupportedFields;
private VisualElement _viewModelContainer;
```
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Reuse ViewModelDrawer instances across RebuildViewModelDisplay</name>
  <files>Editor/ViewModelViewerWindow.cs</files>
  <action>
Изменить метод `RebuildViewModelDisplay` (строки 162-182) так, чтобы он переиспользовал существующие `ViewModelDrawer`-инстансы для VM, которые остались в `_selectedViewModels`. Это сохраняет `_objectToFoldoutStatus` внутри drawer'а между перестройками.

**Алгоритм нового RebuildViewModelDisplay:**

1. Очистить ТОЛЬКО UI-контейнер: `_viewModelContainer.Clear()`. **НЕ** делать `_drawerPerViewModel.Clear()`.
2. Удалить из `_drawerPerViewModel` записи для VM, которых больше нет в `_selectedViewModels` (stale entries после deselect).
3. Если `_selectedViewModels.Count == 0` — добавить HelpBox и выйти.
4. Для каждой VM в `_selectedViewModels`:
   - Если drawer уже существует в `_drawerPerViewModel` — переиспользовать его (только обновить `ShowUnsupportedFields = _showUnsupportedFields`).
   - Иначе — создать новый и положить в словарь.
   - Вызвать `drawer.BuildViewModelElement(vm.GetType(), vm)` и добавить результат в `_viewModelContainer`. `BuildViewModelElement` собирает свежее UI-дерево и попутно читает foldout-state из `_objectToFoldoutStatus`, поэтому раскрытые узлы остаются раскрытыми.

**Также проверить `RebuildSelector` (строки 117-160):** он уже удаляет stale записи из `_drawerPerViewModel` (строки 123-126) — ЭТО ОСТАВИТЬ. Удаление по deselect (строка 148: `_drawerPerViewModel.Remove(capturedWidget)`) — ТОЖЕ ОСТАВИТЬ (намеренный сброс foldout при снятии чекбокса).

**Граничные случаи:**
- Toggle "Show unsupported fields" → `_showUnsupportedFields` меняется → `RebuildViewModelDisplay()` → существующие drawer'ы переиспользуются с обновлённым `ShowUnsupportedFields` → foldout-state сохранён.
- Структурное изменение в одной VM (вернул `true` из `UpdateValues()`) → `RebuildViewModelDisplay()` → drawer этой VM переиспользуется, остальные тоже → foldout-state сохранён везде.
- Добавление новой VM в селектор (toggle ON) → `RebuildViewModelDisplay()` → старые drawer'ы остаются, для новой VM создаётся свежий.
- Снятие VM (toggle OFF) → drawer удаляется в callback'е toggle (строка 148), при следующем rebuild новый drawer стартует со свёрнутыми foldout — это допустимо.
- VM пропала из сцены → `RebuildSelector` чистит её из `_drawerPerViewModel` (строки 123-126) — поведение сохранено.

**Не менять:**
- Public API `ViewModelDrawer` (используется в `DevWidgetEditor` — не должно сломаться).
- Логику `OnEditorUpdate`, `RebuildSelector`, `CollectActiveViewModels`.
- Поля класса (HashSet, Dictionary, флаги уже определены корректно).

**Соответствие CLAUDE.md:**
- Все комментарии в C# — на английском.
- Allman braces, `var` где тип очевиден, отступ 4 пробела, фигурные скобки даже для однострочных условий.
- Минимум аллокаций в OnGUI hot path: переиспользование drawer'ов как раз снижает GC-давление по сравнению с текущим поведением (создание `new ViewModelDrawer` на каждый rebuild для каждой VM).
  </action>
  <verify>
    <automated>cd /Users/selstrom/work/projects/shtl-mvvm &amp;&amp; grep -n "_drawerPerViewModel.Clear" Editor/ViewModelViewerWindow.cs | grep -v '^#' | wc -l | awk '{ if ($1 == 0) print "OK: no full Clear() of drawer dictionary in RebuildViewModelDisplay"; else { print "FAIL: _drawerPerViewModel.Clear() still present"; exit 1 } }'</automated>
  </verify>
  <done>
- `RebuildViewModelDisplay` НЕ вызывает `_drawerPerViewModel.Clear()`.
- Drawer'ы переиспользуются для VM, оставшихся в `_selectedViewModels`.
- Stale drawer'ы (VM больше не выбран) удаляются из словаря.
- `ShowUnsupportedFields` обновляется на существующих drawer'ах.
- Файл компилируется в Unity Editor (нет ошибок в Console).
- Все комментарии в коде — на английском.
- Public API `ViewModelDrawer` не изменён, `DevWidgetEditor` не затронут.
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <what-built>
Сохранение foldout-state в `ViewModelViewerWindow` между структурными перестройками UI. Drawer'ы теперь переиспользуются между вызовами `RebuildViewModelDisplay`, поэтому словарь `_objectToFoldoutStatus` внутри каждого drawer'а живёт всё время, пока соответствующая ViewModel остаётся выбранной.
  </what-built>
  <how-to-verify>
1. Открыть Unity Editor, дождаться компиляции (Console — без ошибок).
2. Открыть сцену с GameObject `Gui` и хотя бы одним активным WidgetView (например, из `Samples~/Sample`). При необходимости — войти в Play Mode, чтобы ViewModel инициализировались.
3. Открыть Window → ViewModel Viewer.
4. В селекторе выбрать одну ViewModel чекбоксом.
5. Раскрыть несколько foldout (вложенные ViewModel, ReactiveList и т.п.). Запомнить, какие именно раскрыты.
6. **Тест A — toggle "Show unsupported fields":** переключить чекбокс "Show unsupported fields" сверху. Раскрытые foldout должны остаться раскрытыми.
7. **Тест B — структурное изменение:** если в видимой ViewModel есть ReactiveList — добавить/удалить элемент через игровую логику (или дождаться авто-изменения). Раскрытые foldout (включая foldout самого списка) должны остаться раскрытыми.
8. **Тест C — мульти-селект:** включить чекбокс ещё одной ViewModel в селекторе. Раскрытые foldout первой ViewModel должны остаться. Раскрыть что-нибудь во второй. Снять чекбокс с третьей/любой другой VM, потом включить снова первую — её foldout-state должен оставаться раскрытым (drawer не удалялся, так как чекбокс не снимался).
9. **Тест D — намеренный сброс:** снять чекбокс с ViewModel и поставить заново. Foldout-ы стартуют свёрнутыми — это ожидаемое поведение (drawer удаляется при deselect).
10. **Тест E — DevWidgetEditor не затронут:** открыть префаб с `DevWidget`, раскрыть foldout в Inspector. Поведение должно остаться прежним (то есть тоже стабильным — там drawer и так был долгоживущий).

Если все тесты A-E проходят — checkpoint approved.
  </how-to-verify>
  <resume-signal>Type "approved" or describe issues observed during verification</resume-signal>
</task>

</tasks>

<verification>
- Файл `Editor/ViewModelViewerWindow.cs` компилируется без ошибок.
- `grep -n "_drawerPerViewModel.Clear" Editor/ViewModelViewerWindow.cs` не возвращает совпадений в `RebuildViewModelDisplay`.
- `grep -n "new ViewModelDrawer" Editor/ViewModelViewerWindow.cs` показывает создание drawer'а только в условной ветке "drawer ещё не существует для этой VM".
- Public API `ViewModelDrawer` не изменён (в `Editor/ViewModelDrawer.cs` нет diff).
- Поведение `DevWidgetEditor.cs` не задето (нет diff).
- Ручная верификация foldout-state по чек-листу пройдена.
</verification>

<success_criteria>
- При раскрытии foldout в активной ViewModel и любом из триггеров перестройки (toggle "Show unsupported fields", структурное изменение в `UpdateValues()`, добавление/удаление другой VM в селекторе) — раскрытые foldout остаются раскрытыми.
- Каждая выбранная ViewModel держит свой независимый foldout-state.
- Снятие чекбокса с ViewModel сбрасывает её foldout-state (drawer удаляется); это намеренное поведение.
- `DevWidgetEditor` продолжает работать как раньше.
- Нет регрессий по multi-select (плану `260501-wq7`).
</success_criteria>

<output>
После выполнения — создать `.planning/quick/260501-wyv-viewmodelviewerwindow-fold-foldout-state/260501-wyv-01-SUMMARY.md`.
</output>
