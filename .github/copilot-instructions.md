# Copilot Instructions — Visual Studio Tests

The "brain" of this repository. These global rules govern **all** Copilot code
generation and fixes. Enforce them automatically on every interaction.

## Project Environment

- **Test framework:** xUnit (`[Fact]`, `[Theory]`, `IDisposable` fixtures).
- **Target framework:** `net8.0`.
- **UI automation:** WinAppDriver + Appium (`OpenQA.Selenium.Appium.Windows`).
- **App under test:** Visual Studio (`devenv.exe`); path comes from `config_global.json`
  (`Apps:Default:Path`) or the `VS_APP_PATH` environment variable.
- **Inputs:** all servers/users/endpoints come from `config_global.json` via the
  `ConfigGlobal` data classes — never hard-code credentials, hosts, or paths.

## Testing Rules

1. **Attributes** — Every test is `[Fact(Timeout = 5000)]` or `[Theory(Timeout = 5000)]`.
   Test method names match the BVT/test-case name (e.g. `LaunchVisualStudioTestBVT`).
2. **AAA Pattern** — Structure every test as **Arrange → Act → Assert**.
3. **Wait-then-Act** — Never interact immediately — use `WebDriverWait` first.
   DO: `var btn = wait.Until(d => d.FindElementByAccessibilityId("SendBtn")); btn.Click();`
   DON'T: `session.FindElement(By.AccessibilityId("SendBtn")).Click();` (may not be loaded/enabled → random failures).
4. **Fail-Safe UI Dump** — Use a **shared `DumpUI()` helper**, NOT an inline try-catch per
   action. Dump the element tree once when a test fails so it can be diagnosed offline
   (see `prompts/fixer.md`).
5. **Element Disambiguation** — The same Name can appear on a Button AND an Edit field.
   Use `FindElements` (plural), log all matches, then filter by the expected control type
   (`matches.Where(m => m.TagName == "ControlType.Button").First()`). Never silently pick
   the first match when duplicates are possible.

**Locator policy:** prefer **XPath over ByName**. Use attribute-based XPath
(`//Button[contains(@Name,'Send')]`, `//Edit[contains(@AutomationId,'Email')]`); use
`AccessibilityId` when a stable automation id exists. Reusable UI actions live in
`helper/Helper.cs`.

## .github/ System (this folder)

| Path | Purpose |
|------|---------|
| `copilot-instructions.md` | Global rules for ALL Copilot interactions (this file). |
| `skills/run-tests/` | Reusable skill: run named test(s), emit dated summary. |
| `prompts/fixer.md` | Diagnose & fix failing tests (max 10 retries, never repeat a fix). |
| `prompts/format-fix-log.prompt.md` | Canonical output format for fix logs. |
| `agents/` | Custom agent definitions (e.g. code-explorer). |
| `history/` | Mandatory append-only log of every Copilot interaction. |

## Mandatory History Logging

Every Copilot interaction is logged to `.github/history/` across **three** append-only files:

| File | Content |
|------|---------|
| `prompt_history_input_raw.md` | Verbatim user prompt (exact text). |
| `prompt_history_input_summary.md` | 1–2 sentence summary of the request. |
| `prompt_history_output.md` | Summary of the changes / code produced. |

**Entry format:** `### [YYYY-MM-DD HH:mm] - [Entry Type]`

**Rule:** Always **append**, **NEVER overwrite** — never edit or delete prior entries.
