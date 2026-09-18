using Microsoft.Web.WebView2.Core;
using SteamAuth;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Steam_Desktop_Authenticator
{
    /// <summary>
    /// Owns one WebView2 environment per account. Each account gets its own user data folder,
    /// so cookies and cache never bleed between accounts, and its own browser arguments, which
    /// is what carries the proxy.
    /// </summary>
    public static class AccountBrowserManager
    {
        private static readonly Dictionary<ulong, CoreWebView2Environment> environments =
            new Dictionary<ulong, CoreWebView2Environment>();

        /// <summary>
        /// Stops Edge's own background services from going out over the account's proxy.
        /// Without these the very first connection is Edge's configuration service, not Steam,
        /// which burns metered proxy traffic and puts a recognisable Edge fingerprint on the IP.
        ///
        /// --disable-background-networking        the umbrella switch: component updater, Safe
        ///                                        Browsing list updates, and Edge's experimentation
        ///                                        and configuration service (config.edge.skype.com)
        /// --disable-component-update             no component/CRX downloads
        /// --disable-sync                         no Microsoft account sync
        /// --disable-domain-reliability           no network-error telemetry uploads
        /// --disable-breakpad                     no crash report uploads
        /// --disable-client-side-phishing-detection   no phishing model downloads
        /// --no-pings                             no hyperlink auditing pings
        /// --no-first-run / --no-default-browser-check   no first-run service calls
        /// --disable-features=...                 OptimizationHints and Translate both fetch on
        ///                                        their own; MediaRouter probes the local network
        /// </summary>
        private const string PrivacyBrowserArguments =
            "--disable-background-networking " +
            "--disable-component-update " +
            "--disable-sync " +
            "--disable-domain-reliability " +
            "--disable-breakpad " +
            "--disable-client-side-phishing-detection " +
            "--no-pings " +
            "--no-first-run " +
            "--no-default-browser-check " +
            "--disable-features=OptimizationHints,Translate,MediaRouter,AutofillServerCommunication";

        public static string GetProfileFolder(ulong steamId)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SteamDesktopAuthenticator",
                "WebViewProfiles",
                steamId.ToString());
        }

        /// <summary>
        /// Drops the cached environment for an account. The proxy is baked into the browser
        /// arguments when the environment is created, so changing it has no effect until the
        /// environment is rebuilt.
        /// </summary>
        public static void Invalidate(ulong steamId)
        {
            environments.Remove(steamId);
        }

        public static async Task<CoreWebView2Environment> GetEnvironmentAsync(ulong steamId, ProxySettings proxy)
        {
            CoreWebView2Environment existing;
            if (environments.TryGetValue(steamId, out existing))
                return existing;

            CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions();
            string arguments = PrivacyBrowserArguments;
            if (proxy != null && proxy.IsValid())
                arguments += " --proxy-server=" + proxy.ToProxyServerArgument();
            options.AdditionalBrowserArguments = arguments;

            string folder = GetProfileFolder(steamId);
            Directory.CreateDirectory(folder);

            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, folder, options);
            environments[steamId] = environment;
            return environment;
        }

        /// <summary>
        /// Opens the account's browser window, creating or reusing its environment.
        /// </summary>
        /// <param name="passKey">Manifest passkey, needed to read an encrypted proxy sidecar. Null when unencrypted.</param>
        public static async Task OpenAsync(SteamGuardAccount account, Form owner, string passKey = null)
        {
            if (account == null || account.Session == null) return;

            ulong steamId = account.Session.SteamID;
            ProxySettings proxy = ProxyStore.Load(steamId, passKey);
            CoreWebView2Environment environment = await GetEnvironmentAsync(steamId, proxy);

            AccountBrowserForm form = new AccountBrowserForm(account, proxy, environment, passKey);
            form.Show(owner);
        }
    }
}
