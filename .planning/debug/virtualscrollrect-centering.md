---
status: resolved
trigger: "В реализации VirtualScrollRect скролящийся контент не центруется во вьюпорте"
created: 2026-04-23
updated: 2026-04-23
---

# Debug Session: virtualscrollrect-centering

## Symptoms

- **Expected behavior:** Скролящийся контент должен оставаться центрированным во вьюпорте при прокрутке
- **Actual behavior:** Контент смещается при скролле — не центрируется
- **Error messages:** Нет
- **Timeline:** Никогда не работало — центрирование не было реализовано с самого начала
- **Reproduction:** Открыть demo сцену (mvvm_demo.unity), запустить Play Mode — смещение видно при скролле

## Current Focus

- hypothesis: PositionView использует абсолютный offset без учёта scrollPosition
- test: Проверить формулу anchoredPosition.y в PositionView
- expecting: anchoredPosition.y = -(offset - scrollPosition), а не -(offset)
- next_action: fix applied
- reasoning_checkpoint: Подтверждено — строка 220 VirtualCollectionBinding.cs использовала абсолютный offset

## Evidence

- timestamp: 2026-04-23 — VirtualCollectionBinding.cs:220: `rt.anchoredPosition = new Vector2(0f, -_layoutCalculator.GetItemOffset(index))` — позиция задается абсолютно без вычитания scrollPosition. При scrollPosition=400 элемент с offset=400 должен быть в верхней части viewport (y=0), но вместо этого оказывается на y=-400.
- timestamp: 2026-04-23 — PositionView вызывается из UpdateVisibleRange, где scrollPosition уже известна через _scrollRect.ScrollPosition, но не передаётся в PositionView.
- timestamp: 2026-04-23 — ViewRecyclingPool создаёт view как дочерние к scrollRect.transform (конструктор, строка 28 VirtualCollectionBinding.cs). Позиционирование элементов происходит относительно scrollRect, а не относительно content container (которого нет в этой архитектуре).

## Eliminated

- LayoutCalculator: GetItemOffset и FindVisibleRange корректно считают позиции и видимый диапазон — тесты подтверждают
- VirtualScrollRect: scrollPosition корректно обновляется при drag/scroll/inertia — callback OnScrollPositionChanged вызывается
- ViewRecyclingPool: recycling работает корректно, Get/Release lifecycle в порядке

## Resolution

- root_cause: PositionView в VirtualCollectionBinding.cs задавал anchoredPosition.y = -(absoluteOffset), не вычитая текущую scrollPosition. Элементы позиционировались на абсолютные координаты в виртуальном контенте, а не относительно текущего viewport, из-за чего при скролле они уплывали за пределы видимой области.
- fix: Изменена формула позиционирования на anchoredPosition.y = -(offset - scrollPosition). Добавлен параметр scrollPosition в метод PositionView. scrollPosition кэшируется в локальную переменную в UpdateVisibleRange для consistency и передаётся во все вызовы PositionView.
- verification: Запустить demo сцену mvvm_demo.unity в Play Mode, добавить элементы и проскроллить — контент должен оставаться в viewport.
- files_changed: Runtime/Core/Bindings/VirtualCollectionBinding.cs
