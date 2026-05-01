# Phase 1: Виртуализированный список - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-09
**Phase:** 01-virtualized-list
**Areas discussed:** API использования, Размеры элементов, Интеграция со ScrollRect, ReactiveList подключение

---

## API использования

| Option | Description | Selected |
|--------|-------------|----------|
| MonoBehaviour-компонент (Рекомендуется) | VirtualizedListView<TVM, TView> : MonoBehaviour, подключается через Connect(reactiveList) | |
| Fluent API расширение | Расширение Bind.From(list).ToVirtualized(...) аналогично .To() для ElementCollectionBinding | ✓ |
| Комбинированный | MonoBehaviour + fluent extension Bind.From(list).To(virtualizedListView) | |

**User's choice:** Fluent API расширение, но с единым методом `.To()` (без `.ToVirtualized()`).
**Notes:** Пользователь указал на необходимость ViewModel-стороннего реактивного типа (аналог ReactiveList) с viewport state. Нужен анализ use cases: scroll-to-position, видимые элементы, вставка/удаление, анимации. Выделение (selection) — не входит.

---

## Размеры элементов

| Option | Description | Selected |
|--------|-------------|----------|
| Callback-функция (Рекомендуется) | Func<int, float> для расчёта высоты по индексу. Zero-alloc, предсказуемо. | ✓ |
| Автоизмерение | Система измеряет элемент после первого рендера через LayoutRebuilder / RectTransform | |
| На усмотрение Claude | Доверяю выбор исследованию и планированию | |

**User's choice:** Callback-функция
**Notes:** Нет дополнительных уточнений.

---

## Интеграция со ScrollRect

| Option | Description | Selected |
|--------|-------------|----------|
| На базе Unity ScrollRect (Рекомендуется) | Стандартный ScrollRect — инерция, elastic, scrollbar из коробки | |
| Кастомный скролл | Собственный скролл-механизм с нуля — полный контроль | ✓ |
| На усмотрение Claude | Доверяю выбор исследованию | |

**User's choice:** Кастомный скролл
**Notes:** Мотивация — независимость от ограничений ScrollRect, а не специфичные требования к поведению.

---

## ReactiveList подключение

| Option | Description | Selected |
|--------|-------------|----------|
| Виртуализация внутри биндинга (Рекомендуется) | Биндинг занимает единственный слот Connect(). Widget работает с ReactiveList напрямую. | ✓ |
| Мульти-подписка | Расширить ReactiveList до поддержки нескольких подписчиков | |
| Новый реактивный тип | ReactiveVirtualList<T> владеет ReactiveList и добавляет viewport state | |

**User's choice:** Виртуализация внутри биндинга
**Notes:** Уточнено: ReactiveVirtualList<T> содержит ReactiveList<T> внутри, биндинг подключается к ReactiveList через Connect(). Viewport state живёт в отдельных ReactiveValue полях и синхронизируется биндингом.

---

## Claude's Discretion

- Внутренняя архитектура recycling pool
- Алгоритм viewport culling
- Размер overscan-буфера
- Реализация инерции и elastic bounce
- Стратегия кэширования позиций элементов

## Deferred Ideas

None
