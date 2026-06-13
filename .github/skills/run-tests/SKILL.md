# SKILL: run-tests

Run specific automated test case(s) by name and produce a dated result folder with a
pass/fail summary.

## When to use

The user asks to run one or more named test cases, e.g.:

> run P0_TC001_LaunchVisualStudioTestBVT

or "run the Launch BVT", "execute LaunchVisualStudioTestBVT", etc.

## What it does

1. Maps each test-case name (`P0_TC001_LaunchVisualStudioTestBVT` or bare
   `LaunchVisualStudioTestBVT`) to
   its xUnit method and runs it via `dotnet test --filter "FullyQualifiedName~<method>"`,
   emitting a TRX log.
2. Creates a dated folder `testRunResult_YYYY-MM-DD/` in the project root.
3. Parses the TRX results and generates `summary.md` containing:
   - **Header:** Run ID + whether PowerShell tests were skipped.
   - **Overall Results:** Total Tests / Passed / Failed / Pass Rate.
   - **Results by Priority:** per-priority totals and pass rate.
   - **Per-priority test-case table:** #, Test Case, Status, Duration, Start, End, Exit Code.

## How to invoke

```powershell
# single test
.github/skills/run-tests/run-tests.ps1 -TestName P0_TC001_LaunchVisualStudioTestBVT

# multiple tests
.github/skills/run-tests/run-tests.ps1 -TestName P0_TC001_LaunchVisualStudioTestBVT,P0_TC002_BuildSolutionTestBVT

# note PowerShell tests are skipped in the summary header
.github/skills/run-tests/run-tests.ps1 -TestName P0_TC001_LaunchVisualStudioTestBVT -SkipPowerShellTests
```

## Output

```
testRunResult_YYYY-MM-DD/
  summary.md                       # the execution summary (tables above)
  <TestName>.trx                   # raw VSTest result per test case
```

## Notes

- Requires the project to build and, for UI tests, a live WinAppDriver + the app under
  test. Without them the test reports FAIL/erroring in the summary (the skill still
  produces the report).
- Priority is inferred from the `P<n>` prefix of the test-case name.
