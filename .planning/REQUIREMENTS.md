# Requirements: Shtl.Mvvm

**Defined:** 2026-04-09
**Core Value:** Простой и предсказуемый fluent API биндингов, который позволяет декларативно связывать данные с UI без бойлерплейта.

## v1 Requirements

Requirements for v1.2 release. Each maps to roadmap phases.

### Виртуализированный список

- [ ] **VLIST-01**: Виртуализированный список рендерит только видимые элементы + overscan-буфер
- [ ] **VLIST-02**: Элементы переиспользуются через recycling pool при скролле
- [ ] **VLIST-03**: Поддержка вертикальной прокрутки
- [ ] **VLIST-04**: Поддержка элементов переменной высоты/ширины
- [ ] **VLIST-05**: Виртуализированный список интегрируется с ReactiveList<T> через существующие события add/replace/remove
- [ ] **VLIST-06**: Виртуализированный список корректно работает с IWidgetViewFactory для создания/удаления View элементов
- [ ] **VLIST-07**: Zero-alloc в hot path скролла (без аллокаций в каждом кадре)

### Тестирование

- [ ] **TEST-04**: Unit-тесты для виртуализированного списка (recycling, viewport culling, переменные размеры)

### Samples

- [ ] **SMPL-02**: Sample-проект демонстрирует виртуализированный список с большим количеством элементов
- [ ] **SMPL-03**: Sample-проект демонстрирует виртуализированный список с элементами переменной высоты

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Биндинги -- Builder

- **BIND-01**: Bind.From() возвращает структуру-билдер с промежуточным этапом конфигурации до создания подписки
- **BIND-02**: Существующий API `Bind.From(x).To(y)` работает без изменений (обратная совместимость)
- **BIND-03**: Все существующие тесты проходят после рефакторинга биндингов
- **BIND-04**: Билдер поддерживает chained-конфигурацию (направление, конвертеры и др.) до финализации
- **TEST-01**: Все новые фичи разрабатываются по TDD (тесты пишутся до реализации)
- **TEST-02**: Unit-тесты для builder-паттерна биндингов (создание, конфигурация, финализация)

### Биндинги -- Two-Way

- **TWOWAY-01**: Пользователь может создать two-way биндинг через chain-метод `.TwoWay()` в fluent API
- **TWOWAY-02**: Two-way биндинг для InputField синхронизирует текст с ReactiveValue<string> в обоих направлениях
- **TWOWAY-03**: Two-way биндинг для Slider синхронизирует значение с ReactiveValue<float> в обоих направлениях
- **TWOWAY-04**: Two-way биндинг для Toggle синхронизирует состояние с ReactiveValue<bool> в обоих направлениях
- **TWOWAY-05**: Two-way биндинг не вызывает бесконечный цикл обновлений (guard-механизм)
- **TWOWAY-06**: Two-way биндинг корректно очищается при Dispose/Unbind
- **TEST-03**: Unit-тесты для two-way биндингов (синхронизация, guard от цикла, cleanup)
- **SMPL-01**: Sample-проект демонстрирует two-way биндинг (InputField <-> ViewModel <-> текст)

### Биндинги -- Расширенные

- **BIND-V2-01**: Конвертеры значений (.WithConverter()) -- трансформация данных в chain
- **BIND-V2-02**: OneTime режим привязки -- однократная привязка без подписки
- **BIND-V2-03**: Кэширование ранее собранных биндингов -- переиспользование конфигурации

### Виртуализированный список -- Расширенные

- **VLIST-V2-01**: Горизонтальный скролл -- параметризация вертикального
- **VLIST-V2-02**: Grid layout -- сетки для инвентарей, магазинов
- **VLIST-V2-03**: Snap-to-item прокрутка -- пагинация, карусели

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Рефлексия для автобиндинга | GC pressure, IL2CPP stripping, магическое поведение. Явный fluent API -- предсказуемый и читаемый |
| ICommand / AsyncCommand | Overkill для Unity -- `ReactiveValue<Action>` + `ButtonEventBinding` уже покрывают use case |
| DI-интеграция | Нарушает независимость пакета. Widget-паттерн передаёт зависимости явно |
| UIToolkit поддержка | Полная смена парадигмы рендеринга. Отдельный пакет в будущем |
| XAML-разметка биндингов | Unity не поддерживает нативно. Параллельная система описания UI |
| Бесконечный скролл с подгрузкой | Смешивает UI-виртуализацию с бизнес-логикой. ViewModel управляет данными |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| VLIST-01 | Phase 1 | Pending |
| VLIST-02 | Phase 1 | Pending |
| VLIST-03 | Phase 1 | Pending |
| VLIST-04 | Phase 1 | Pending |
| VLIST-05 | Phase 1 | Pending |
| VLIST-06 | Phase 1 | Pending |
| VLIST-07 | Phase 1 | Pending |
| TEST-04 | Phase 1 | Pending |
| SMPL-02 | Phase 2 | Pending |
| SMPL-03 | Phase 2 | Pending |

**Coverage:**
- v1 requirements: 10 total
- Mapped to phases: 10
- Unmapped: 0

---
*Requirements defined: 2026-04-09*
*Last updated: 2026-04-09 after roadmap revision -- Builder и Two-way перенесены в v2*
