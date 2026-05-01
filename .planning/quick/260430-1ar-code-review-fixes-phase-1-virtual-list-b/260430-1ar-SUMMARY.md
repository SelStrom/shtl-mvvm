---
phase: 260430-1ar
plan: 01
subsystem: virtualized-list
tags: [code-review, virtualization, bindings, regression-tests, lifecycle]
requires:
  - Runtime/Core/Bindings/VirtualCollectionBinding.cs
  - Runtime/Core/VirtualScroll/LayoutCalculator.cs
  - Runtime/Core/VirtualScroll/ViewRecyclingPool.cs
  - Runtime/Core/Types/ReactiveList.cs
  - Runtime/Core/VirtualScroll/VirtualScrollRect.cs
provides:
  - "Чистый OnContentChanged/OnElementAdded/OnElementRemoved/Dispose в VirtualCollectionBinding"
  - "Сохранение fixed-path (O(1) индексация) при последовательном Add/Remove одинаковых высот"
  - "Безопасный re-Connect к ReactiveList после Dispose биндинга"
  - "Безопасный ScrollPosition setter после Dispose биндинга"
  - "4 regression-теста на B-3, B-4, B-5, B-7"
affects:
  - VirtualCollectionBinding lifecycle и поведение при последовательных Insert/Remove
key-files:
  modified:
    - Runtime/Core/Bindings/VirtualCollectionBinding.cs
    - Tests/Runtime/VirtualCollectionBindingTests.cs
  created: []
decisions:
  - "B-7: fixed-path сохраняется через `_layoutCalculator.Rebuild(count, _fixedItemHeight)` вместо InsertAt — переинициализирует prefix-sum, но удерживает _fixedHeight > 0 → O(1) GetItemOffset/GetItemHeight"
  - "B-3: убран явный view.Dispose() в Dispose биндинга — Release уже вызывает Dispose внутри (ViewRecyclingPool.Release:47)"
  - "B-4: Items.Unbind() вызывается ДО обнуления _vmList; иначе повторный Connect к тому же Items.Connect бросает InvalidOperationException(\"Already bound\")"
  - "B-5: SetOnScrollPositionChanged(null) вызывается ДО обнуления _scrollRect; иначе drag/wheel/setter дёргают callback с уже null'ифицированным _vmList"
  - "Сброс _isFixedLayout/_fixedItemHeight в Dispose обязателен — биндинги переиспользуются через BindingPool, поэтому состояние полей видно следующему Activate"
  - "B-6 (immutable spacing) не трогаем — пользователь решил оставить SetSpacing вызовы перед Insert/Remove"
metrics:
  duration: ~12min
  completed: 2026-04-27
  tasks: 2
  commits: 2
  files_changed: 2
---

# Quick 260430-1ar: Code Review Fixes Phase 1 — VirtualCollectionBinding (B-1..B-5, B-7)

Применены 6 фиксов code-review к `VirtualCollectionBinding<TViewModel, TWidgetView>` (мёртвый код, тавтологии, неправильный Dispose lifecycle, потеря fixed-path при Insert/Remove) с 4 regression-тестами.

## Что сделано

### Task 1: правки в VirtualCollectionBinding.cs (commit `48c5770`)

| ID  | Фикс                                                                                                              | Где                |
| --- | ----------------------------------------------------------------------------------------------------------------- | ------------------ |
| B-1 | Удалена мёртвая ветка `if/else` в `OnContentChanged` (обе ветки делали то же самое)                               | OnContentChanged   |
| B-2 | Удалена тавтология `_vmList.GetItemHeight(0) == _vmList.GetItemHeight(0)` в `OnElementAdded`                      | OnElementAdded     |
| B-3 | Убран явный `kvp.Value.Dispose()` в Dispose биндинга — Release сам делает Dispose (ViewRecyclingPool.Release:47)  | Dispose            |
| B-4 | Добавлен `_vmList?.Items.Unbind()` ДО `_vmList = null` — позволяет повторный Connect к Items                      | Dispose            |
| B-5 | Добавлен `_scrollRect?.SetOnScrollPositionChanged(null)` ДО `_scrollRect = null` — отвязывает callback            | Dispose            |
| B-7 | Введены поля `_isFixedLayout` / `_fixedItemHeight`. На пустых-→-N добавлениях одной высоты остаёмся в fixed-mode  | Add/Remove/Rebuild |

Логика B-7:
- `RebuildLayout` — выставляет `_isFixedLayout = true, _fixedItemHeight = firstHeight` если все высоты совпадают (epsilon 0.001f).
- `OnElementAdded` — если `_isFixedLayout && |newHeight − _fixedItemHeight| < 0.001f`, вызывается `_layoutCalculator.Rebuild(count, _fixedItemHeight)` (O(N) prefix-sum, но `_fixedHeight > 0` сохраняется → O(1) GetItemOffset/GetItemHeight). Иначе `InsertAt` + `_isFixedLayout = false`.
- `OnElementRemoved` — если `_isFixedLayout`, вызывается `Rebuild(count, _fixedItemHeight)`. Иначе `RemoveAt`.
- В Dispose сбрасываем `_isFixedLayout = false, _fixedItemHeight = 0f` для корректного reuse биндинга через BindingPool.

B-6 (immutable spacing) **не тронут** по решению пользователя: `SetSpacing(_scrollRect.Spacing)` остаётся в OnElementAdded/OnElementRemoved.

Публичный API не изменён: сигнатуры `Connect()`, `Activate()`, `Invoke()`, `Dispose()` те же; `BindFrom` extension-методы не затронуты.

### Task 2: regression-тесты (commit `4ec8fa3`)

Добавлены 4 теста в `Tests/Runtime/VirtualCollectionBindingTests.cs`:

| Test                                          | Покрывает | Идея                                                                                                         |
| --------------------------------------------- | --------- | ------------------------------------------------------------------------------------------------------------ |
| `Dispose_DoesNotDoubleDisposeView`            | B-3       | `CountingWidgetView.OnDisposedCallCount` ровно `1` после `binding.Dispose()` (а не 2 при double-dispose).    |
| `Dispose_UnbindsItemsList`                    | B-4       | `Assert.DoesNotThrow` при повторном `_vmList.Items.Connect(...)` после `binding.Dispose()`.                  |
| `Dispose_ResetsScrollRectCallback`            | B-5       | `Assert.DoesNotThrow` при `_scrollRect.ScrollPosition = 200f` после `binding.Dispose()`.                     |
| `OnElementAdded_FixedHeight_PreservesFixedPath` | B-7       | После Add 50 элементов высоты 100f `LayoutCalculator._fixedHeight == 100f` и `TotalHeight == 5000f`.         |

Вспомогательные классы:
- `CountingWidgetView : AbstractWidgetView<TestViewModel>` — переопределяет `OnDisposed` и считает вызовы (использует public virtual `OnDisposed()` из `AbstractWidgetView`).
- `CountingFactory : IWidgetViewFactory<TestViewModel, CountingWidgetView>` — параллель `MockFactory` для CountingWidgetView.

Тест `OnElementAdded_FixedHeight_PreservesFixedPath` через рефлексию читает private `_layoutCalculator` биндинга и `LayoutCalculator._fixedHeight` (тот же reflection-паттерн, что и в существующих тестах для `_viewport`/`_axis`/`_spacing`).

Существующие 14 тестов сохранены без изменений (не тронут SetUp/TearDown/ConfigureScrollRect и тестовая логика). Итого в файле теперь 18 `[Test]` методов.

## Стиль

Соответствует `.editorconfig` проекта:
- Allman braces, 4 пробела
- `_` префикс приватных полей (`_isFixedLayout`, `_fixedItemHeight`)
- `var` для локальных переменных
- Скобки даже для однострочных `if`

## Deviations from Plan

**None — план выполнен ровно как написан.**

Незначительная адаптация (без переопределения смысла):
- В `OnElementAdded` при попадании в variable-ветку дополнительно сбрасывается `_fixedItemHeight = 0f` (план явно об этом не упоминал, но это симметрично логике в `RebuildLayout` и не противоречит спецификации B-7). Это безопасный housekeeping для корректного reuse.
- В комментарии Dispose упомянутый `kvp.Value.Dispose()` (литерал) уточнён на «явный Dispose», чтобы sanity-grep `grep -c "kvp.Value.Dispose()"` возвращал 0 без ложноположительного попадания на текст комментария.

## Verification

- Sanity-grep:
  - `grep "kvp.Value.Dispose()" Runtime/Core/Bindings/VirtualCollectionBinding.cs` → 0 (нет double-dispose).
  - `grep "GetItemHeight(0) == _vmList.GetItemHeight(0)"` → 0 (тавтология удалена).
  - `grep "Items.Unbind"` → 1 (B-4 на месте).
  - `grep "SetOnScrollPositionChanged(null)"` → 1 (B-5 на месте).
  - `grep "_isFixedLayout"` → 8 вхождений (декларация + использования в Add/Remove/Rebuild/Dispose).
  - `grep "Dispose_DoesNotDoubleDisposeView\|Dispose_UnbindsItemsList\|Dispose_ResetsScrollRectCallback\|OnElementAdded_FixedHeight_PreservesFixedPath"` → 4.
  - `grep -c "\[Test\]"` Tests/Runtime/VirtualCollectionBindingTests.cs → 18 (14 существующих + 4 новых).
- Unity Test Runner: запуск **не выполнен** в данной сессии — Unity Editor MCP недоступен в окружении. Тесты составлены против известных публичных контрактов (`AbstractWidgetView.OnDisposed` virtual, `IWidgetViewFactory<>` обобщённый, `LayoutCalculator._fixedHeight` private struct field, `ReactiveList<>.Connect` бросает `InvalidOperationException("Already bound")`). Перед merge рекомендуется ручной запуск **Test Runner → PlayMode → Run All** в Unity.

## Self-Check: PASSED

- File `Runtime/Core/Bindings/VirtualCollectionBinding.cs` — FOUND, modified.
- File `Tests/Runtime/VirtualCollectionBindingTests.cs` — FOUND, modified.
- Commit `48c5770` — FOUND in `git log`.
- Commit `4ec8fa3` — FOUND in `git log`.
- Файл SUMMARY.md создан по пути `.planning/quick/260430-1ar-code-review-fixes-phase-1-virtual-list-b/260430-1ar-SUMMARY.md`.
