# GSD Debug Knowledge Base

Resolved debug sessions. Used by `gsd-debugger` to surface known-pattern hypotheses at the start of new investigations.

---

## three-virtualscroll-test-failures — three independent EditMode test failures
- **Date:** 2026-05-01
- **Error patterns:** FindVisibleRange overscan boundary, MissingReferenceException CountingWidgetView Dispose, fixed-mode B-7 OnElementAdded InsertAt, double-dispose ShiftActiveViewIndices SetContentSize, _layoutCalculator _fixedHeight 0
- **Root cause:** F1: fixed-path FindVisibleRange не корректировал случай "элемент начинается ровно на endPos" (variable-path делал). F2: ShiftActiveViewIndices вызывался ПОСЛЕ SetContentSize, чей callback триггерит UpdateVisibleRange со старыми ключами _activeViews → out-of-range Release+regen → double-dispose. F3: _isFixedLayout инициализировался только в OnContentChanged (RebuildLayout); при последовательных Add'ах в пустой список (когда OnContentChanged не срабатывает) флаг оставался false → InsertAt-ветка → variable-mode навсегда.
- **Fix:** F1: добавлена boundary-correction `if (lastVisible * stride >= endPos) lastVisible--`. F2: ShiftActiveViewIndices перенесён ДО SetContentSize/ScrollPosition в OnElementAdded и OnElementRemoved. F3: при count==1 (первый Add в пустой) явно ставим _isFixedLayout=true, _fixedItemHeight=newHeight. Бонус: чинено диагностическое сообщение теста, обращавшееся к view.name на уже DestroyImmediate'нутом GameObject.
- **Files changed:** Runtime/Core/VirtualScroll/LayoutCalculator.cs, Runtime/Core/Bindings/VirtualCollectionBinding.cs, Tests/Runtime/VirtualCollectionBindingTests.cs
---
