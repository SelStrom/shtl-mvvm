# Roadmap: Shtl.Mvvm v1.2

## Overview

Расширение MVVM-фреймворка виртуализированным списком с recycling элементов. Милестоун v1.2 сфокусирован на одной ключевой возможности: отображение больших коллекций данных (1000+ элементов) с плавным скроллом и минимальным расходом памяти. Builder-паттерн биндингов и two-way биндинги перенесены в будущие милестоуны.

## Phases

**Phase Numbering:**
- Integer phases (1, 2): Planned milestone work
- Decimal phases (1.1, 1.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Виртуализированный список** - Recycling-список с viewport culling, overscan-буфером и интеграцией с ReactiveList (completed 2026-05-01)
- [ ] **Phase 2: Samples виртуализации** - Sample-проекты, демонстрирующие виртуализированный список

## Phase Details

### Phase 1: Виртуализированный список
**Goal**: Разработчик может отображать большие коллекции данных (1000+ элементов) с плавным скроллом и минимальным расходом памяти
**Depends on**: Nothing (first phase)
**Requirements**: VLIST-01, VLIST-02, VLIST-03, VLIST-04, VLIST-05, VLIST-06, VLIST-07, TEST-04
**Success Criteria** (what must be TRUE):
  1. Список из 10000 элементов скроллится плавно, при этом одновременно в иерархии существует только видимые элементы + overscan-буфер
  2. При скролле View-элементы переиспользуются из recycling pool, а не создаются/уничтожаются
  3. Добавление/удаление элементов в ReactiveList автоматически обновляет виртуализированный список без ручной синхронизации
  4. Элементы переменной высоты корректно позиционируются и скроллятся без визуальных артефактов
  5. Профайлер не показывает GC-аллокаций в hot path скролла (каждый кадр при прокрутке)
**Plans**: 6 plans
Plans:
- [x] 01-01-PLAN.md — ReactiveVirtualList + LayoutCalculator (типы данных и алгоритм позиционирования)
- [x] 01-02-PLAN.md — VirtualScrollRect + ViewRecyclingPool (кастомный скролл и пул View)
- [x] 01-03-PLAN.md — VirtualCollectionBinding + extension .To() (интеграция и fluent API)
- [x] 01-04-PLAN.md — Code review zero-alloc + human verification
- [x] 01-05-PLAN.md — Gap closure: testables opt-in в manifest.json Sample-проекта
- [x] 01-06-PLAN.md — Gap closure: Elastic bounce fix + scroll lock для короткого списка
**UI hint**: yes

### Phase 2: Samples виртуализации
**Goal**: Новые пользователи фреймворка могут изучить все ключевые возможности виртуализации на готовых sample-сценах
**Depends on**: Phase 1
**Requirements**: SMPL-02, SMPL-03
**Success Criteria** (what must be TRUE):
  1. Sample-сцена демонстрирует виртуализированный список с 10000+ элементами и плавным скроллом
  2. Sample-сцена демонстрирует виртуализированный список с элементами разной высоты
**Plans**: TBD
**UI hint**: yes

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Виртуализированный список | 6/6 | Complete    | 2026-05-01 |
| 2. Samples виртуализации | 0/0 | Not started | - |
