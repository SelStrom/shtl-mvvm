---
status: diagnosed
trigger: "тесты не проинициализированы в проекте sample"
created: 2026-04-30T00:00:00Z
updated: 2026-04-30T00:00:00Z
---

## Current Focus

hypothesis: CONFIRMED — UPM-пакет `com.shtl.mvvm` подключён к Sample через `file:` ссылку, но не добавлен в массив `testables` в `Samples~/Sample/Packages/manifest.json`. Unity Test Runner по умолчанию НЕ сканирует тесты в подключённых через UPM пакетах (исключая embedded в `Packages/`). Asmdef-у `Shtl.Mvvm.Tests.Runtime` поставлен `defineConstraints: ["UNITY_INCLUDE_TESTS"]`, который Unity активирует ТОЛЬКО для пакетов из `testables`. Без этого define сборка тестов не компилируется и тесты не появляются в Test Runner — silent miss.

test: Прочитал все ключевые файлы (manifest.json Sample-а, package.json пакета, asmdef тестов, packages-lock.json).
expecting: Поиск ключа `testables` в обоих манифестах — пусто, т.е. конфигурация для test discovery отсутствует.
next_action: complete diagnosis — return ROOT CAUSE FOUND

## Symptoms

expected: Все EditMode-тесты фазы (ReactiveVirtualListTests, LayoutCalculatorTests, ViewRecyclingPoolTests, VirtualCollectionBindingTests) видны в Unity Test Runner Sample-проекта (Samples~/Sample/) и проходят зелёными.
actual: Тесты не появляются в списке EditMode Test Runner; пользователь сообщает "тесты не проинициализированы в проекте sample".
errors: Не сообщены — Test Runner не показывает тесты как item'ы (silent miss; ожидаемо при `UNITY_INCLUDE_TESTS` define gate + отсутствии package в testables).
reproduction: Открыть Samples~/Sample в Unity Editor, Window → General → Test Runner → EditMode tab. Тесты из Tests/Runtime/ корневого пакета отсутствуют.
started: Discovered during UAT 2026-04-30; Sample-проект потребляет пакет com.shtl.mvvm как UPM-зависимость через `file:../../../`.

## Eliminated

- hypothesis: "Два конфликтующих asmdef в Tests/Runtime (Shtl.Mvvm.Tests.asmdef и Shtl.Mvvm.Tests.Runtime.asmdef)"
  evidence: "ls Tests/Runtime/ показал ОДИН asmdef — только Shtl.Mvvm.Tests.Runtime.asmdef. Shtl.Mvvm.Tests.asmdef не существует. Investigation hint про два asmdef-а не подтвердился."
  timestamp: 2026-04-30T00:00:00Z

- hypothesis: "Test Framework пакет (com.unity.test-framework) не подключен к Sample"
  evidence: "packages-lock.json Sample-а показывает com.unity.test-framework@1.1.33 как транзитивную зависимость через com.unity.feature.development. Test Runner package присутствует."
  timestamp: 2026-04-30T00:00:00Z

- hypothesis: "Asmdef неправильно сконфигурирован (отсутствуют references на TestRunner / nunit)"
  evidence: "Shtl.Mvvm.Tests.Runtime.asmdef содержит references: [Shtl.Mvvm, UnityEngine.TestRunner, UnityEditor.TestRunner], precompiledReferences: [nunit.framework.dll], overrideReferences: true. Конфигурация корректна."
  timestamp: 2026-04-30T00:00:00Z

## Evidence

- timestamp: 2026-04-30T00:00:00Z
  checked: Tests/Runtime/ directory contents
  found: |
    Один asmdef-файл: Shtl.Mvvm.Tests.Runtime.asmdef (с .meta, GUID 14232a95f43d240639f629659b7f9ca2).
    4 .cs тест-файла + .meta: ReactiveVirtualListTests.cs, LayoutCalculatorTests.cs, ViewRecyclingPoolTests.cs, VirtualCollectionBindingTests.cs.
    Никакого второго asmdef (Shtl.Mvvm.Tests.asmdef) НЕТ.
  implication: Investigation hint #2 (про два asmdef-а) не актуален — конфликта между ними нет.

- timestamp: 2026-04-30T00:00:00Z
  checked: Tests/Runtime/Shtl.Mvvm.Tests.Runtime.asmdef
  found: |
    {
      "name": "Shtl.Mvvm.Tests.Runtime",
      "rootNamespace": "Shtl.Mvvm.Tests",
      "references": ["Shtl.Mvvm", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
      "includePlatforms": [],
      "excludePlatforms": [],
      "overrideReferences": true,
      "precompiledReferences": ["nunit.framework.dll"],
      "autoReferenced": false,
      "defineConstraints": ["UNITY_INCLUDE_TESTS"],
      "noEngineReferences": false
    }
  implication: |
    Asmdef сконфигурирован канонически для Unity package tests. Ключевой момент:
    `defineConstraints: ["UNITY_INCLUDE_TESTS"]` — этот define активируется Unity ТОЛЬКО когда
    содержащий пакет находится в `testables` потребляющего проекта (или сам проект — корневой).
    Без активного define ассембли не компилируется и тесты НЕ ОТКРЫВАЮТСЯ Test Runner-ом.
    Это canonical UPM test pattern: ассембли существует только когда тесты явно "включены" пакетом-host.

- timestamp: 2026-04-30T00:00:00Z
  checked: package.json (root, com.shtl.mvvm)
  found: |
    {
      "name": "com.shtl.mvvm", "version": "1.1.0", "unity": "2020.3",
      "dependencies": { "com.unity.ugui": "1.0.0", "com.unity.nuget.newtonsoft-json": "3.2.2" }
    }
    Полный текст файла; поля `testables` НЕТ.
  implication: |
    Поле `testables` в package.json пакета НЕ контролирует discovery в потребляющем проекте — это поле манифеста ПРОЕКТА (Packages/manifest.json), а не пакета. Однако само его отсутствие здесь — не проблема.
    Однако стоит отметить: в манифесте пакета (`package.json`) Unity допускает `testables` для собственных тестов пакета, но это поле имеет другую семантику. Главный gate — в Sample/Packages/manifest.json.

- timestamp: 2026-04-30T00:00:00Z
  checked: Samples~/Sample/Packages/manifest.json
  found: |
    "dependencies": { ..., "com.shtl.mvvm": "file:../../../" }
    Поле `testables` ОТСУТСТВУЕТ. grep -i "testable" не нашёл совпадений ни в одном из манифестов.
  implication: |
    ROOT CAUSE: Sample-проект подключает пакет через `file:` UPM-ссылку (source: "local" в packages-lock.json, depth: 0).
    По правилам Unity Test Runner: тесты из UPM-пакетов (любых: registry, file:, git) НЕ дискаверятся,
    если имя пакета не присутствует в массиве `testables` манифеста проекта.
    Embedded-пакеты (физически лежащие в `Packages/`) — единственное исключение, но `file:` reference
    embedded-ом НЕ считается.

- timestamp: 2026-04-30T00:00:00Z
  checked: Samples~/Sample/Packages/packages-lock.json
  found: |
    "com.shtl.mvvm": { "version": "file:../../../", "depth": 0, "source": "local", ... }
    "com.unity.test-framework": { "version": "1.1.33", "depth": 1, "source": "registry", ... }
  implication: |
    com.unity.test-framework резолвится транзитивно через com.unity.feature.development → значит Test Runner
    UI работает, и стандартные nunit/TestRunner ассембли доступны.
    `source: "local"` (а не embedded в Packages/) подтверждает: пакет рассматривается как UPM-зависимость,
    и без `testables` его тесты невидимы.

- timestamp: 2026-04-30T00:00:00Z
  checked: Samples~/Sample/Assets/Scripts/Sample.asmdef
  found: |
    {"name": "Sample", "references": [GUID:..., GUID:...], "autoReferenced": false, ...}
    Никаких других asmdef в Sample/Assets — только этот один.
  implication: В Sample-проекте нет своего тест-asmdef и нет дублирующих тестовых файлов. Разрешать конфликт нечему.

- timestamp: 2026-04-30T00:00:00Z
  checked: Tests/Editor.meta (orphaned meta без директории)
  found: Файл .meta существует, директория Tests/Editor/ — нет.
  implication: Косметический cleanup-issue, к проблеме discovery отношения не имеет (предупреждение в Editor возможно, но тесты независимо не появятся).

- timestamp: 2026-04-30T00:00:00Z
  checked: Unity official manual — "Add tests to your package" + "Project manifest file"
  found: |
    Из https://docs.unity3d.com/Manual/cus-tests.html и https://docs.unity3d.com/Manual/upm-manifestPrj.html:
    "Unless it's an embedded package located in the Packages folder, tests in packages are not included in the Test Runner by default, so you need to add the package to 'testables' in Packages/manifest.json to include those tests."
    "To manually enable tests for packages developed outside the project's Packages folder, you need to add the testables property to your project manifest."
  implication: Подтверждение: ровно та конфигурация, которая описана в найденных evidence — file:-reference и отсутствие testables — гарантирует silent miss в Test Runner.

## Resolution

root_cause: |
  Sample-проект (`Samples~/Sample/`) подключает пакет `com.shtl.mvvm` через UPM file-reference (`file:../../../` в `Samples~/Sample/Packages/manifest.json`), но в манифесте проекта ОТСУТСТВУЕТ массив `testables` со значением `["com.shtl.mvvm"]`. По правилам Unity Test Runner тесты из UPM-пакетов (любого источника, кроме embedded в `Packages/`) дискаверятся в Test Runner ТОЛЬКО если пакет явно перечислен в `testables` манифеста потребляющего проекта.

  Дополнительный механизм: asmdef тестов (`Tests/Runtime/Shtl.Mvvm.Tests.Runtime.asmdef`) имеет `defineConstraints: ["UNITY_INCLUDE_TESTS"]`. Этот define Unity активирует только при добавлении пакета в `testables` — иначе сборка не компилируется и тестов в Test Runner не видно (silent miss, без ошибок компиляции, потому что constraint фильтрует ассембли целиком).

  Итог: тесты физически на диске и asmdef корректен, но связка manifest.testables ↔ UNITY_INCLUDE_TESTS define-constraint требует явного opt-in в Sample-манифесте, который отсутствует.
fix: (find_root_cause_only mode — fix not applied; suggested direction below)
verification: (find_root_cause_only mode — not applied)
files_changed: []
