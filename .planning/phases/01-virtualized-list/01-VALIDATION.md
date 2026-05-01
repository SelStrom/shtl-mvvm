---
phase: 1
slug: virtualized-list
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-10
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Unity Test Framework (NUnit 3.5) via `com.unity.test-framework` |
| **Config file** | none — Wave 0 installs `Tests/Runtime/Shtl.Mvvm.Tests.asmdef` |
| **Quick run command** | `Unity -runTests -testPlatform EditMode -testFilter Shtl.Mvvm` |
| **Full suite command** | `Unity -runTests -testPlatform EditMode` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `Unity -runTests -testPlatform EditMode -testFilter Shtl.Mvvm`
- **After every plan wave:** Run `Unity -runTests -testPlatform EditMode`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 1-01-01 | 01 | 0 | TEST-04 | — | N/A | setup | Create test asmdef + stubs | ❌ W0 | ⬜ pending |
| 1-02-01 | 02 | 1 | VLIST-04 | — | N/A | unit | NUnit тест LayoutCalculator prefix sum + binary search | ❌ W0 | ⬜ pending |
| 1-02-02 | 02 | 1 | VLIST-01 | — | N/A | unit | NUnit тест LayoutCalculator viewport culling | ❌ W0 | ⬜ pending |
| 1-03-01 | 03 | 1 | VLIST-02 | — | N/A | unit | NUnit тест ViewRecyclingPool Get/Release | ❌ W0 | ⬜ pending |
| 1-04-01 | 04 | 1 | VLIST-03 | — | N/A | unit | NUnit тест VirtualScrollRect logic | ❌ W0 | ⬜ pending |
| 1-05-01 | 05 | 2 | VLIST-05 | — | N/A | unit | NUnit тест VirtualCollectionBinding + ReactiveList | ❌ W0 | ⬜ pending |
| 1-05-02 | 05 | 2 | VLIST-06 | — | N/A | unit | NUnit тест IWidgetViewFactory integration | ❌ W0 | ⬜ pending |
| 1-06-01 | 06 | 3 | VLIST-07 | — | N/A | manual | Unity Profiler — GC.Alloc = 0 в hot path | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Tests/Runtime/Shtl.Mvvm.Tests.asmdef` — assembly definition для тестов (ссылка на Shtl.Mvvm + com.unity.test-framework)
- [ ] `Tests/Runtime/LayoutCalculatorTests.cs` — стабы тестов prefix sum, binary search, variable heights
- [ ] `Tests/Runtime/ViewRecyclingPoolTests.cs` — стабы тестов Get/Release, pool size
- [ ] `Tests/Runtime/ReactiveVirtualListTests.cs` — стабы тестов ViewModel-типа
- [ ] `Tests/Runtime/VirtualCollectionBindingTests.cs` — стабы тестов интеграции с ReactiveList

*If none: "Existing infrastructure covers all phase requirements."*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Zero-alloc hot path скролла | VLIST-07 | Требует Unity Profiler в runtime, GC.Alloc не проверяется автоматически | 1. Открыть сцену с 10000 элементов 2. Начать запись Profiler 3. Скроллить список 4. Проверить GC.Alloc = 0 в кадрах скролла |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
