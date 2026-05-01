# Shtl.Mvvm

## What This Is

MVVM-фреймворк для Unity с чётким разделением слоёв: Model, Widget, ViewModel, View. UPM-пакет (`com.shtl.mvvm`), совместимый с Unity 2020.3+. Используется в личном проекте с небольшой командой, в перспективе — публичный open source пакет.

## Core Value

Простой и предсказуемый fluent API биндингов (`Bind.From().To()`), который позволяет декларативно связывать данные с UI без бойлерплейта.

## Requirements

### Validated

- ✓ Реактивные типы: ObservableValue, ReactiveValue, ReactiveList, ReactiveAwaitable — existing
- ✓ Fluent binding API: Bind.From().To() с extension-методами для Model→ViewModel, UI→ViewModel, ViewModel→UI — existing
- ✓ Базовые классы: AbstractViewModel, AbstractWidgetView — existing
- ✓ Автоматическое управление жизненным циклом биндингов (Dispose, Unbind) — existing
- ✓ Пулирование объектов биндингов (BindingPool) — existing
- ✓ Поддержка вложенных ViewModel через композицию — existing
- ✓ ReactiveList с синхронизацией коллекций и событиями add/replace/remove — existing
- ✓ ReactiveAwaitable для ожидания анимаций — existing
- ✓ IWidgetViewFactory для создания/удаления View элементов списка — existing
- ✓ Editor-инструменты: ViewModel Viewer, DevWidget — existing
- ✓ Совместимость с Unity 6 (ugui вместо TextMeshPro) — existing

### Active

- [ ] Рефакторинг биндингов: структура-билдер внутри Bind.From(), ленивое создание, кэширование ранее собранных биндингов
- [ ] Two-way биндинги: новый chain-метод для двусторонней связи ViewModel↔View
- [ ] Виртуализированный список: recycling элементов, произвольные размеры, viewport culling, вертикальный/горизонтальный/grid

### Out of Scope

- UIToolkit поддержка — фреймворк построен на uGUI, миграция на UIToolkit выходит за рамки текущего милестоуна
- DI-интеграция — Widget-паттерн намеренно не зависит от DI-контейнеров
- Сетевая синхронизация ViewModel — не является задачей UI-фреймворка

## Context

- Текущая версия: 1.1.0 (UPM пакет `com.shtl.mvvm`)
- Биндинги сейчас создаются in-place через extension-методы, нет промежуточного этапа конфигурации
- Two-way связь отсутствует — все биндинги однонаправленные (Model→ViewModel→View), обратная связь только через callbacks
- Списки рендерят все элементы сразу, нет виртуализации для больших коллекций
- API биндингов должен остаться обратно совместимым: `Bind.From(x).To(y)` должен работать как раньше
- Завершение конструирования билдера: по следующему `Bind.From()` или по финализации всех билдеров

## Constraints

- **Совместимость**: Unity 2020.3+ — нельзя использовать API, недоступные в старых версиях
- **uGUI**: Работа с uGUI (UnityEngine.UI), не UIToolkit
- **Zero-alloc**: Минимизация аллокаций в hot path (binding pool, структуры вместо классов где возможно)
- **Обратная совместимость API**: Существующий `Bind.From().To()` API не должен ломаться

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Билдер-структура вместо Create/Build | Сохранение лаконичного fluent API без бойлерплейта | — Pending |
| Ленивое создание биндингов | Позволяет конфигурировать (TwoWay и др.) до фактического создания подписки | — Pending |
| Виртуализированный список как часть фреймворка | Списки — основной use case UI, нужен из коробки | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-04-09 after initialization*
