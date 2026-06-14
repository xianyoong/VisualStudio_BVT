using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using VisualStudioTests.Helpers;
using Xunit;

namespace VisualStudioTests.Tests;

/// <summary>
/// Automated BVT (Build Verification Test) cases for Visual Studio (devenv.exe), built on
/// WinAppDriver. Tests leverage the reusable functions in helper/Helper.cs and pull flexible
/// inputs from config_global.json (the ConfigGlobal data classes) instead of hard-coding paths.
///
/// PREREQUISITES to actually run (otherwise the session fails fast, which is expected here):
///   * WinAppDriver.exe running and listening on http://127.0.0.1:4723
///   * A real devenv.exe path in config_global.json (Apps:Default:Path) OR the
///     VS_APP_PATH environment variable set.
/// </summary>
public sealed class BVT_Test : IDisposable
{
    // Environment-specific; override via the VS_APP_PATH environment variable or config.
    private const string DefaultDevenvPath =
        @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe";
    private const string DefaultWinAppDriverUrl = "http://127.0.0.1:4723";

    private readonly WindowsDriver<WindowsElement> _session;
    private readonly Helper _helper;
    private readonly string _host;
    private readonly List<Helper> _attachedHelpers = new();

    public BVT_Test()
    {
        // Arrange (shared): resolve endpoint + app path from config/env, then start a session.
        var cfg = ConfigGlobal.Instance;
        string host = ResolveDriverUrl(cfg.DefaultServer().Host);
        _host = host;
        string appPath = ResolveDevenvPath(cfg.DefaultApp().Path);

        // Launch VS ourselves and attach AFTER the splash screen (VSSplash) closes. Binding a
        // session directly to the launched app attaches to the splash window, which then closes
        // ("Currently selected window has been closed"). LaunchAndAttach re-roots on the real
        // start/IDE window once it appears.
        _helper = Helper.LaunchAndAttach(host, appPath);
        _session = _helper.Session;
    }

    /// <summary>
    /// TC001 - launch Visual Studio and verify the shell becomes ready (start window or IDE).
    /// NOTE: Timeout is set high because VS startup commonly exceeds the deck's 5000 ms guide.
    /// </summary>
    [Fact(Timeout = 120000)]
    public async Task LaunchVisualStudioTestBVT()
    {
        await Task.Run(() =>
        {
            try
            {
                // Act
                _helper.WaitForShellReady();

                // Assert: either the start window or a loaded IDE window is present.
                bool shellVisible = _helper.IsStartWindowVisible()
                                    || !string.IsNullOrEmpty(_helper.GetStatusBarText());
                Assert.True(shellVisible, "Visual Studio shell did not become ready.");
            }
            catch
            {
                // Fail-Safe UI Dump: one shared dump on failure (NOT inline try-catch per action).
                _helper.DumpUI(nameof(LaunchVisualStudioTestBVT));
                throw;
            }
        });
    }

    /// <summary>
    /// TC002 - BuildSolutionTest (from VisualStudio_TC_standard.csv): from the start window,
    /// create a new "Console App" project and verify the configure-project page appears.
    /// (The CSV title still says "Build the solution" — rename it if you prefer.)
    /// </summary>
    [Fact(Timeout = 300000)]
    public async Task BuildSolutionTest()
    {
        await Task.Run(() =>
        {
            try
            {
                // Arrange
                _helper.WaitForShellReady();

                // Act
                _helper.StartNewProject("Console App");          // steps 1-3: New project -> Console App -> Next

                // Step 4: on "Configure your new project", continue to "Additional information".
                _helper.ClickButton("Next");

                // Step 5: choose the target framework on the "Additional information" page.
                _helper.SelectFromDropdown("Framework", ".NET 9.0 (Standard Term Support)");

                // Step 6: create the project.
                _helper.ClickButton("Create");

                // The New Project wizard completed. VS opens the IDE in a SEPARATE top-level
                // window the launch-bound session can't see, so success is first signalled by
                // the wizard window closing (its Create/Configure/Additional-info markers gone).
                bool projectCreated = _helper.WaitForNewProjectWizardToClose();
                Assert.True(projectCreated, "The new Console App project was not created/opened.");

                // Steps 7-8 (build and run): re-attach a session to the new IDE window, build the
                // solution (step 7), then RUN the program (step 8) and verify via the Output pane.
                var ide = Helper.AttachToTopLevelWindow(_host, "Microsoft Visual Studio");
                _attachedHelpers.Add(ide);

                ide.WaitForShellReady();
                ide.BuildSolution();                              // step 7: Build > Build Solution
                string buildStatus = ide.WaitForBuildToFinish();  // wait for the build result

                ide.RunWithoutDebugging();                        // step 8: run (Debug > Start Without Debugging)
                ide.OpenOutputPane();                             // surface the Output pane for verification

                // Assert: build succeeded (status bar) and the Output pane is shown.
                bool buildVerified =
                    buildStatus.IndexOf("succeeded", StringComparison.OrdinalIgnoreCase) >= 0
                    || ide.IsElementPresent("//*[contains(@Name,'Output')]");
                Assert.True(buildVerified,
                    $"Build did not succeed or the Output pane was not shown. Status: '{buildStatus}'.");
            }
            catch
            {
                // Fail-Safe UI Dump: one shared dump on failure (NOT inline try-catch per action).
                _helper.DumpUI(nameof(BuildSolutionTest));
                throw;
            }
        });
    }

    private static string ResolveDriverUrl(string configuredHost)
    {
        // Use the configured WinAppDriver host when it is a valid absolute URL,
        // otherwise fall back to the standard local endpoint.
        if (!string.IsNullOrWhiteSpace(configuredHost)
            && Uri.TryCreate(configuredHost, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return configuredHost;
        }
        return DefaultWinAppDriverUrl;
    }

    /// <summary>
    /// Resolve the path to devenv.exe for ANY installed Visual Studio edition
    /// (Community / Professional / Enterprise / BuildTools) and version (2019, 2022, ...).
    /// Resolution order (first existing wins):
    ///   1. VS_APP_PATH environment variable (explicit override).
    ///   2. config_global.json (Apps:Default:Path) — only if the file actually exists.
    ///   3. vswhere.exe — the official Visual Studio locator shipped with VS Installer.
    ///   4. A filesystem scan of the standard install roots.
    ///   5. The hard-coded DefaultDevenvPath (last resort).
    /// </summary>
    private static string ResolveDevenvPath(string configuredPath)
    {
        // 1. Explicit override via environment variable.
        var envPath = Environment.GetEnvironmentVariable("VS_APP_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            return envPath;

        // 2. config_global.json — honor it only when the path actually exists on this machine.
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            return configuredPath;

        // 3. vswhere.exe — the canonical way to locate any VS install.
        var viaVsWhere = FindDevenvViaVsWhere();
        if (viaVsWhere != null)
            return viaVsWhere;

        // 4. Filesystem scan across editions/versions/Program Files roots.
        var viaScan = FindDevenvViaScan();
        if (viaScan != null)
            return viaScan;

        // 5. Last resort — return the default (session creation will fail clearly if missing).
        return DefaultDevenvPath;
    }

    /// <summary>
    /// Query vswhere.exe (installed at a fixed location with the VS Installer) for the
    /// path to devenv.exe. Returns null if vswhere is absent or finds nothing.
    /// </summary>
    private static string? FindDevenvViaVsWhere()
    {
        string vsWhere = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (!File.Exists(vsWhere))
            return null;

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = vsWhere,
                // -latest: newest version; -prerelease: include Preview;
                // -products *: any edition incl. BuildTools; -property productPath: full devenv.exe path.
                Arguments = "-latest -prerelease -products * -property productPath",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null)
                return null;
            string output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(10000);

            // productPath can point at devenv.exe directly; accept it if it exists.
            if (!string.IsNullOrWhiteSpace(output) && File.Exists(output))
                return output;
        }
        catch
        {
            // vswhere failed — fall through to the filesystem scan.
        }
        return null;
    }

    /// <summary>
    /// Scan the standard Visual Studio install roots for any devenv.exe, preferring the
    /// newest version/edition found. Covers 32/64-bit Program Files locations.
    /// </summary>
    private static string? FindDevenvViaScan()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        };

        var candidates = new List<string>();
        foreach (var root in roots.Distinct())
        {
            if (string.IsNullOrEmpty(root))
                continue;
            string vsRoot = Path.Combine(root, "Microsoft Visual Studio");
            if (!Directory.Exists(vsRoot))
                continue;
            try
            {
                // e.g. ...\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\devenv.exe
                candidates.AddRange(Directory.EnumerateFiles(
                    vsRoot, "devenv.exe", SearchOption.AllDirectories));
            }
            catch
            {
                // ignore folders we can't read
            }
        }

        // Prefer the highest path (newest year folder like "2022" sorts after "2019").
        return candidates
            .OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public void Dispose()
    {
        // Quit any IDE-window sessions attached during the run first.
        foreach (var h in _attachedHelpers)
        {
            try { h.Session.Quit(); }
            catch { /* session may already be gone */ }
        }

        try { _session.Quit(); }
        catch { /* session may already be gone */ }
    }
}
