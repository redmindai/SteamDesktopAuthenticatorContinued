using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using SteamAuth;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Steam_Desktop_Authenticator
{
    /// <summary>
    /// A WebView2 window bound to one account: its own browser profile, its own proxy,
    /// and the account's Steam session injected before the first navigation.
    ///
    /// Pages that call window.open get a second window of this same form, built on the same
    /// environment, so a popup keeps the account's cookies and proxy instead of becoming a
    /// bare window outside our control.
    /// </summary>
    public class AccountBrowserForm : Form
    {
        private const string StartUrl = "https://steamcommunity.com/my/tradeoffers/";

        private readonly SteamGuardAccount account;
        private readonly ProxySettings proxy;
        private readonly CoreWebView2Environment environment;
        private readonly string passKey;
        private readonly bool isPopup;
        private readonly WebView2 webView;
        private readonly ulong steamId;

        private readonly TaskCompletionSource<CoreWebView2> ready = new TaskCompletionSource<CoreWebView2>();

        public AccountBrowserForm(SteamGuardAccount account, ProxySettings proxy, CoreWebView2Environment environment, string passKey)
            : this(account, proxy, environment, passKey, false)
        {
        }

        private AccountBrowserForm(SteamGuardAccount account, ProxySettings proxy, CoreWebView2Environment environment, string passKey, bool isPopup)
        {
            this.account = account;
            this.proxy = proxy;
            this.environment = environment;
            this.passKey = passKey;
            this.isPopup = isPopup;
            this.steamId = account.Session == null ? 0 : account.Session.SteamID;

            this.Text = String.Format("Steam Browser - {0}", account.AccountName);
            this.ClientSize = isPopup ? new Size(900, 640) : new Size(1100, 750);
            this.MinimumSize = new Size(480, 360);
            this.StartPosition = FormStartPosition.CenterParent;

            this.webView = new WebView2();
            this.webView.Dock = DockStyle.Fill;
            this.webView.CoreWebView2InitializationCompleted += webView_CoreWebView2InitializationCompleted;
            this.Controls.Add(this.webView);

            DarkTheme.Apply(this);
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                await this.webView.EnsureCoreWebView2Async(this.environment);
            }
            catch (Exception ex)
            {
                // Most often: the WebView2 runtime is missing, or the profile folder is already
                // locked by another window that was opened with different proxy arguments.
                this.ready.TrySetException(ex);
                MessageBox.Show(string.Format(LocalizationManager.T("AccountBrowserForm.msg.StartFailed", "Unable to start the browser: {0}"), ex.Message), LocalizationManager.T("AccountBrowserForm.title.Browser", "Steam Browser"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
        }

        private async void webView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                string message = e.InitializationException == null ? "Unknown error." : e.InitializationException.Message;
                this.ready.TrySetException(e.InitializationException ?? new Exception(message));
                MessageBox.Show(string.Format(LocalizationManager.T("AccountBrowserForm.msg.StartFailed", "Unable to start the browser: {0}"), message), LocalizationManager.T("AccountBrowserForm.title.Browser", "Steam Browser"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return;
            }

            CoreWebView2 core = this.webView.CoreWebView2;

            // Both already default to true; set explicitly so that F12 and the logging below
            // cannot be turned off by a later change without someone noticing.
            core.Settings.AreDevToolsEnabled = true;
            core.Settings.IsWebMessageEnabled = true;

            // Must be subscribed before navigating, otherwise the first proxy challenge is missed.
            if (this.proxy != null && this.proxy.HasCredentials)
                core.BasicAuthenticationRequested += CoreWebView2_BasicAuthenticationRequested;

            core.NewWindowRequested += CoreWebView2_NewWindowRequested;
            core.ProcessFailed += CoreWebView2_ProcessFailed;

            await StartPageLoggingAsync(core);

            // A popup is handed its page by the window that opened it: injecting cookies or
            // navigating here would overwrite whatever it was opened for.
            if (this.isPopup)
            {
                this.ready.TrySetResult(core);
                return;
            }

            try
            {
                await CookieInjector.InjectSessionAsync(core, this.account, this.passKey);
            }
            catch (Exception ex)
            {
                this.ready.TrySetException(ex);
                MessageBox.Show(ex.Message, LocalizationManager.T("AccountBrowserForm.title.Browser", "Steam Browser"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return;
            }

            this.ready.TrySetResult(core);
            core.Navigate(StartUrl);
        }

        /// <summary>
        /// Mirrors what the page reports into a log file: console output, uncaught script
        /// errors, and the browser's own entries such as blocked requests. Steam's pages fail
        /// silently more often than they show an error, and this is what makes "the button
        /// does nothing" readable after the fact instead of only under F12.
        /// </summary>
        private async Task StartPageLoggingAsync(CoreWebView2 core)
        {
            try
            {
                CoreWebView2DevToolsProtocolEventReceiver console = core.GetDevToolsProtocolEventReceiver("Runtime.consoleAPICalled");
                console.DevToolsProtocolEventReceived += (s, e) =>
                    BrowserConsoleLog.Write(this.steamId, "console", e.ParameterObjectAsJson);

                CoreWebView2DevToolsProtocolEventReceiver exceptions = core.GetDevToolsProtocolEventReceiver("Runtime.exceptionThrown");
                exceptions.DevToolsProtocolEventReceived += (s, e) =>
                    BrowserConsoleLog.Write(this.steamId, "exception", e.ParameterObjectAsJson);

                CoreWebView2DevToolsProtocolEventReceiver logEntries = core.GetDevToolsProtocolEventReceiver("Log.entryAdded");
                logEntries.DevToolsProtocolEventReceived += (s, e) =>
                    BrowserConsoleLog.Write(this.steamId, "browser", e.ParameterObjectAsJson);

                await core.CallDevToolsProtocolMethodAsync("Runtime.enable", "{}");
                await core.CallDevToolsProtocolMethodAsync("Log.enable", "{}");

                BrowserConsoleLog.Write(this.steamId, "session",
                    "browser opened for " + this.account.AccountName +
                    ", proxy=" + ProxySettings.Describe(this.proxy) +
                    (this.isPopup ? ", popup window" : string.Empty));
            }
            catch (Exception ex)
            {
                // Diagnostics are a convenience; losing them must not stop the browser.
                BrowserConsoleLog.Write(this.steamId, "session", "logging unavailable: " + ex.Message);
            }
        }

        /// <summary>
        /// Without a handler WebView2 opens popups in a window of its own. That window does
        /// share this profile, but nothing of ours reaches it: no title, no theme, and none
        /// of the page logging above. Creating it here keeps popups inside the same
        /// environment and under the same diagnostics.
        /// </summary>
        private async void CoreWebView2_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            CoreWebView2Deferral deferral = e.GetDeferral();

            try
            {
                BrowserConsoleLog.Write(this.steamId, "popup", "window.open -> " + e.Uri);

                AccountBrowserForm popup = new AccountBrowserForm(this.account, this.proxy, this.environment, this.passKey, true);
                popup.Show(this);

                CoreWebView2 core = await popup.ready.Task;

                ApplyRequestedSize(popup, e.WindowFeatures);

                e.NewWindow = core;
                e.Handled = true;
            }
            catch (Exception ex)
            {
                // Leaving Handled false lets WebView2 fall back to its own popup window,
                // which is still better than the click doing nothing at all.
                BrowserConsoleLog.Write(this.steamId, "popup", "failed to open popup: " + ex.Message);
            }
            finally
            {
                deferral.Complete();
            }
        }

        private static void ApplyRequestedSize(Form popup, CoreWebView2WindowFeatures features)
        {
            if (features == null) return;

            try
            {
                if (features.HasSize && features.Width > 200 && features.Height > 150)
                    popup.ClientSize = new Size((int)features.Width, (int)features.Height);
            }
            catch (Exception)
            {
            }
        }

        private void CoreWebView2_ProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
        {
            BrowserConsoleLog.Write(this.steamId, "process",
                e.ProcessFailedKind + " / " + e.Reason + " " + e.ProcessDescription);
        }

        private void CoreWebView2_BasicAuthenticationRequested(object sender, CoreWebView2BasicAuthenticationRequestedEventArgs e)
        {
            if (this.proxy == null) return;

            e.Response.UserName = this.proxy.Username ?? string.Empty;
            e.Response.Password = this.proxy.Password ?? string.Empty;
        }
    }
}
