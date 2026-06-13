# FixesResult

Append-only fix logs produced by the `.github/prompts/fixer.md` workflow.

Each stabilized test case gets:
- `<testcase>_fix_log.md` — per-attempt diagnosis/fix/result log.
- `<testcase>_attempt<N>_uidump.xml` — UI element-tree dump captured at failure.
