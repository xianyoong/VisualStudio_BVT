# fixer.md — Stabilize / Fix a Test Case

Instructions and rules for autonomously running and fixing a single automated test
case until it passes (or the retry budget is exhausted).

**Invocation:** "Using fixer.md as the instructions and rules to run and fix for
`<PRIORITY>_<TCID>_<Name>.txt`" (e.g. `P0_TC009_ForwardMailTestBVT.txt`).

---

## Inputs

- **Test-case spec:** `TestCasesData/<file>.txt` — the source of truth for the steps,
  expected results and assertions.
- **xUnit test:** the method in `test/BVT_Test.cs` whose name matches the case
  (e.g. `ForwardMailTestBVT`).
- **Helpers:** `helper/Helper.cs` (reusable actions, XPath-first).
- **Config:** `config_global.json` (flexible inputs — never hard-code).

## Hard rules (do not violate)

1. **Max 10 retries.** Stop after the 10th fix attempt even if still failing.
2. **Never repeat the same fix.** Track every attempted fix in the log; if a fix was
   already tried, choose a different approach.
3. **Targeted fixes only.** Change the minimum needed for the diagnosed root cause.
   Do not refactor unrelated code.
4. **Never weaken the test to force a pass.** Do not delete steps, remove assertions,
   or replace assertions with no-ops. The steps must still match the `.txt` spec.
5. **Locator policy:** prefer **XPath over ByName**; keep **Wait-then-Act** on every
   interaction.
6. **Log every iteration** to `FixesResult/<testcase>_fix_log.md` (see format below).

---

## The loop (repeat until pass or 10 attempts)

### 1. RUN
```
dotnet test --no-restore --tl:off -v n --filter "FullyQualifiedName~<TestMethod>"
```
Capture the full output. If it passes → go to VALIDATE, then stop.

### 2. DIAGNOSE
- Read the failure: exception type, message, failing step number, stack trace.
- Capture a **UI Dump (XML)** of the live element tree at the point of failure
  (`driver.PageSource`) and save it next to the log, e.g.
  `FixesResult/<testcase>_attempt<N>_uidump.xml`. Use it to see the real
  `Name` / `AutomationId` / `ControlType` of the target control.

### 3. IDENTIFY root cause (pick one)
- **Control mismatch** — locator points at the wrong attribute/value (fix the XPath
  using the real attributes from the UI dump).
- **Not found / timing** — element exists but appears late (increase wait, wait for a
  precursor element, or correct the XPath).
- **Unexpected popup/dialog** — a modal is intercepting input (dismiss/handle it before
  continuing).
- **Assertion mismatch** — the app behaved differently than the `.txt` expects
  (verify whether the spec or the locator is wrong; never silence the assertion).
- **App state** — wrong starting view/folder (navigate to the correct state first).

### 4. FIX
- Apply ONE targeted change addressing the identified root cause.
- Confirm it is **not** a previously logged fix. If it is, pick a different strategy.

### 5. VALIDATE
- Re-run the filtered test (back to RUN).
- On pass, confirm: executed steps still match the `.txt` spec, and all assertions are
  intact and satisfied. Then write the final log entry and stop.

---

## Log format — `FixesResult/<testcase>_fix_log.md`

Append one section per attempt (newest at the bottom):

```markdown
## <testcase>.txt

### Attempt <N> — <yyyy-MM-dd HH:mm:ss>
- **Symptom:** <exception + failing step>
- **UI dump:** FixesResult/<testcase>_attempt<N>_uidump.xml
- **Root cause:** <Control mismatch | Not found | Popup | Assertion | App state>
- **Fix applied:** <one concrete change; file + locator/wait before → after>
- **Result:** <Pass | Fail (carry to next attempt)>
```

End with a final summary line: `RESULT: PASS in <N> attempts` or
`RESULT: GIVE UP after 10 attempts — <remaining symptom>`.
