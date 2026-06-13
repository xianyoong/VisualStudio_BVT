# format-fix-log.prompt.md — Fix Log Output Format

Canonical output format for the logs produced by `prompts/fixer.md`. Every fix run must
write to `FixesResult/<testcase>_fix_log.md` using exactly this structure.

## Rules

- **Append-only.** Never edit or delete earlier attempts; add new attempts at the bottom.
- **One section per attempt**, numbered sequentially starting at 1.
- **Reference the UI dump** captured for that attempt.
- **Record the exact fix** (file + locator/wait, before → after) so repeats are detectable.
- End the file with a single `RESULT:` line.

## Template

```markdown
# Fix Log — <testcase>.txt

## <testcase>.txt

### Attempt <N> — <yyyy-MM-dd HH:mm:ss>
- **Symptom:** <exception type + message + failing step number>
- **UI dump:** FixesResult/<testcase>_attempt<N>_uidump.xml
- **Root cause:** <Control mismatch | Not found/timing | Popup | Assertion | App state>
- **Fix applied:** <file>: <locator/wait>  `before` → `after`
- **Result:** <Pass | Fail (carry to next attempt)>

<!-- repeat the Attempt block for each retry, up to 10 -->

RESULT: <PASS in N attempts | GIVE UP after 10 attempts — remaining symptom>
```

## Example

```markdown
# Fix Log — P0_TC001_LaunchVisualStudioTestBVT.txt

## P0_TC001_LaunchVisualStudioTestBVT.txt

### Attempt 1 — 2026-06-12 22:50:14
- **Symptom:** NoSuchElementException at step 2 (Click the 'Build' menu).
- **UI dump:** FixesResult/P0_TC001_LaunchVisualStudioTestBVT_attempt1_uidump.xml
- **Root cause:** Control mismatch
- **Fix applied:** helper/Helper.cs: Build locator `//MenuItem[@Name='Build']` → `//MenuItem[contains(@Name,'Build')]`
- **Result:** Pass

RESULT: PASS in 1 attempts
```
