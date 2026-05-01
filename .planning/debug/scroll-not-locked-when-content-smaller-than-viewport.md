---
status: diagnosed
trigger: "Если количество элементов в списке меньше вьюпорта то скролл должен быть заблокирован (UAT phase 01, test 5/6)"
created: 2026-04-30T18:00:00Z
updated: 2026-04-30T18:00:00Z
---

## Current Focus

hypothesis: "OnDrag и OnScroll в VirtualScrollRect.cs модифицируют _scrollPosition без проверки на (contentHeight <= viewportHeight) — следовательно, при коротком содержимом пользователь визуально двигает список, и Elastic-rubber/SmoothDamp возвращают позицию обратно. UI-SPEC требует полной блокировки взаимодействия в этом случае."
test: "Прочитан VirtualScrollRect.cs полностью; проверены все обработчики (OnBeginDrag, OnDrag, OnEndDrag, OnScroll), MaxScrollPosition, ClampScrollPosition, LateUpdate."
expecting: "В OnDrag и OnScroll должны быть guard-проверки `contentHeight <= viewportHeight` (или `maxScroll <= 0`) с ранним выходом — таких guard'ов нет."
next_action: "Diagnose-only: вывести ROOT CAUSE и Suggested Fix Direction для plan-phase --gaps."

reasoning_checkpoint:
  hypothesis: "Отсутствие guard `contentHeight <= viewportHeight` (он же `maxScroll <= 0`) в начале OnDrag и OnScroll позволяет пользователю drag-ом и колесом мыши модифицировать _scrollPosition даже когда содержимое полностью помещается во viewport. Elastic-движок затем возвращает позицию к 0 через SmoothDamp в LateUpdate, создавая визуальный 'эффект пружины' вместо ожидаемой блокировки взаимодействия."
  confirming_evidence:
    - "VirtualScrollRect.cs:182-201 (OnDrag) — никаких guards на пустой/короткий список; _scrollPosition -= delta выполняется безусловно"
    - "VirtualScrollRect.cs:208-228 (OnScroll) — _scrollPosition -= delta безусловно; ClampScrollPosition() (line 299-306) — no-op в Elastic mode (default per UI-SPEC line 72)"
    - "VirtualScrollRect.cs:299-306 — `_scrollPosition = Mathf.Clamp(_scrollPosition, 0f, maxScroll)` выполняется ТОЛЬКО для MovementType.Clamped; в Elastic/Unrestricted ничего не клампится"
    - "VirtualScrollRect.cs:372-376 — MaxScrollPosition() возвращает `Mathf.Max(0f, _contentHeight - ViewportSize)`, т.е. при contentHeight < viewportHeight возвращает 0. То есть maxScroll корректен, но flow управления не использует его как сигнал блокировки"
    - "01-UI-SPEC.md строка 204 — 'Состояния компонента → Пустой список → Drag: Не вызывает изменений'. Это поведенческий контракт, а в коде нет проверки `_vmList.Count == 0` или `contentHeight <= viewportHeight` ни в одном из обработчиков ввода"
    - "RebuildLayout(0) в VirtualCollectionBinding.cs:347 вызывает `_layoutCalculator.Rebuild(0, 1f)`, после чего `_scrollRect.SetContentSize(_layoutCalculator.TotalHeight)` устанавливает _contentHeight = 0. Информация о пустом/коротком списке доступна VirtualScrollRect через _contentHeight, но не используется для блокировки ввода"
  falsification_test: "Добавить лог в начале OnDrag/OnScroll выводящий (_contentHeight, ViewportSize, MaxScrollPosition()), запустить с коротким списком (3 элемента 80px при viewport 600px), сделать drag — если в логе видно, что обработчик отрабатывает и _scrollPosition меняется, гипотеза подтверждена. Если обработчик не вызывается или _scrollPosition не меняется — гипотеза опровергнута."
  fix_rationale: "Минимальный fix: добавить guard `if (_contentHeight <= ViewportSize) return;` (или эквивалент через `MaxScrollPosition() <= 0f`) в начале OnBeginDrag, OnDrag и OnScroll. Это устраняет КОРНЕВУЮ причину — отсутствие проверки контракта 'нет скроллируемого контента => нет реакции на ввод'. Альтернатива через ClampScrollPosition в Elastic режиме недопустима: clamp в Elastic ломает rubber-band за нижней границей при нормальном (длинном) списке."
  blind_spots:
    - "Не проверено реальное PlayMode-поведение в Unity Editor (Unity MCP недоступен в worktree-агенте без Editor); вывод сделан code-only из источника"
    - "Не проверена interaction с inertia: что если _velocity != 0 при переходе списка из длинного в короткий через Remove? SetContentSize:94 обнуляет velocity если _scrollPosition вышел за [0, maxScroll], но если _scrollPosition уже = 0 и список обнулился — velocity остаётся прежним и LateUpdate (line 250-253) применит inertia при _inertia=true. Это потенциально вторая дыра в защите, но НЕ источник симптома, описанного пользователем (он жалуется на возможность drag/wheel при коротком списке, не на остаточную инерцию)"
    - "В OnBeginDrag (line 175-180) обнуляется _velocity и фиксируется _prevDragPosition — без guard'а это создаёт побочный эффект (велосити обнуляется) даже когда drag должен быть проигнорирован. Не критично, но при добавлении guard стоит учесть"
    - "Связанное по UAT gap-2 ('elastic не возвращает в крайнюю позицию') — другое поведение, тестировать отдельно. Возможно связано с OnEndDrag не вычисляющим финальный _velocity (line 203-206 пустой), но это вне scope текущей gap-3"

## Symptoms

expected: |
  Если суммарная высота элементов меньше высоты viewport (contentHeight < viewportHeight, и значит maxScroll == 0), drag и mouse wheel НЕ должны менять _scrollPosition. Согласно UI-SPEC раздел "Состояния компонента → Пустой список": "scroll position = 0, скролл заблокирован (нет контента)". То же должно действовать для непустого, но короткого списка (0 < contentHeight <= viewportHeight).

actual: |
  Пользователь может drag/scroll коротким списком (contentHeight < viewportHeight): drag сдвигает _scrollPosition, виден визуальный отклик. Elastic SmoothDamp возвращает позицию к 0, но факт самой реакции на ввод противоречит контракту "скролл заблокирован".

errors: "None reported"

reproduction: |
  1. Открыть Sample-сцену VirtualListEntryScreen.
  2. Установить количество элементов в ReactiveVirtualList меньше, чем помещается в viewport (например, 3 элемента 80px при viewport 600px).
  3. Запустить Play Mode.
  4. Сделать drag-жест по списку либо прокрутить колесом мыши.
  5. Наблюдать: список визуально реагирует на ввод (Elastic-эффект), хотя по контракту должен быть полностью заблокирован.

started: "2026-04-30 (UAT discovery, phase 01-virtualized-list)"

## Eliminated

- hypothesis: "MaxScrollPosition() возвращает отрицательное значение при contentHeight < viewportHeight, что приводит к Clamp(value, 0, негативное) с непредсказуемым результатом."
  evidence: "VirtualScrollRect.cs:372-376 — `Mathf.Max(0f, _contentHeight - ViewportSize)` гарантирует non-negative. Эта защита уже на месте."
  timestamp: "2026-04-30T18:00:00Z"

- hypothesis: "ClampScrollPosition не вызывается, и поэтому позиция уходит за границы."
  evidence: "ClampScrollPosition() вызывается, но (intentionally) выполняет clamp только в MovementType.Clamped; в Elastic — no-op. Это не баг clamp'а, а отсутствие guard'а ВЫШЕ по флоу. Сам Elastic-возврат в LateUpdate работает корректно (через CalculateOffset + SmoothDamp)."
  timestamp: "2026-04-30T18:00:00Z"

- hypothesis: "W-03 fix scrollbar-clamp как-то взаимодействует с этой проблемой."
  evidence: "UpdateScrollbar (line 326-356) при !needsScroll (contentHeight <= viewportSize) скрывает scrollbar и выходит — он отключён, не источник симптома. Сам же _scrollPosition пользователя меняется через OnDrag/OnScroll, не через scrollbar."
  timestamp: "2026-04-30T18:00:00Z"

## Evidence

- timestamp: "2026-04-30T18:00:00Z"
  checked: "VirtualScrollRect.cs полностью (378 строк)"
  found: |
    OnDrag (line 182-201): обработчик безусловно делает _scrollPosition -= delta + _velocity = -delta / dt. Никаких guards на _contentHeight, _vmList, MaxScrollPosition. Только проверка `if (_movementType == MovementType.Elastic && offset != 0f) delta = RubberDelta(...)` — но при коротком списке и _scrollPosition в [0, 0] CalculateOffset возвращает 0, RubberDelta не применяется, drag сдвигает позицию свободно.
  implication: "Это первое из двух мест, где должен быть guard '_contentHeight <= ViewportSize → return' (раннее завершение обработчика без какого-либо изменения состояния)."

- timestamp: "2026-04-30T18:00:00Z"
  checked: "OnScroll (line 208-228), ClampScrollPosition (line 299-306)"
  found: |
    OnScroll: _scrollPosition -= rawDelta * _scrollSensitivity, затем ClampScrollPosition() + OnScrollPositionChanged. ClampScrollPosition() в дефолтном Elastic-режиме (UI-SPEC line 72) — no-op. Результат: колесо мыши свободно меняет _scrollPosition, Elastic-возврат через LateUpdate срабатывает только потому, что CalculateOffset вычислит ненулевой offset. Контракт "скролл заблокирован" нарушен.
  implication: "Второе место для guard. Симметрично с OnDrag."

- timestamp: "2026-04-30T18:00:00Z"
  checked: "OnBeginDrag (line 175-180)"
  found: |
    Метод обнуляет _velocity и сохраняет _prevDragPosition без проверок. Сам по себе не модифицирует _scrollPosition, но без guard'а отрабатывает на каждом drag-старте, и затем OnDrag модифицирует позицию.
  implication: "Желательно guard'ить и здесь (или достаточно в OnDrag — обнуление velocity нейтрально). Не критично для исправления симптома."

- timestamp: "2026-04-30T18:00:00Z"
  checked: "MaxScrollPosition (line 372-376), SetContentSize (line 86-101)"
  found: |
    MaxScrollPosition защищён `Mathf.Max(0f, ...)`. SetContentSize устанавливает _contentHeight = size, обнуляет velocity если позиция вышла за допустимые [0, maxScroll], вызывает ClampScrollPosition (no-op в Elastic) и OnScrollPositionChanged. То есть состояние '_contentHeight известен и корректен' уже доступно классу — проблема исключительно в отсутствии его use-case'а как сигнала блокировки ввода.
  implication: "Информация для guard'а уже есть в полях класса; добавление проверки требует только if-условия в начале обработчиков."

- timestamp: "2026-04-30T18:00:00Z"
  checked: "VirtualCollectionBinding.cs (как контент-сайз попадает в VirtualScrollRect)"
  found: |
    OnContentChanged/OnElementAdded/OnElementRemoved/OnElementReplaced — все вызывают `_scrollRect.SetContentSize(_layoutCalculator.TotalHeight)`. Для пустого списка RebuildLayout(0) ставит TotalHeight=0 (через Rebuild(0, 1f)). Для короткого списка (n элементов фиксированной высоты h) — TotalHeight = n*h + (n-1)*spacing. Нет специального флага "короткий список" — VirtualScrollRect должен сам сравнить с ViewportSize. Это уже корректно сделано в UpdateScrollbar (line 334), но не сделано в OnDrag/OnScroll.
  implication: "Линия защиты от 'короткого списка' существует только частично (для scrollbar visibility); требуется такая же линия для input-обработчиков. Архитектурно корректное место — внутри VirtualScrollRect, не binding."

- timestamp: "2026-04-30T18:00:00Z"
  checked: "01-UI-SPEC.md раздел 'Состояния компонента' (строки 194-209)"
  found: |
    "### Пустой список (0 элементов) | Scroll position | 0, скролл заблокирован (нет контента) | ... | Drag | Не вызывает изменений |". Контракт явно требует блокировки drag для пустого списка. Случай 0 < contentHeight < viewportHeight в спецификации не покрыт явно, но логически идентичен — если контент полностью видим во viewport, скроллить нечего.
  implication: "Контракт UI-SPEC должен быть расширен на 'короткий список' (contentHeight <= viewportHeight) — это та же ситуация 'нет скроллируемого контента'. Код должен реализовать единый guard."

## Resolution

root_cause: |
  В VirtualScrollRect.cs обработчики ввода OnDrag (line 182-201) и OnScroll (line 208-228) безусловно модифицируют _scrollPosition, не проверяя, есть ли вообще что скроллить. Когда суммарная высота контента не превышает размер viewport (`_contentHeight <= ViewportSize`, эквивалентно `MaxScrollPosition() <= 0f`), скроллить нечего, но обработчики реагируют на ввод и сдвигают позицию. В Elastic-режиме (default per UI-SPEC) ClampScrollPosition() — no-op, поэтому единственное "восстановление" происходит через LateUpdate'овский SmoothDamp обратно к 0. Это создаёт визуальный rubber-band вместо требуемой контрактом полной блокировки взаимодействия. Тот же дефект относится и к строго пустому списку (`_contentHeight == 0`): UI-SPEC раздел "Состояния компонента → Пустой список" явно требует "Drag: Не вызывает изменений", но guard в коде отсутствует.

  Симметрично, OnBeginDrag (line 175-180) выполняется на коротком/пустом списке и обнуляет _velocity — побочный эффект минимальный, но желательно guard'ить и его.

fix: ""
verification: ""
files_changed: []
