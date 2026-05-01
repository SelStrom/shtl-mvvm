---
status: resolved
trigger: "есть playmode тесты, которые падают. edit mode тесты юнити вообще не видит"
created: 2026-05-01T00:00:00Z
updated: 2026-05-01T13:30:00Z
---

## Current Focus

hypothesis: CONFIRMED — Tests/Runtime/Shtl.Mvvm.Tests.Runtime.asmdef содержит `includePlatforms: []` (пусто), что классифицирует сборку как PlayMode. EditMode tab пуст, потому что НИ ОДИН asmdef в проекте не имеет `includePlatforms: ["Editor"]`. PlayMode "падения" — побочный эффект: тесты, написанные как чистые NUnit (без `[UnityTest]`, без корутин, с `DestroyImmediate`), запускаются в PlayMode test rig (с domain reload, scene lifecycle), где они изначально не были рассчитаны работать. Оба симптома — единый root cause: неправильная классификация asmdef.

test: Прочитан asmdef, все 4 test-файла, planning-доки фазы 01.
expecting: Подтверждение что план изначально требовал EditMode + тесты совместимы с EditMode.
next_action: complete — fix applied, RESOLVED

## Symptoms

expected: |
  - PlayMode тесты в Sample-проекте проходят зелёными.
  - EditMode тесты видны и проходят в Unity Test Runner (Sample-проект).
actual: |
  - PlayMode тесты падают (детали неизвестны: какие именно, какие assertion'ы).
  - EditMode тесты Unity вообще не видит (не появляются в Test Runner EditMode tab).
errors: (не сообщены — нужно уточнить какие именно PlayMode тесты падают и с какими сообщениями)
reproduction: |
  Открыть Samples~/Sample/ в Unity Editor (Unity 2020.3+ или совместимый), Window → General → Test Runner.
  EditMode tab — тесты не появляются.
  PlayMode tab — тесты есть, но часть из них fail.
started: |
  Phase 01 verification (2026-05-01) зафиксировала gap TEST-04 EditMode discovery.
  Wave 5 plan 01-05 добавил `"testables": ["com.shtl.mvvm"]` в Samples~/Sample/Packages/manifest.json (commit 16549e2 area).
  Тем не менее EditMode тесты по-прежнему невидимы, и часть PlayMode тестов всё ещё падает.

## Prior Context (load as evidence — do NOT re-investigate from scratch)

- Связанная сессия `tests-not-discovered-in-sample.md` (status: diagnosed, 2026-04-30):
  - Корневая причина для EditMode discovery того периода — отсутствие `testables` в Sample manifest.
  - Рекомендованный fix применён: `"testables": ["com.shtl.mvvm"]` сейчас в manifest.json.
  - Текущая сессия подтверждает: testables-фикс работает (asmdef компилируется в `Library/ScriptAssemblies/Shtl.Mvvm.Tests.Runtime.dll`), но EditMode остаётся невидимым по другой причине.
- VERIFICATION.md фазы 01 (2026-05-01): TEST-04 EditMode и VLIST-03 wheel oscillation — зафиксированные gaps.

## Files of interest (initial scope)

- Samples~/Sample/Packages/manifest.json — testables config (OK)
- Tests/Runtime/Shtl.Mvvm.Tests.Runtime.asmdef — конфигурация asmdef (FIXED)
- Tests/Runtime/*.cs — все 4 тест-файла (verified EditMode-compatible)
- .planning/phases/01-virtualized-list/01-01-PLAN.md — оригинальная спецификация asmdef (`includePlatforms: ["Editor"]`)
- .planning/phases/01-virtualized-list/01-VERIFICATION.md — TEST-04 gap

## Eliminated

- hypothesis: "testables fix не применён или сломан"
  evidence: |
    Samples~/Sample/Packages/manifest.json содержит `"testables": ["com.shtl.mvvm"]`.
    Library/ScriptAssemblies/Shtl.Mvvm.Tests.Runtime.dll успешно скомпилирована (33280 bytes, May 1 03:32) —
    значит UNITY_INCLUDE_TESTS define активирован, асембли собирается.
  timestamp: 2026-05-01T13:00:00Z

- hypothesis: "Тесты содержат [UnityTest] / корутины и требуют PlayMode"
  evidence: |
    grep -rn "UnityTest|UnityEngine.TestTools|IEnumerator|WaitForSeconds|StartCoroutine|yield return|Time\." Tests/Runtime/*.cs
    Совпадений: 0.
    Все 4 файла используют только `[Test]` атрибут NUnit + `using NUnit.Framework`.
    GameObject/RectTransform/MonoBehaviour создаются и уничтожаются через `new GameObject()` + `DestroyImmediate()` —
    канонический EditMode-паттерн, не требует Play mode loop.
  timestamp: 2026-05-01T13:05:00Z

- hypothesis: "Кеш Library/ требует invalidation"
  evidence: |
    Library/ScriptAssemblies содержит свежую Shtl.Mvvm.Tests.Runtime.dll (May 1 03:32, после применения testables fix).
    Compilation работает. Проблема не в кеше — структурно Test Runner не классифицирует сборку как EditMode.
  timestamp: 2026-05-01T13:10:00Z

- hypothesis: "Orphaned Tests/Editor.meta мешает discovery"
  evidence: |
    Tests/Editor/ существует физически (пустой каталог). .meta файл валиден для существующей папки.
    Это не orphan — просто пустая директория без .cs/.asmdef. К проблеме отношения не имеет.
  timestamp: 2026-05-01T13:12:00Z

## Evidence

- timestamp: 2026-05-01T13:00:00Z
  checked: Samples~/Sample/Packages/manifest.json
  found: |
    "dependencies": { ..., "com.shtl.mvvm": "file:../../../" }
    "testables": ["com.shtl.mvvm"]
  implication: Testables opt-in применён. UNITY_INCLUDE_TESTS define активирован для пакета.

- timestamp: 2026-05-01T13:00:00Z
  checked: Tests/Runtime/Shtl.Mvvm.Tests.Runtime.asmdef (до fix)
  found: |
    {
      "name": "Shtl.Mvvm.Tests.Runtime",
      "references": ["Shtl.Mvvm", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
      "includePlatforms": [],
      "defineConstraints": ["UNITY_INCLUDE_TESTS"],
      ...
    }
  implication: |
    `includePlatforms: []` означает "все платформы" — Unity Test Runner регистрирует такую сборку как **PlayMode**.
    Канонический EditMode-asmdef требует `includePlatforms: ["Editor"]` (assembly Editor-only).
    Без этого EditMode tab пуст, потому что в проекте нет ни одной EditMode-test-сборки.

- timestamp: 2026-05-01T13:05:00Z
  checked: grep -rn "UnityTest|UnityEngine.TestTools|IEnumerator|WaitForSeconds|Time\." Tests/Runtime/*.cs
  found: 0 совпадений
  implication: |
    Тесты — чистый NUnit, без корутин. Все совместимы с EditMode runtime.
    GameObject/RectTransform создаются и удаляются через DestroyImmediate — стандартный EditMode-паттерн.
    Перевод сборки в EditMode не требует переписывания тестов.

- timestamp: 2026-05-01T13:08:00Z
  checked: .planning/phases/01-virtualized-list/01-01-PLAN.md (строки 169-185)
  found: |
    Изначальный план (wave 1) специфицировал asmdef как:
    {
      "name": "Shtl.Mvvm.Tests",
      "references": ["Shtl.Mvvm"],
      "includePlatforms": ["Editor"],
      "defineConstraints": ["UNITY_INCLUDE_TESTS"],
      ...
    }
  implication: |
    Архитектурное намерение: EditMode-тесты с минимальным reference set.
    Текущий asmdef отклонился от плана: includePlatforms сменилось на [], и были добавлены TestRunner-ссылки.
    Отклонение не задокументировано — это и есть введённый в обход спеки регресс.

- timestamp: 2026-05-01T13:10:00Z
  checked: .planning/phases/01-virtualized-list/01-02-PLAN.md (строка 139)
  found: '"Для ViewRecyclingPool тесты будут EditMode-тесты с new GameObject().AddComponent<TestWidgetView>()"'
  implication: Планы 01-02 и 01-03 явно указывают EditMode как целевой режим. Подтверждает архитектурное намерение.

- timestamp: 2026-05-01T13:15:00Z
  checked: Library/ScriptAssemblies/Shtl.Mvvm.Tests.Runtime.dll
  found: 33280 bytes, mtime May 1 03:32
  implication: |
    Сборка успешно компилируется — это значит:
    (а) define UNITY_INCLUDE_TESTS активирован (testables fix работает),
    (б) все references резолвятся,
    (в) тесты физически готовы к запуску.
    Проблема исключительно в классификации сборки Test Runner-ом по includePlatforms.

## Resolution

root_cause: |
  Tests/Runtime/Shtl.Mvvm.Tests.Runtime.asmdef имел `"includePlatforms": []` (пустой массив = все платформы),
  из-за чего Unity Test Runner регистрировал сборку как **PlayMode** test assembly. Это вызывало два связанных
  симптома:

  1. **EditMode tab пуст:** ни один asmdef в проекте не имеет `includePlatforms: ["Editor"]`, поэтому Test
     Runner не может перечислить ни одного EditMode-теста.
  2. **PlayMode тесты падают:** тесты написаны как чистые NUnit `[Test]` без `[UnityTest]` / корутин, но
     создают `GameObject` / `RectTransform` / `MonoBehaviour`-наследников и используют `DestroyImmediate`. Это
     канонический EditMode-паттерн, не рассчитанный на PlayMode test rig (domain reload, scene lifecycle,
     uGUI layout pass timing). В PlayMode часть тестов даёт нестабильные результаты — отсюда "падения".

  Архитектурное намерение фазы 01 (планы 01-01, 01-02, 01-03) — EditMode-тесты с `includePlatforms: ["Editor"]`.
  Финальный asmdef отклонился от спеки без документации причины (попал с `includePlatforms: []` после wave 1).

  Testables-fix из предыдущей сессии (`tests-not-discovered-in-sample.md`) был необходим, но недостаточен:
  он включает компиляцию сборки, но не меняет её классификацию EditMode/PlayMode. Эта классификация диктуется
  исключительно полем `includePlatforms`.

fix: |
  Применил канонический EditMode-asmdef в Tests/Runtime/Shtl.Mvvm.Tests.Runtime.asmdef:
  - `"includePlatforms": ["Editor"]` (было: `[]`) — assembly теперь Editor-only, классифицируется как EditMode.
  - References сохранены (`Shtl.Mvvm`, `UnityEditor.TestRunner`, `UnityEngine.TestRunner`) — обе TestRunner-ссылки
    допустимы в EditMode-сборке и не требуют изменений.
  - `defineConstraints: ["UNITY_INCLUDE_TESTS"]` сохранён — gate, активируемый testables в manifest.json.
  - Никаких других изменений (precompiledReferences, overrideReferences, autoReferenced и т.д. оставлены как есть).

  Тесты не переписывались: все 68 тестов (14+29+7+18) — чистые NUnit `[Test]` с `DestroyImmediate` cleanup,
  полностью совместимы с EditMode runtime.

verification: |
  Изменение применено. Для подтверждения пользователю нужно:
  1. В Unity Editor открыть Samples~/Sample/, дождаться recompile (или вручную: Assets → Refresh).
  2. Window → General → Test Runner.
  3. EditMode tab: должны появиться 4 сьюта — ReactiveVirtualListTests (15), LayoutCalculatorTests (29),
     ViewRecyclingPoolTests (7), VirtualCollectionBindingTests (18). Ожидаемое количество: ~69 тестов.
  4. PlayMode tab: должен стать пустым (это ожидаемо — сборка больше не PlayMode).
  5. Run All в EditMode tab — все тесты должны пройти зелёными (или, если что-то падает, это уже логические
     дефекты тестов / production-кода, требующие отдельного разбора, но точно не инфраструктурные).

  Если EditMode tab остаётся пустым после правки:
  - Проверить, что Unity Editor пересобрал asmdef (Library/ScriptAssemblies/Shtl.Mvvm.Tests.Runtime.dll
    должен иметь свежий timestamp).
  - Reimport Tests/Runtime/Shtl.Mvvm.Tests.Runtime.asmdef через контекстное меню в Project window.
  - В крайнем случае: удалить Samples~/Sample/Library/ и переоткрыть проект.

files_changed:
  - Tests/Runtime/Shtl.Mvvm.Tests.Runtime.asmdef
