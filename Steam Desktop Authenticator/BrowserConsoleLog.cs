using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Steam_Desktop_Authenticator
{
    /// <summary>
    /// Writes what the embedded browser's pages report to a file, so a page that quietly
    /// fails can be diagnosed without opening DevTools and reproducing it.
    ///
    /// Off unless the user turns it on, because these pages carry the account's session and
    /// the log is one more file that has seen it. Secrets are masked either way: a setting
    /// can be switched on by accident, a leaked token cannot be taken back.
    ///
    /// Lives next to the WebView profiles under LocalAppData rather than beside the
    /// executable, which may sit in a directory the user cannot write to.
    /// </summary>
    public static class BrowserConsoleLog
    {
        private const long MaxBytes = 2 * 1024 * 1024;
        private static readonly object writeLock = new object();

        /// <summary>Set from Manifest.BrowserLogging at startup and whenever settings are saved.</summary>
        public static bool Enabled { get; set; }

        /// <summary>
        /// Anything whose value would let someone act as the account. Steam puts these in
        /// query strings, in cookie headers and in JSON bodies, all of which can reach the
        /// console or the browser's own log entries.
        /// </summary>
        private static readonly string[] SecretNames =
        {
            "access_token",
            "refresh_token",
            "steamLoginSecure",
            "sessionid"
        };

        private static readonly Regex[] QuotedForms;
        private static readonly Regex[] BareForms;

        static BrowserConsoleLog()
        {
            QuotedForms = new Regex[SecretNames.Length];
            BareForms = new Regex[SecretNames.Length];

            for (int i = 0; i < SecretNames.Length; i++)
            {
                string name = Regex.Escape(SecretNames[i]);

                // "access_token":"value"  /  access_token: "value"
                QuotedForms[i] = new Regex(
                    "(\"?" + name + "\"?\\s*[:=]\\s*\")([^\"]*)(\")",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled);

                // access_token=value  in a URL, a cookie header or a form body
                BareForms[i] = new Regex(
                    "(" + name + "\\s*=\\s*)([^&\\s\";,}\\]]+)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
        }

        public static string GetLogDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SteamDesktopAuthenticator",
                "logs");
        }

        public static string GetLogPath(ulong steamId)
        {
            return Path.Combine(GetLogDirectory(), "browser-" + steamId + ".log");
        }

        /// <summary>
        /// Replaces the value of every known secret with ***, leaving the surrounding text
        /// readable. Applied to every line regardless of the setting.
        /// </summary>
        public static string Mask(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            for (int i = 0; i < SecretNames.Length; i++)
            {
                text = QuotedForms[i].Replace(text, "$1***$3");
                text = BareForms[i].Replace(text, "$1***");
            }

            return text;
        }

        /// <summary>
        /// Appends one line when logging is on. Never throws: logging must not be able to
        /// break browsing.
        /// </summary>
        public static void Write(ulong steamId, string category, string message)
        {
            if (!Enabled || string.IsNullOrEmpty(message)) return;

            try
            {
                lock (writeLock)
                {
                    string path = GetLogPath(steamId);
                    Directory.CreateDirectory(GetLogDirectory());
                    Rotate(path);

                    string line = string.Format("{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}{3}",
                        DateTime.Now, category, Collapse(Mask(message)), Environment.NewLine);
                    File.AppendAllText(path, line, Encoding.UTF8);
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Removes an account's log, including the rotated one. Called when the account
        /// leaves the manifest, so nothing it browsed outlives it.
        /// </summary>
        public static bool Delete(ulong steamId)
        {
            bool ok = true;

            lock (writeLock)
            {
                foreach (string path in new[] { GetLogPath(steamId), GetLogPath(steamId) + ".1" })
                {
                    try
                    {
                        if (File.Exists(path)) File.Delete(path);
                    }
                    catch (Exception)
                    {
                        ok = false;
                    }
                }
            }

            return ok;
        }

        /// <summary>Keeps one previous file so a long session cannot fill the disk.</summary>
        private static void Rotate(string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists || info.Length < MaxBytes) return;

                string previous = path + ".1";
                if (File.Exists(previous)) File.Delete(previous);
                File.Move(path, previous);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>One entry stays one line, so the file can be scanned with a text search.</summary>
        private static string Collapse(string message)
        {
            string text = message.Replace("\r", " ").Replace("\n", " ");
            return text.Length > 4000 ? text.Substring(0, 4000) + " ...[truncated]" : text;
        }
    }
}
