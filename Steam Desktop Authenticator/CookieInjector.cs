using Microsoft.Web.WebView2.Core;
using SteamAuth;
using System;
using System.Threading.Tasks;

namespace Steam_Desktop_Authenticator
{
    /// <summary>
    /// Seeds a WebView2 instance with the Steam session held in a maFile, so the embedded
    /// browser opens already logged in.
    /// </summary>
    public static class CookieInjector
    {
        /// <summary>
        /// SessionData.GetCookies() hardcodes steamcommunity.com, but the store and help
        /// subdomains need the same session, so the cookies are written per domain here.
        /// </summary>
        private static readonly string[] Domains =
        {
            ".steamcommunity.com",
            ".store.steampowered.com",
            ".help.steampowered.com"
        };

        /// <summary>
        /// Refreshes the access token if needed, then writes steamLoginSecure and sessionid
        /// onto every Steam domain. Throws when the session cannot be made usable.
        /// </summary>
        /// <param name="passKey">Manifest passkey, needed to persist a refreshed token. Null when unencrypted.</param>
        public static async Task InjectSessionAsync(CoreWebView2 webView, SteamGuardAccount account, string passKey = null)
        {
            if (webView == null) throw new ArgumentNullException("webView");
            if (account == null || account.Session == null)
                throw new ArgumentException("Account has no session data.", "account");

            if (account.Session.IsRefreshTokenExpired())
                throw new InvalidOperationException("Your session has expired. Use the login again button under the selected account menu.");

            if (account.Session.IsAccessTokenExpired())
            {
                await account.Session.RefreshAccessToken();
                PersistSession(account, passKey);
            }

            // SessionData generates a SessionID lazily inside GetCookies() and its generator is
            // private, so call it for that side effect rather than duplicating the generator.
            account.Session.GetCookies();

            // Same value as SessionData's private GetSteamLoginSecure(): "{steamid}||{token}",
            // url-encoded so the pipes become %7C%7C.
            string steamLoginSecure = Uri.EscapeDataString(account.Session.SteamID + "||" + account.Session.AccessToken);
            string sessionId = account.Session.SessionID;

            foreach (string domain in Domains)
            {
                CoreWebView2Cookie login = webView.CookieManager.CreateCookie("steamLoginSecure", steamLoginSecure, domain, "/");
                login.IsSecure = true;
                login.IsHttpOnly = true;
                webView.CookieManager.AddOrUpdateCookie(login);

                // sessionid has to stay readable from JS: Steam's pages echo it back in form posts.
                CoreWebView2Cookie session = webView.CookieManager.CreateCookie("sessionid", sessionId, domain, "/");
                session.IsSecure = true;
                webView.CookieManager.AddOrUpdateCookie(session);
            }
        }

        /// <summary>
        /// Writes the refreshed token back to the maFile, the way LoginForm.HandleManifest does.
        /// A failure here only costs a refresh on the next open, so it must not break the browser.
        /// </summary>
        private static void PersistSession(SteamGuardAccount account, string passKey)
        {
            try
            {
                Manifest manifest = Manifest.GetManifest();
                manifest.SaveAccount(account, passKey != null, passKey);
            }
            catch (Exception)
            {
            }
        }
    }
}
