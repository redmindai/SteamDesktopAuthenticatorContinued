using System;
using System.IO;
using System.Text;

namespace Steam_Desktop_Authenticator
{
    /// <summary>
    /// Writes what the embedded browser's pages report to a file, so a page that quietly
    /// fails can be diagnosed without opening DevTools and reproducing it.
    ///
    /// Lives next to the WebView profiles under LocalAppData rather than beside the
    /// executable, which may sit in a directory the user cannot write to.
    /// </summary>
    public static class BrowserConsoleLog
    {
        private const long MaxBytes = 2 * 1024 * 1024;
        private static readonly object writeLock = new object();

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
        /// Appends one line. Never throws: logging must not be able to break browsing.
        /// </summary>
        public static void Write(ulong steamId, string category, string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            try
            {
                lock (writeLock)
                {
                    string path = GetLogPath(steamId);
                    Directory.CreateDirectory(GetLogDirectory());
                    Rotate(path);

                    string line = string.Format("{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}{3}",
                        DateTime.Now, category, Collapse(message), Environment.NewLine);
                    File.AppendAllText(path, line, Encoding.UTF8);
                }
            }
            catch (Exception)
            {
            }
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
