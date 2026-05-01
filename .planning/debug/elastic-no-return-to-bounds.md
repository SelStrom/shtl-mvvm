---
status: diagnosed
trigger: "При перетягивании drag / прокрутке колесом / тачпадом за границы (Elastic mode) после отпускания viewport не возвращается в крайнюю позицию [0, maxScroll]"
created: 2026-04-30T00:00:00Z
updated: 2026-04-30T00:00:00Z
---

## Current Focus

reasoning_checkpoint:
  hypothesis: "Раннее `return` в `LateUpdate` (`if (_isDragging || _velocity == 0f) return;`) блокирует Elastic-возврат во всех случаях, когда после ухода `_scrollPosition` за границы `_velocity` равен нулю или достигает нуля до того, как SmoothDamp успевает приземлить позицию ровно на границу. `OnScroll` (mouse wheel) не выставляет velocity вовсе, `ClampScrollPosition` для Elastic не делает clamp, поэтому wheel мгновенно даёт перманентный out-of-range. У drag тот же эффект на пути затухания: внутри SmoothDamp velocity падает ниже 1f → принудительно зануляется (line 260-263) → следующий кадр early-return → возврат прерывается, не достигнув offset==0."
  confirming_evidence:
    - "VirtualScrollRect.cs:230-235 — раннее return при `_velocity == 0f` без проверки `CalculateOffset() != 0`. Это значит: позиция `out of [0, maxScroll]` + velocity == 0 → Elastic-ветка не выполняется никогда."
    - "VirtualScrollRect.cs:208-228 (OnScroll, mouse wheel/touchpad) — выставляет `_scrollPosition -= delta`, вызывает `ClampScrollPosition()` (который для Elastic ничего не делает, см. line 299-306) и **не выставляет _velocity**. После одного wheel-overshoot scrollPosition остаётся за границей, velocity == 0 → следующий LateUpdate early-return → застрявший viewport."
    - "VirtualScrollRect.cs:299-306 — `ClampScrollPosition()` clamp'ит только при `_movementType == MovementType.Clamped`. Для Elastic/Unrestricted no-op. Это согласовано с rubber-эффектом во время drag, но в паре с early-return по `velocity == 0f` создаёт мёртвую зону."
    - "VirtualScrollRect.cs:260-263 — `if (Mathf.Abs(_velocity) < 1f) _velocity = 0f;` обнуляет velocity до того, как `CalculateOffset()` достигнет 0. SmoothDamp с большим smoothTime (`elasticity=0.1f`) и медленно снижающейся скоростью часто оставляет velocity < 1f при ещё ненулевом offset → следующий кадр early-return → возврат остановлен где-то между out-of-range позицией и границей."
    - "VirtualScrollRect.cs:203-206 — `OnEndDrag` устанавливает только `_isDragging = false`. Если пользователь отпустил палец на out-of-range без активного движения (delta≈0 в последних OnDrag-кадрах перед отпусканием), `_velocity ≈ 0` → следующий LateUpdate early-return → возврат не запускается."
    - "Samples~/Sample/Assets/Scenes/mvvm_demo.unity:3179 и 4103 — `_movementType: 0` (Elastic — default enum index). То есть на Sample-сцене из UAT действительно активна Elastic-ветка."
    - "REVIEW-FIX W-04 (commit 3075bf1): `SetContentSize` принудительно зануляет velocity при out-of-range. Это лечит один источник застревания (Add/Clear), но усугубляет общий контракт: после такого сброса velocity == 0 + позиция out-of-range → LateUpdate раннний return → SetContentSize эксплицитно теряет шанс на Elastic-возврат вообще."
    - "01-UI-SPEC.md строки 78-82 (Interaction Contract): Elastic bounce контракт — `Mathf.SmoothDamp(position, target, ref velocity, elasticity, Infinity, deltaTime)`. Нет упоминания, что возврат должен зависеть от ненулевой velocity. Контракт нарушен в реализации."
  falsification_test: "Если в LateUpdate заменить early-return-условие на `if (_isDragging) return;` (то есть не early-return-ить при velocity == 0) и при `velocity == 0 && CalculateOffset() != 0 && _movementType == Elastic` инициировать SmoothDamp ветку — тогда после wheel-overshoot или drag-release-with-zero-velocity Elastic возврат должен запуститься и довести позицию до границы. Если этот эксперимент НЕ устраняет застревание — гипотеза неверна и причина в чём-то другом (например, в самом SmoothDamp или в OnScroll-clamp)."
  fix_rationale: "Корневая причина — early-return в LateUpdate инвертирует приоритеты: 'нет velocity → ничего не делать' вместо 'нет velocity и нет out-of-range → ничего не делать'. Правильный гейт: `if (_isDragging || (_velocity == 0f && CalculateOffset() == 0f)) return;`. Это устраняет застревание для всех трёх источников ввода (drag/wheel/touch) одной правкой пути возврата. Дополнительно: порог остановки velocity (line 260-263) должен срабатывать только если позиция уже в границах — иначе Elastic-цикл может не достичь target."
  blind_spots:
    - "Не проверено runtime-поведение в Unity Editor — все выводы из чтения кода."
    - "Не проверено, как ведёт себя SmoothDamp при `_velocity = 0` на старте (теоретически он должен сам набрать скорость в направлении target, но при `smoothTime=0.1f` и `deltaTime≈0.016` приращение очень малое — может быть видно как 'медленный, но возврат идёт', а не 'застрял')."
    - "Не проверено, происходит ли где-то в VirtualCollectionBinding или OnScrollPositionChanged внешний reset velocity, который мог бы дать ту же картину. Беглый grep показал velocity-write только внутри VirtualScrollRect."
    - "Не проверено, влияет ли interaction с UAT issue 'count < viewport → скролл должен быть заблокирован' (gap-3) на эту проблему — может быть смежная причина в UpdateScrollbar/MaxScrollPosition при contentHeight == 0."
  next_action: "Diagnose-only mode → return ROOT CAUSE FOUND."

## Symptoms

expected: При drag/wheel/touch за границу [0, maxScroll] после отпускания viewport SmoothDamp-возвращается в границу (Elastic). Контракт UI-SPEC: Mathf.SmoothDamp(position, target, ref velocity, elasticity, Infinity, deltaTime), порог Abs(velocity) < 1f → velocity = 0
actual: Viewport остаётся "застрявшим" за границей (scrollPosition < 0 или > maxScroll), не возвращается в [0, maxScroll]
errors: None reported
reproduction: UAT Test 5 (mouse wheel за границу), Test 6 (drag за границу) в .planning/phases/01-virtualized-list/01-UAT.md. Воспроизводится для drag, mouse wheel и touch (общий путь возврата)
started: Discovered during UAT 2026-04-30

## Eliminated

- hypothesis: "Дефолтный _movementType может быть не Elastic в Sample-сцене"
  evidence: "В Samples~/Sample/Assets/Scenes/mvvm_demo.unity на строках 3179 и 4103 у обоих VirtualScrollRect значение `_movementType: 0` (= MovementType.Elastic — первый enum). Дефолтный SerializeField тоже Elastic. Сцена работает в Elastic режиме."
  timestamp: 2026-04-30T00:00:00Z

- hypothesis: "Внешний код (VirtualCollectionBinding или ScrollPosition setter) обнуляет velocity и обрывает Elastic"
  evidence: "Grep по `_velocity` в Runtime — присваивания только в VirtualScrollRect.cs (строки 96, 105, 134, 178, 198, 244, 252, 257, 262). VirtualCollectionBinding.cs не пишет в _velocity. Setter `ScrollPosition` (line 45-54) тоже не трогает _velocity. Внешнего обнуления velocity нет — проблема изолирована в VirtualScrollRect."
  timestamp: 2026-04-30T00:00:00Z

## Evidence

- timestamp: 2026-04-30T00:00:00Z
  checked: VirtualScrollRect.cs:230-267 (LateUpdate)
  found: |
    Раннее return на line 232: `if (_isDragging || _velocity == 0f) return;`
    Гейт не проверяет CalculateOffset(). Если `_velocity == 0` И `_scrollPosition` за границами — вся ветка Elastic-SmoothDamp недостижима.
  implication: |
    Любой путь, который оставляет позицию out-of-range с velocity == 0, гарантированно застревает.

- timestamp: 2026-04-30T00:00:00Z
  checked: VirtualScrollRect.cs:208-228 (OnScroll - mouse wheel)
  found: |
    Делает `_scrollPosition -= delta` (line 225), `ClampScrollPosition()` (line 226), `OnScrollPositionChanged()` (line 227).
    НЕ ВЫСТАВЛЯЕТ _velocity. ClampScrollPosition для Elastic — no-op.
  implication: |
    После одного wheel-tick за границу: scrollPosition out-of-range, velocity == 0 → next LateUpdate early-return → застрял.
    Этот путь — самый прямой репро вышеописанной проблемы.

- timestamp: 2026-04-30T00:00:00Z
  checked: VirtualScrollRect.cs:299-306 (ClampScrollPosition)
  found: |
    Clamp выполняется ТОЛЬКО при `_movementType == MovementType.Clamped`. Для Elastic/Unrestricted метод — no-op.
  implication: |
    OnScroll вызывает ClampScrollPosition, но для Elastic это ничего не меняет. Wheel-overshoot не корректируется на месте, и должен корректироваться LateUpdate-ветвью SmoothDamp — которая блокирована early-return.

- timestamp: 2026-04-30T00:00:00Z
  checked: VirtualScrollRect.cs:203-206 (OnEndDrag)
  found: |
    Тело: `_isDragging = false;`. Не запускает явно Elastic-возврат, не выставляет target. Полагается на residual velocity из последних OnDrag-кадров.
  implication: |
    Если в последних OnDrag-кадрах delta была близка к нулю (палец остановился у границы перед отпусканием), velocity ≈ 0 после OnEndDrag → next LateUpdate early-return → застрял на out-of-range.

- timestamp: 2026-04-30T00:00:00Z
  checked: VirtualScrollRect.cs:260-263 (порог остановки velocity)
  found: |
    `if (Mathf.Abs(_velocity) < 1f) _velocity = 0f;` без проверки, что offset == 0.
  implication: |
    Во время Elastic SmoothDamp velocity естественно осциллирует около нуля. Этот порог обнуляет её, не дожидаясь, пока позиция приземлится на границу. Следующий кадр — early-return по `_velocity == 0f`, и оставшийся offset навсегда замораживается.

- timestamp: 2026-04-30T00:00:00Z
  checked: Samples~/Sample/Assets/Scenes/mvvm_demo.unity (lines 3179, 4103)
  found: |
    `_movementType: 0` для обоих VirtualScrollRect в сцене. 0 = первый элемент enum MovementType (Elastic).
  implication: |
    Подтверждено: на UAT-сцене активен Elastic-режим. Симптомы UAT воспроизводятся именно для пути возврата в Elastic.

- timestamp: 2026-04-30T00:00:00Z
  checked: REVIEW-FIX W-04 (commit 3075bf1) и SetContentSize (line 86-101)
  found: |
    SetContentSize при out-of-range принудительно `_velocity = 0f`. Это интенциональный fix W-04 (рывки от старой инерции при Add/Clear).
  implication: |
    После Add/Remove/Clear, который сжимает контент, velocity сбрасывается. Если итоговый scrollPosition out-of-range — early-return в LateUpdate блокирует Elastic-возврат. UAT Test 6 (Add/Remove) описывает ту же симптоматику. W-04 fix корректен в части устранения рывков, но обнажает дефект пути возврата.

- timestamp: 2026-04-30T00:00:00Z
  checked: 01-UI-SPEC.md (Interaction Contract, lines 78-82)
  found: |
    Контракт явно описывает Elastic bounce как `Mathf.SmoothDamp(position, target, ref velocity, elasticity, Infinity, deltaTime)`. Нет условия "только если velocity != 0".
  implication: |
    Реализация нарушает контракт: SmoothDamp должен срабатывать всегда, когда position вне [0, maxScroll] и не идёт активный drag. Текущий гейт по `_velocity == 0f` — это посторонняя оптимизация (T-02-01 mitigation, упомянутая в hints), которая не учитывает out-of-range кейс.

## Resolution

root_cause: |
  Early-return в `VirtualScrollRect.LateUpdate` (строка 232: `if (_isDragging || _velocity == 0f) return;`) блокирует ветку Elastic-SmoothDamp в двух типичных ситуациях:

  1) **Mouse wheel/touchpad**: `OnScroll` (line 208-228) сдвигает `_scrollPosition` на `delta * sensitivity`, вызывает `ClampScrollPosition()` (которая для Elastic — no-op) и **не выставляет `_velocity`**. Если сдвиг ушёл за границу, viewport оказывается в состоянии `scrollPosition out-of-range && _velocity == 0` — следующий `LateUpdate` мгновенно early-return-ит и Elastic-возврат не выполняется никогда.

  2) **Drag**: затухающий SmoothDamp в Elastic-ветке естественно опускает `Abs(velocity)` ниже 1f. Порог в строках 260-263 принудительно зануляет velocity без учёта `CalculateOffset()`. Следующий кадр — early-return → возврат прерывается, не достигнув границы. Тот же сценарий для drag-release с почти нулевой delta в последнем кадре `OnDrag` (палец остановился у границы перед отпусканием).

  Дополнительный усугубляющий фактор: `SetContentSize` (W-04 fix, commit 3075bf1) намеренно зануляет velocity при out-of-range — это устраняет рывки от старой инерции при Add/Clear, но в текущей архитектуре LateUpdate означает, что Elastic-возврат после такого сброса вообще не запустится.

  Корневое архитектурное противоречие: гейт `_velocity == 0f` в LateUpdate был введён как mitigation для другой задачи (T-02-01: не делать пустую работу при отсутствии активной анимации), но не учитывает, что **out-of-range позиция сама по себе является активной "анимацией"** в Elastic-режиме — ей нужен SmoothDamp независимо от текущей velocity.

fix:
verification:
files_changed: []
