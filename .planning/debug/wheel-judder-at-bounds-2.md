---
status: awaiting_human_verify
trigger: "дрожание все еще есть (после fix 276b1f2)"
created: 2026-05-01T00:00:00Z
updated: 2026-05-01T00:00:00Z
---

## Current Focus

reasoning_checkpoint:
  hypothesis: |
    Fix 276b1f2 в исходнике корректен и закрывает root cause судорог
    (clamp + velocity=0 в OnScroll → LateUpdate elastic SmoothDamp не запускается
    после wheel events возле границы). НО исполняемая в Sample-Unity DLL устарела:
    `Samples~/Sample/Library/ScriptAssemblies/Shtl.Mvvm.dll` (timestamp `May 1 14:11`)
    старше изменённого `Runtime/Core/VirtualScroll/VirtualScrollRect.cs`
    (`May 1 14:22`, коммит fix'а — `2026-05-01 14:23:19`). User играет в Play Mode
    Sample-проекта на pre-fix DLL — поэтому видит старое поведение.
  confirming_evidence:
    - "ls -la Sample/Library/ScriptAssemblies/Shtl.Mvvm.dll: May 1 14:11"
    - "git log -1 -- Runtime/Core/VirtualScroll/VirtualScrollRect.cs: 2026-05-01 14:23:19"
    - "Headless EditMode test suite: 75/75 PASSED на post-fix исходнике (включая 7 новых
       wheel-regression тестов в Tests/Editor/VirtualScrollRectWheelTests.cs)"
    - "Симуляция OnScroll возле границы (тест ContinuousWheelAtBound_NeverOvershoots)
       подтверждает: 20 events подряд → pos == maxScroll, velocity == 0, никакого overscroll"
    - "Аналитический трейс OnScroll → LateUpdate с post-fix кодом: vel=0+offset=0 → early
       return на line 300-304 → SmoothDamp не запускается → no movement → no judder"
  falsification_test: |
    После forced recompile DLL в Sample-Unity (Assets → Refresh / re-import package)
    user снова репродит judder возле границы wheel-input'ом. Если judder остаётся
    после подтверждённого refresh — гипотеза неверна и нужен новый round investigation.
  fix_rationale: |
    Корневая причина наблюдаемого user'ом behavior — stale DLL, а не недостающий код-fix.
    Корректное действие — НЕ менять корректный код в попытке «починить уже починенное»,
    а: (а) форсировать recompile в Sample-Unity, (б) дать user'у инструкцию верифицировать
    timestamp DLL после refresh. Любое дополнительное изменение Runtime-кода рискует
    исказить уже верифицированную логику и сломать 75/75 зелёных тестов.
  blind_spots: |
    – Sample-Unity открыт (PID 63871). У Unity при открытом Editor file:-package может
      требовать явного refresh для подхвата изменений в исходниках. Я не могу
      программно форсировать это (нет MCP бриджа в Sample-Unity, в TestProject~ —
      отдельный Library, не помогает Sample).
    – Возможен альтернативный сценарий: touchpad sensor noise (микро-shifts ±0.05) даёт
      мини-OnScroll events когда пальцы лежат на тачпаде. Возле границы clamp работает
      ассиметрично (одна сторона заклампится, противоположная пройдёт), что может
      выглядеть как дрожание. Это НЕ закрывается текущим fix'ом — это сенсорный noise
      платформы. Если после recompile судороги остаются именно в "пальцы лежат" (не во
      время активного жеста), нужен будет deadzone на rawDelta.

hypothesis: stale DLL в Sample Library — fix не применён в исполняемом коде
test: сравнение timestamp DLL и исходника
expecting: DLL старше исходника → fix не в исполнении
next_action: (resolved — пользователь должен сделать refresh DLL и повторно проверить)

## Symptoms

expected: |
  При длительном wheel/touchpad scroll-вводе у boundary позиция должна стабильно
  удерживаться у границы без визуальных артефактов.
actual: |
  Дрожание сохраняется. User отчётливо описывает: «при хендле скрола берётся крайнее
  положение... и ещё немного отодвигается».

  Симптом ВОСПРОИЗВОДИТСЯ: wheel/touchpad continuous scroll возле края.
  Симптом НЕ воспроизводится: drag.

errors: визуальный artifact

reproduction: |
  1. Sample, виртуальный список, MovementType=Elastic.
  2. Скроллить колесом непрерывно к краю.
  3. Продолжать вводить wheel events после достижения края.
  4. Дрожание сохраняется даже после commit 276b1f2.

started: |
  После применения первого fix (commit 276b1f2). User: «дрожание все еще есть».

## Eliminated

- hypothesis: Fix 276b1f2 был неполным или неправильным
  evidence: |
    Headless EditMode тесты на post-fix исходнике 75/75 зелёные. Тест
    OnScroll_Elastic_ContinuousWheelAtBound_NeverOvershoots специально симулирует
    20 wheel events подряд возле maxScroll и проверяет (а) pos <= maxScroll на каждом
    шаге, (б) velocity == 0 на каждом шаге, (в) финальная позиция == maxScroll. Все
    проверки проходят. Аналитический трейс кода LateUpdate подтверждает: при vel=0
    и offset=0 срабатывает early return — SmoothDamp не запускается, никакого
    visible движения возле границы.
  timestamp: 2026-05-01T00:00:00Z

- hypothesis: Coupling wheel→OnScrollPositionChanged→SetContentSize меняет content
              size синхронно во время wheel events
  evidence: |
    Прочитан VirtualCollectionBinding.cs целиком. SetContentSize вызывается ТОЛЬКО
    из reactions на изменение ReactiveList: OnContentChanged, OnElementAdded,
    OnElementRemoved, OnElementReplaced. OnScrollPositionChanged (callback на wheel)
    не вызывает SetContentSize и не меняет _contentHeight никаким способом. Цикл
    coupling отсутствует.
  timestamp: 2026-05-01T00:00:00Z

- hypothesis: Re-entrancy через ScrollPosition ReactiveValue subscriber из user-кода
  evidence: |
    grep по ScrollPosition в Runtime и Samples: ни один потребитель не подписывается
    на ReactiveVirtualList.ScrollPosition (это write-only канал из биндинга наружу).
    Цикла нет.
  timestamp: 2026-05-01T00:00:00Z

- hypothesis: Re-entrancy через scrollbar callback (UpdateScrollbar → onValueChanged
              → OnScrollbarValueChanged → ScrollPosition setter)
  evidence: |
    `_updatingScrollbar = true` устанавливается синхронно перед `_scrollbar.value = ...`,
    `OnScrollbarValueChanged` имеет early return по `_updatingScrollbar`, Unity Scrollbar
    onValueChanged вызывается синхронно — re-entrant поток гасится. Edge-case (scrollbar
    only-just-activated SetActive(true)) затрагивает максимум один кадр first-time, не
    sustained judder.
  timestamp: 2026-05-01T00:00:00Z

## Evidence

- timestamp: 2026-05-01T00:00:00Z
  source: ls -la Samples~/Sample/Library/ScriptAssemblies/Shtl.Mvvm.dll
  observation: |
    DLL в Sample/Library — May 1 14:11 (49664 байт).
- timestamp: 2026-05-01T00:00:00Z
  source: ls -la Runtime/Core/VirtualScroll/VirtualScrollRect.cs
  observation: |
    Исходник — May 1 14:22 (17034 байта). На 11 минут НОВЕЕ DLL.
- timestamp: 2026-05-01T00:00:00Z
  source: git log -1 --format="%ai" -- Runtime/Core/VirtualScroll/VirtualScrollRect.cs
  observation: |
    Last commit touching file: 2026-05-01 14:23:19 +0200 (commit 276b1f2 — fix VLIST-03).
    Sample DLL собрана ДО этого коммита.
- timestamp: 2026-05-01T00:00:00Z
  source: TestProject~ headless EditMode run
  observation: |
    test-run testcasecount=75 result=Passed total=75 passed=75 failed=0 inconclusive=0.
    Включая VirtualScrollRectWheelTests (7 тестов), все зелёные. Логика fix'а корректна.
- timestamp: 2026-05-01T00:00:00Z
  source: VirtualScrollRect.OnScroll (post-fix), lines 251-274
  observation: |
    После `_scrollPosition -= delta` идёт безусловный clamp в [0, maxScroll]
    (для ВСЕХ MovementType, не только Clamped) и `_velocity = 0f`. Затем
    OnScrollPositionChanged. На LateUpdate vel=0 и offset=0 → early return на 300-304.
    SmoothDamp в Elastic-ветке (line 306-317) не достигается. Никакого visible
    движения после wheel events возле границы.
- timestamp: 2026-05-01T00:00:00Z
  source: VirtualCollectionBinding.OnScrollPositionChanged (line 196-206)
  observation: |
    Callback на wheel events: устанавливает _vmList.ScrollPosition.Value и зовёт
    UpdateVisibleRange. Никаких вызовов SetContentSize или модификаций _contentHeight.
    UpdateVisibleRange читает scroll position, обновляет active views, но не пишет
    обратно в VirtualScrollRect.
- timestamp: 2026-05-01T00:00:00Z
  source: Sample/Assets/Scenes/mvvm_demo.unity
  observation: |
    `_movementType: 0` (Elastic), `_scrollSensitivity: 35`, `_elasticity: 0.1`.
    Параметры совпадают с тем, что симулирует regression-тест
    ContinuousWheelAtBound_NeverOvershoots — он зелёный.

## Resolution

root_cause: |
  Fix 276b1f2 корректен и решает оригинальную судорогу wheel-judder возле границы
  (clamp + velocity reset в OnScroll, безусловно для всех MovementType). Это
  подтверждается 75/75 зелёных headless EditMode тестов, включая 7 новых
  wheel-regression тестов в VirtualScrollRectWheelTests.

  Причина наблюдаемого user'ом «дрожание ещё есть» — Sample-Unity использует
  pre-fix DLL: `Samples~/Sample/Library/ScriptAssemblies/Shtl.Mvvm.dll` (May 1 14:11)
  старше исходника `Runtime/Core/VirtualScroll/VirtualScrollRect.cs` (May 1 14:22,
  fix-коммит 276b1f2 от 14:23:19). Пакет `com.shtl.mvvm` в Sample/Packages/manifest.json
  установлен через `file:../../../` — UPM кэширует, Unity не всегда пересобирает
  при изменениях исходников file:-пакета без явного refresh.

fix: |
  Никаких изменений в исходном коде НЕ требуется. Fix 276b1f2 уже корректен и
  верифицирован headless-тестами. Действие — пересобрать DLL в открытом
  Sample-Unity:

  1. В Unity Editor (Sample проект), окно Project: правый клик на пакете
     `Shtl.Mvvm` → `Reimport` (или Assets → Refresh, ⌘R).
  2. Альтернативно: закрыть Sample-Unity и открыть заново (Library пересоберётся).
  3. Verify: `ls -la Samples~/Sample/Library/ScriptAssemblies/Shtl.Mvvm.dll` должен
     показать timestamp ПОСЛЕ `May 1 14:22`.
  4. После recompile воспроизвести wheel-input возле границы — judder должен
     исчезнуть.

  Если после verified-recompile судороги остаются — это уже другая причина (наиболее
  вероятно: touchpad sensor noise при «пальцы лежат на тачпаде», требующий deadzone
  на rawDelta). В этом случае открыть новую debug-сессию с repro-trace.

verification: |
  Логика: 75/75 EditMode tests PASSED после fix-коммита 276b1f2 (TestProject~ headless
  на свежесобранном Library). VirtualScrollRectWheelTests покрывают:
  – top bound + wheel up: pos=0, vel=0
  – bottom bound + wheel down: pos=maxScroll, vel=0
  – continuous 20 wheel events at bound: pos<=maxScroll && vel==0 на каждом шаге
  – stale velocity carryover reset on wheel
  – Clamped mode behaviour preserved
  – mid-content shift correctness
  – guard для contentHeight <= viewport

  Manual verification (за пользователем): после recompile воспроизвести judder.

  Self-verified: timestamp DLL и исходника, аналитический трейс OnScroll/LateUpdate,
  test suite green.

  Need user-verified: после Unity refresh judder фактически исчез.

files_changed: []
