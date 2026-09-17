using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using SteamAuth;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Steam_Desktop_Authenticator
{
    /// <summary>
    /// A WebView2 window bound to one account: its own browser profile, its own proxy,
    /// and the account's Steam session injected before the first navigation.
    /// </summary>
    public class AccountBrowserForm : Form
    {
        private const string StartUrl = "https://steamcommunity.com/my/tradeoffers/";

        private readonly SteamGuardAccount account;
        private readonly ProxySettings proxy;
        private readonly CoreWebView2Environment environment;
        private readonly string passKey;
        private readonly WebView2 webView;

        public AccountBrowserForm(SteamGuardAccount account, ProxySettings proxy, CoreWebView2Environment environment, string passKey)
        {
            this.account = account;
            this.proxy = proxy;
            this.environment = environment;
            this.passKey = passKey;

            this.Text = String.Format("Steam Browser - {0}", account.AccountName);
            this.ClientSize = new Size(1100, 750);
            this.MinimumSize = new Size(640, 480);
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
                MessageBox.Show("Unable to start the browser: " + ex.Message, "Steam Browser", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
        }

        private async void webView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                string message = e.InitializationException == null ? "Unknown error." : e.InitializationException.Message;
                MessageBox.Show("Unable to start the browser: " + message, "Steam Browser", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return;
            }

            // Must be subscribed before navigating, otherwise the first proxy challenge is missed.
            if (this.proxy != null && this.proxy.HasCredentials)
                this.webView.CoreWebView2.BasicAuthenticationRequested += CoreWebView2_BasicAuthenticationRequested;

            try
            {
                await CookieInjector.InjectSessionAsync(this.webView.CoreWebView2, this.account, this.passKey);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Steam Browser", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return;
            }

            this.webView.CoreWebView2.Navigate(StartUrl);
        }

        private void CoreWebView2_BasicAuthenticationRequested(object sender, CoreWebView2BasicAuthenticationRequestedEventArgs e)
        {
            if (this.proxy == null) return;

            e.Response.UserName = this.proxy.Username ?? string.Empty;
            e.Response.Password = this.proxy.Password ?? string.Empty;
        }
    }
}
