---
status: resolved
trigger: "при скролле колесиком мышкой/двумя пальцами на тач паде при крайних значениях скрола при длительном скроле передвижение начинает заикаться. как будто при хендле скрола берется крайнее положение в котором может находиться вьюпор и еще немного отодвигается. При драге одним пальцем такого нет"
created: 2026-05-01T00:00:00Z
updated: 2026-05-01T00:00:00Z
---

## Current Focus

hypothesis: |
  В Elastic-режиме `OnScroll` (wheel/touchpad) сдвигает `_scrollPosition` за границу
  `[0, maxScroll]` БЕЗ применения RubberDelta и БЕЗ сброса `_velocity`. Каждый кадр:
  (1) wheel pushes position past max,
  (2) LateUpdate elastic branch SmoothDamps назад с нетривиальным `_velocity`,
  (3) на следующий кадр velocity carryover влияет на SmoothDamp, и новый wheel push
  начинается уже с другой позиции/скорости.
  Это даёт визуальный judder ("дёргание") при длительном непрерывном wheel-input
  возле границы. Drag не имеет проблемы потому что (а) применяет RubberDelta и
  (б) LateUpdate блокирован `_isDragging` пока drag активен.

test: Прочитать OnScroll/OnDrag/LateUpdate, сравнить с UI-SPEC.
expecting: OnScroll не клампит для Elastic, не сбрасывает velocity → SmoothDamp velocity carryover.
next_action: (resolved)

## Symptoms

expected: |
  При scroll-вводе колесом мыши или двумя пальцами на тачпаде у boundary (top/bottom)
  поведение должно быть плавным: clamp в boundary без визуальных артефактов
  (UI-SPEC: «Mouse Wheel | Инерция от wheel | Нет — мгновенный сдвиг позиции»).
actual: |
  При длительном непрерывном wheel/touchpad scroll возле крайнего положения вьюпорта
  поведение начинает «заикаться» (judder).

  Симптом ВОСПРОИЗВОДИТСЯ:
  - Mouse wheel scroll
  - Two-finger touchpad scroll

  Симптом НЕ воспроизводится:
  - Drag одним пальцем

errors: (визуальный artifact, не exception)
reproduction: |
  1. Открыть Sample-проект, виртуальный список, MovementType=Elastic.
  2. Скроллить колесом/тачпадом непрерывно к краю.
  3. Продолжать вводить wheel events после достижения края — judder.

started: |
  Phase 01 verification зафиксировал gap VLIST-03 «wheel oscillation» (см. 01-VERIFICATION.md).

## Files of interest (initial scope)

- Runtime/Core/VirtualScroll/VirtualScrollRect.cs (OnScroll handler, LateUpdate)
- .planning/phases/01-virtualized-list/01-UI-SPEC.md (Mouse Wheel contract)
- .planning/phases/01-virtualized-list/01-VERIFICATION.md (VLIST-03 gap)

## Eliminated

- ClampScrollPosition в OnScroll НЕ клампит для Elastic (line 340-347: clamp работает
  только при `MovementType.Clamped`). Подтверждено чтением кода.
- LateUpdate elastic branch (line 284-295) корректно запускает SmoothDamp при
  out-of-range — предыдущий fix (commit 3667686+187fd48) это починил для wheel-stuck.
  Текущий баг — про continuous wheel push, а не про stuck.

## Evidence

- timestamp: 2026-05-01T00:00:00Z
  source: VirtualScrollRect.cs:229-255 (OnScroll)
  observation: |
    OnScroll выполняет `_scrollPosition -= delta`, затем `ClampScrollPosition()`.
    Для MovementType.Elastic ClampScrollPosition — no-op. _velocity НЕ сбрасывается.
    Сравнить с OnDrag (line 197-222): применяет RubberDelta при offset != 0,
    устанавливает _velocity = -delta / Time.unscaledDeltaTime.
- timestamp: 2026-05-01T00:00:00Z
  source: VirtualScrollRect.cs:284-295 (LateUpdate elastic branch)
  observation: |
    Mathf.SmoothDamp использует `ref _velocity` — modifies velocity each frame.
    После одного wheel event past-bounds: SmoothDamp за один кадр частично
    приближает _scrollPosition к target (=maxScroll), задавая _velocity ≠ 0.
    Если на следующем кадре приходит ещё один wheel event, position снова
    выпрыгивает за max, но _velocity carries the previous SmoothDamp impulse.
    SmoothDamp в новом кадре стартует с ненулевой velocity, что меняет
    динамику возврата → визуальное «заикание».
- timestamp: 2026-05-01T00:00:00Z
  source: 01-UI-SPEC.md:91-99 (Mouse Wheel section)
  observation: |
    «Инерция от wheel | Нет — мгновенный сдвиг позиции».
    Контракт явно указывает: wheel — мгновенный сдвиг, БЕЗ инерции.
    Текущая реализация нарушает контракт: wheel в Elastic индуцирует
    SmoothDamp velocity (через LateUpdate elastic branch).
- timestamp: 2026-05-01T00:00:00Z
  source: 01-VERIFICATION.md:228 (GAP-2 VLIST-03)
  observation: |
    Verification doc отдельно фиксировал «wheel oscillation» как unresolved gap.
    Текущий user report — то самое зафиксированное состояние.

## Resolution

root_cause: |
  В `OnScroll` для `MovementType.Elastic`/`Unrestricted` `_scrollPosition` после
  вычитания `delta * sensitivity` НЕ клампится в `[0, MaxScrollPosition]`
  (`ClampScrollPosition()` — no-op для Elastic), а `_velocity` НЕ сбрасывается.

  При длительном непрерывном wheel-input возле границы каждый кадр:
  1) `OnScroll` сдвигает `_scrollPosition` за `MaxScrollPosition`.
  2) `LateUpdate` (line 284-295) видит `offset != 0`, запускает `Mathf.SmoothDamp`
     с целью `target = _scrollPosition - offset = maxScroll`. SmoothDamp обновляет
     `_velocity` через `ref _velocity` — велосити становится ненулевой.
  3) На следующем кадре приходит новый wheel event, `OnScroll` снова толкает
     position за max. SmoothDamp на следующем LateUpdate стартует уже с ненулевой
     velocity carryover.

  Накопление velocity carryover + повторное выпрыгивание за границу = визуальное
  «заикание» (judder). У drag судороги нет: (а) `OnDrag` применяет `RubberDelta`,
  ограничивающую сдвиг при offset != 0; (б) `LateUpdate` early-return-ит при
  `_isDragging` — SmoothDamp не запускается во время drag.

  Контракт UI-SPEC (line 98) явно требует: «Инерция от wheel — Нет, мгновенный
  сдвиг позиции». Текущая реализация нарушает этот контракт, поскольку Elastic-
  ветка LateUpdate индуцирует SmoothDamp-velocity на wheel-вводе.

fix: |
  В `OnScroll` после вычисления нового `_scrollPosition`:
  1) Безусловно клампить позицию в `[0, MaxScrollPosition()]` (для всех
     MovementType, включая Elastic) — wheel не должен порождать overscroll.
  2) Сбрасывать `_velocity = 0f` — wheel импульсивный ввод, как и drag-start;
     стэйл damp-velocity из предыдущего кадра не должна влиять на следующее
     LateUpdate.

  Существующий `ClampScrollPosition()` оставлен (он клампит только для Clamped),
  заменён на явный безусловный clamp в OnScroll. Это соответствует UI-SPEC
  «мгновенный сдвиг без инерции» и устраняет обе причины judder:
  – нет out-of-range → LateUpdate elastic branch не запускается на wheel input;
  – нет velocity carryover между кадрами.

  Drag-поведение не затронуто: OnDrag/OnEndDrag/LateUpdate elastic-возврат после
  drag по-прежнему работает по контракту (rubber-band на drag, SmoothDamp на
  release).

verification: |
  Запустить полный EditMode test suite headless: должны пройти 68/68
  (текущий зелёный baseline после bd47a93).
  Ручная верификация: длительный непрерывный wheel/touchpad scroll до края —
  больше нет judder, вьюпор плавно фиксируется в крайней позиции.

files_changed:
  - Runtime/Core/VirtualScroll/VirtualScrollRect.cs
