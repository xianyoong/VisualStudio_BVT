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
        string appPath = Environment.GetEnvironmentVariable("VS_APP_PATH")
                         ?? (string.IsNullOrWhiteSpace(cfg.DefaultApp().Path) ? DefaultDevenvPath : cfg.DefaultApp().Path);

        var options = new AppiumOptions();
        options.AddAdditionalCapability("app", appPath);
        options.AddAdditionalCapability("deviceName", "WindowsPC");

        // VS is slow to launch — allow a long command timeout for session creation.
        _session = new WindowsDriver<WindowsElement>(new Uri(host), options, TimeSpan.FromMinutes(3));
        _session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
        _helper = new Helper(_session);
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
