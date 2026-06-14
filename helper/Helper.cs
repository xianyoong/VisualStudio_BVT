using System.Linq;
using System.Runtime.InteropServices;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Support.UI;

namespace VisualStudioTests.Helpers;

/// <summary>
/// Reusable Visual Studio (devenv.exe) UI-automation helpers.
///
/// Conventions enforced here (same as the BVT framework this was derived from):
///   * Locator policy: prefer XPath over ByName (attribute-based XPath is stable and
///     disambiguates controls that share a Name).
///   * Wait-then-Act: every interaction resolves the element through a WebDriverWait
///     first, because the UI may still be loading (VS is slow to start).
///   * Fail-Safe UI Dump: a single shared DumpUI() helper — NOT inline try-catch per action.
///   * Element Disambiguation: FindElements (plural) -> log all -> filter by control type.
///
/// IMPORTANT: the XPaths below are STUBS based on common Visual Studio 2022 controls.
/// Inspect your VS build with the WinAppDriver UI Recorder / inspect.exe and adjust the
/// @Name / @AutomationId values to match your installed edition and theme.
/// </summary>
public sealed class Helper
{
    private readonly WindowsDriver<WindowsElement> _session;
    private readonly WebDriverWait _wait;

    public Helper(WindowsDriver<WindowsElement> session, int waitSeconds = 60)
    {
        _session = session;
        // VS startup is slow; give the wait a generous default.
        _wait = new WebDriverWait(session, TimeSpan.FromSeconds(waitSeconds));
        _wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(WebDriverException));
    }

    // ---- low-level, XPath-first (preferred over ByName) --------------------

    /// <summary>Wait-then-resolve an element by XPath. Throws on timeout.</summary>
    private WindowsElement WaitXPath(string xpath)
        => _wait.Until(_ => _session.FindElementByXPath(xpath));

    private void ClickXPath(string xpath) => WaitXPath(xpath).Click();

    private void TypeXPath(string xpath, string text)
    {
        var el = WaitXPath(xpath);
        el.Click();
        el.Clear();
        el.SendKeys(text);
    }

    // ---- Fail-Safe UI Dump (shared helper, NOT inline try-catch per action) ----

    /// <summary>
    /// Capture the full UI element tree once, so a failure can be diagnosed offline.
    /// This is the single, shared dump point — actions must NOT wrap themselves in
    /// per-call try-catch. Call this from the test/fixer when something fails.
    /// </summary>
    public string DumpUI(string label = "")
    {
        string tree;
        try { tree = _session.PageSource; }
        catch (Exception ex) { tree = $"<UI dump unavailable: {ex.Message}>"; }

        Console.WriteLine($"==== UI DUMP {label} ====");
        Console.WriteLine(tree);
        return tree;
    }

    // ---- Element Disambiguation (FindElements -> log -> filter by type) --------

    /// <summary>
    /// Resolve a single control when the same Name can appear on more than one element
    /// (e.g. a Button AND a MenuItem both named "Build"). Finds ALL matches (plural),
    /// logs each one, then filters by the expected control type — never silently picks
    /// the first match.
    /// </summary>
    /// <param name="name">The Name shared by the candidate controls.</param>
    /// <param name="controlType">Expected WinAppDriver tag, e.g. "ControlType.Button".</param>
    public WindowsElement FindByNameOfType(string name, string controlType)
    {
        var matches = _wait.Until(_ =>
        {
            var found = _session.FindElementsByName(name);
            return found.Count > 0 ? found : null;
        })!;

        Console.WriteLine($"Disambiguation: {matches.Count} element(s) found for \"{name}\":");
        foreach (var m in matches)
            Console.WriteLine($"  Tag={m.TagName}  Text=\"{m.Text}\"  Enabled={m.Enabled}");

        var hit = matches.FirstOrDefault(m => m.TagName == controlType);
        if (hit == null)
            throw new InvalidOperationException(
                $"Ambiguous/locator: no '{controlType}' named \"{name}\" among {matches.Count} match(es).");

        return (WindowsElement)hit;
    }

    // ---- Visual Studio modules (STUB locators — adjust to your VS build) -------

    /// <summary>
    /// Wait until the Visual Studio shell is ready. The start window ("Open recent" /
    /// "Create a new project") appears first; once a solution is open the main IDE
    /// window (with the menu bar) is shown.
    /// </summary>
    public void WaitForShellReady()
        => _ = WaitXPath("//Window[contains(@Name,'Visual Studio')] | //Window[contains(@Name,'Start Window')]");

    /// <summary>True if the start window (no solution loaded) is currently shown.</summary>
    public bool IsStartWindowVisible()
        => _session.FindElementsByXPath(
               "//*[contains(@Name,'Create a new project') or contains(@Name,'Open recent')]").Count > 0;

    /// <summary>Open the top menu by name (e.g. "File", "Build", "Debug").</summary>
    public void OpenMenu(string menuName)
        => ClickXPath($"//MenuItem[@Name='{menuName}'] | //MenuBar//*[@Name='{menuName}']");

    /// <summary>Open an existing solution/project file via File &gt; Open &gt; Project/Solution.</summary>
    public void OpenSolution(string solutionPath)
    {
        OpenMenu("File");
        ClickXPath("//MenuItem[contains(@Name,'Open')]");
        ClickXPath("//MenuItem[contains(@Name,'Project/Solution')]");
        TypeXPath("//Edit[contains(@Name,'File name')]", solutionPath);
        ClickXPath("//Button[@Name='Open']");
    }

    /// <summary>Build the current solution (Build &gt; Build Solution / Ctrl+Shift+B).</summary>
    public void BuildSolution()
    {
        OpenMenu("Build");
        ClickXPath("//MenuItem[contains(@Name,'Build Solution')]");
    }

    /// <summary>
    /// Read the status-bar text (e.g. "Build succeeded"). Returns empty string if the
    /// status bar cannot be found. Wait-then-Act: resolved through the shared wait.
    /// </summary>
    public string GetStatusBarText()
    {
        var bars = _session.FindElementsByXPath("//StatusBar | //*[contains(@AutomationId,'StatusBar')]");
        return bars.Count > 0 ? bars[0].Text : string.Empty;
    }

    /// <summary>Close Visual Studio via File &gt; Exit.</summary>
    public void ExitApplication()
    {
        OpenMenu("File");
        ClickXPath("//MenuItem[@Name='Exit']");
    }

    /// <summary>True if any element matching the given XPath is currently present.</summary>
    public bool IsElementPresent(string xpath)
        => _session.FindElementsByXPath(xpath).Count > 0;

    /// <summary>
    /// From the start window: click "Create a new project", pick a template by name, click Next.
    /// (STUB locators — adjust to your VS build.)
    /// </summary>
    public void StartNewProject(string templateName)
    {
        ClickXPath("//Button[contains(@Name,'Create a new project')]");
        ClickXPath($"//ListItem[contains(@Name,'{templateName}')]");
        ClickXPath("//Button[@Name='Next']");
    }

    /// <summary>Click a button by its Name (XPath-first). (STUB — adjust to your VS build.)</summary>
    public void ClickButton(string buttonName)
        => ClickXPath($"//Button[@Name='{buttonName}' or contains(@Name,'{buttonName}')]");

    /// <summary>
    /// Open a ComboBox/dropdown by name and select an item by its visible text — e.g. the
    /// "Additional information" page's Framework dropdown. A dropdown is a two-part action
    /// (open it, then pick the item). (STUB locators — adjust to your VS build.)
    /// </summary>
    public void SelectFromDropdown(string comboName, string itemText)
    {
        ClickXPath($"//ComboBox[@Name='{comboName}' or contains(@Name,'{comboName}')]");
        ClickXPath($"//ListItem[contains(@Name,\"{itemText}\")] | //Text[contains(@Name,\"{itemText}\")]");
    }

    /// <summary>
    /// Open a ComboBox/dropdown by name and select an item by its 1-based position.
    /// Use position = -1 (or 0) for the LAST/bottom item. Resolves all options first
    /// (FindElements), logs them, then clicks the requested one — no silent guessing.
    /// (STUB locators — adjust to your VS build.)
    /// </summary>
    public void SelectFromDropdownByPosition(string comboName, int position)
    {
        ClickXPath($"//ComboBox[@Name='{comboName}' or contains(@Name,'{comboName}')]");

        // Wait until the option list is populated, then enumerate every option.
        var options = _wait.Until(_ =>
        {
            var found = _session.FindElementsByXPath("//ListItem | //ComboBox//Text");
            return found.Count > 0 ? found : null;
        })!;

        Console.WriteLine($"Dropdown '{comboName}' has {options.Count} option(s):");
        foreach (var o in options)
            Console.WriteLine($"  - \"{o.Text}\"");

        // -1 or 0 means "last/bottom"; otherwise 1-based from the top.
        int index = (position <= 0) ? options.Count - 1 : position - 1;
        if (index < 0 || index >= options.Count)
            throw new ArgumentOutOfRangeException(
                nameof(position), $"Position {position} is out of range (1..{options.Count}).");

        options[index].Click();
    }

    /// <summary>Open a dropdown by name and select the LAST (bottom) item.</summary>
    public void SelectLastFromDropdown(string comboName)
        => SelectFromDropdownByPosition(comboName, -1);

    /// <summary>
    /// After clicking "Create", verify the New Project wizard completed. VS opens the IDE in a
    /// SEPARATE top-level window, and a WinAppDriver session is bound to the window it launched
    /// against — so the original session cannot see Solution Explorer in the new IDE window.
    /// The reliable, in-session signal that the project was created/opened is that the wizard
    /// window (its "Create"/"Configure"/"Additional information" markers) is no longer present.
    /// Polls until the wizard closes; swallows the session/window exceptions that occur while
    /// the window is tearing down. Wait-then-Act friendly.
    /// </summary>
    public bool WaitForNewProjectWizardToClose(int timeoutSeconds = 30)
    {
        const string wizardMarkers =
            "//Button[@Name='Create'] | //*[contains(@Name,'Configure your new project')] " +
            "| //*[contains(@Name,'Additional information')]";

        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (_session.FindElementsByXPath(wizardMarkers).Count == 0)
                    return true; // wizard closed -> project creation was accepted
            }
            catch (WebDriverException)
            {
                // The wizard window is closing/closed; the session can no longer query it.
                return true;
            }

            Thread.Sleep(500);
        }
        return false;
    }

    // ---- Cross-window re-attach + build/run verification (steps 7-8) -----------

    /// <summary>The underlying WinAppDriver session (exposed so fixtures can dispose child sessions).</summary>
    public WindowsDriver<WindowsElement> Session => _session;

    // ---- Win32 interop: distinguish the real IDE window from the VSSplash screen ----

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    private static string GetWindowClass(IntPtr hWnd)
    {
        var sb = new System.Text.StringBuilder(256);
        GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        var sb = new System.Text.StringBuilder(512);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// Enumerate every top-level window owned by any running devenv.exe process and return the
    /// first real one: VISIBLE, with a non-empty title, and NOT the transient splash screen
    /// (ClassName="VSSplash"). This is more reliable than Process.MainWindowHandle, which can
    /// momentarily point at the splash while both windows exist.
    /// </summary>
    private static IntPtr FindRealVsWindow()
    {
        var devenvPids = new HashSet<uint>(
            System.Diagnostics.Process.GetProcessesByName("devenv").Select(p => (uint)p.Id));
        if (devenvPids.Count == 0)
            return IntPtr.Zero;

        IntPtr match = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
                return true; // keep enumerating
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (!devenvPids.Contains(pid))
                return true;
            if (GetWindowClass(hWnd).Equals("VSSplash", StringComparison.OrdinalIgnoreCase))
                return true; // skip the splash
            if (string.IsNullOrWhiteSpace(GetWindowTitle(hWnd)))
                return true; // skip title-less helper windows
            match = hWnd;
            return false; // found it -> stop enumerating
        }, IntPtr.Zero);

        return match;
    }

    /// <summary>
    /// Poll until devenv.exe exposes a real, visible main window that is NOT the transient splash
    /// screen (ClassName="VSSplash"), then return that window's native handle. Visual Studio shows
    /// the splash first; a WinAppDriver session bound to it dies with "Currently selected window
    /// has been closed" once the splash disappears — so we must wait for the start/IDE window
    /// before attaching.
    /// </summary>
    private static IntPtr WaitForVsMainWindow(int waitSeconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(waitSeconds);
        while (DateTime.UtcNow < deadline)
        {
            IntPtr h = FindRealVsWindow();
            if (h != IntPtr.Zero)
            {
                Console.WriteLine($"VS main window ready: \"{GetWindowTitle(h)}\" " +
                                  $"(class {GetWindowClass(h)}, handle {h}).");
                return h;
            }
            Thread.Sleep(1000);
        }
        return IntPtr.Zero;
    }

    /// <summary>Re-root a WinAppDriver session on an existing top-level window handle.</summary>
    private static Helper AttachByHandle(string host, IntPtr handle)
    {
        string handleHex = handle.ToInt64().ToString("x");
        var options = new AppiumOptions();
        options.AddAdditionalCapability("appTopLevelWindow", handleHex);
        options.AddAdditionalCapability("deviceName", "WindowsPC");
        var driver = new WindowsDriver<WindowsElement>(new Uri(host), options, TimeSpan.FromMinutes(2));
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
        return new Helper(driver);
    }

    /// <summary>
    /// Launch Visual Studio (devenv.exe) and attach a session to its real start/IDE window — the
    /// reliable way to drive VS with WinAppDriver. Binding a session directly to the launched app
    /// attaches to the VSSplash screen, which then closes; instead we start the process ourselves
    /// and re-attach AFTER the splash clears (see WaitForVsMainWindow).
    /// </summary>
    public static Helper LaunchAndAttach(string host, string appPath, int waitSeconds = 120)
    {
        if (!File.Exists(appPath))
            throw new FileNotFoundException(
                $"Visual Studio (devenv.exe) not found at: '{appPath}'. " +
                "Install Visual Studio or set VS_APP_PATH / config_global.json to a valid devenv.exe.");

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = appPath,
            UseShellExecute = true,
        });

        IntPtr handle = WaitForVsMainWindow(waitSeconds);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException(
                "Visual Studio launched but its main window (past the splash screen) never appeared.");

        return AttachByHandle(host, handle);
    }

    /// <summary>
    /// Attach a NEW session to the main Visual Studio IDE window that opens AFTER the New Project
    /// wizard. A WinAppDriver session is bound to the window it was created against; once VS swaps
    /// the start window for the IDE window, a fresh session rooted on the IDE window's handle is
    /// required to drive its menus/panes ("build and run"). The window is located by its native
    /// handle (skipping the VSSplash screen) rather than by traversing the desktop tree.
    /// </summary>
    /// <param name="host">WinAppDriver endpoint, e.g. http://127.0.0.1:4723/.</param>
    /// <param name="windowTitleContains">Unused now; kept for call-site compatibility/logging.</param>
    public static Helper AttachToTopLevelWindow(string host, string windowTitleContains, int waitSeconds = 90)
    {
        IntPtr handle = WaitForVsMainWindow(waitSeconds);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException(
                "Could not find the Visual Studio IDE window (devenv.exe main window) to attach to.");

        return AttachByHandle(host, handle);
    }

    /// <summary>Open the Output tool window (View &gt; Output). STUB locators.</summary>
    public void OpenOutputPane()
    {
        OpenMenu("View");
        ClickXPath("//MenuItem[contains(@Name,'Output')]");
    }

    /// <summary>
    /// Run the program WITHOUT debugging (Debug &gt; Start Without Debugging, i.e. Ctrl+F5) — the
    /// "run" step. For a Console App this launches the built executable. (STUB locators.)
    /// </summary>
    public void RunWithoutDebugging()
    {
        OpenMenu("Debug");
        ClickXPath("//MenuItem[contains(@Name,'Start Without Debugging')]");
    }

    /// <summary>
    /// Wait until the status bar reports a build result ("succeeded"/"failed"/"up to date") or the
    /// timeout elapses. Returns the final status-bar text so the caller can assert on it.
    /// </summary>
    public string WaitForBuildToFinish(int timeoutSeconds = 90)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        string status = string.Empty;
        while (DateTime.UtcNow < deadline)
        {
            status = GetStatusBarText();
            if (status.IndexOf("succeeded", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("up to date", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return status;
            }
            Thread.Sleep(500);
        }
        return status;
    }
}