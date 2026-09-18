using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Net;
using System.Runtime.Serialization;

namespace Steam_Desktop_Authenticator
{
    public enum ProxyType
    {
        Http,
        Socks5
    }

    /// <summary>
    /// How an account reaches Steam. Stored explicitly so that "no proxy" is a choice on
    /// record rather than something inferred from a missing file.
    /// </summary>
    public enum ProxyMode
    {
        Direct,
        Proxy
    }

    /// <summary>
    /// Per-account proxy configuration. Only ever used by the embedded WebView2 browser,
    /// never by SteamWeb or SteamKit2.
    /// </summary>
    public class ProxySettings
    {
        [JsonProperty("host")]
        public string Host { get; set; }

        [JsonProperty("port")]
        public int Port { get; set; }

        /// <summary>Optional. Null or empty means the proxy needs no authentication.</summary>
        [JsonProperty("username")]
        public string Username { get; set; }

        /// <summary>Optional. Only meaningful together with <see cref="Username"/>.</summary>
        [JsonProperty("password")]
        public string Password { get; set; }

        [JsonProperty("type")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ProxyType Type { get; set; } = ProxyType.Http;

        [JsonProperty("mode")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ProxyMode Mode
        {
            get { return mode; }
            set { mode = value; modeWasRead = true; }
        }

        private ProxyMode mode = ProxyMode.Direct;
        private bool modeWasRead;

        /// <summary>
        /// Sidecars written before the mode existed carry no "mode" key. One that names a
        /// reachable proxy was a deliberate choice to use it, so it is read back as Proxy:
        /// defaulting those to Direct would quietly put the account back on the host IP,
        /// which is the exact failure this feature exists to prevent. Anything else, and a
        /// missing sidecar, mean Direct.
        /// </summary>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (!modeWasRead)
                mode = HasEndpoint() ? ProxyMode.Proxy : ProxyMode.Direct;
        }

        /// <summary>True when this account should actually be routed through a proxy.</summary>
        [JsonIgnore]
        public bool UsesProxy
        {
            get { return Mode == ProxyMode.Proxy && HasEndpoint(); }
        }

        [JsonIgnore]
        public bool HasCredentials
        {
            get { return !string.IsNullOrEmpty(Username); }
        }

        /// <summary>Whether host and port could address a proxy, regardless of the mode.</summary>
        public bool HasEndpoint()
        {
            return !string.IsNullOrWhiteSpace(Host) && Port > 0 && Port <= 65535;
        }

        /// <summary>Usable as configured: either a deliberate direct connection, or a complete proxy.</summary>
        public bool IsValid()
        {
            return Mode == ProxyMode.Direct || HasEndpoint();
        }

        /// <summary>Short label for the account list, e.g. "direct" or "1.2.3.4:8080".</summary>
        public static string Describe(ProxySettings settings)
        {
            if (settings == null || !settings.UsesProxy) return "direct";
            return settings.Host + ":" + settings.Port;
        }

        public static ProxySettings DirectConnection()
        {
            return new ProxySettings { Mode = ProxyMode.Direct };
        }

        /// <summary>
        /// Builds the value for Chromium's --proxy-server switch, e.g. "http://1.2.3.4:8080".
        /// Credentials are deliberately left out: Chromium ignores them in the switch and they
        /// would leak into the command line. They are supplied via BasicAuthenticationRequested.
        /// </summary>
        public string ToProxyServerArgument()
        {
            string scheme = Type == ProxyType.Socks5 ? "socks5" : "http";
            return string.Format("{0}://{1}:{2}", scheme, Host, Port);
        }

        /// <summary>
        /// Builds the proxy used by every .NET request for this account: SteamWeb through
        /// WebClient, and SteamKit2 through SocketsHttpHandler. Both answer a proxy's Basic
        /// challenge and both speak SOCKS5 with RFC 1929, so credentials go on directly.
        /// Returns null when the settings are incomplete, which means "connect directly".
        /// </summary>
        public WebProxy ToWebProxy()
        {
            if (!UsesProxy) return null;

            WebProxy proxy = new WebProxy(ToProxyServerArgument());
            if (HasCredentials)
                proxy.Credentials = new NetworkCredential(Username, Password);
            return proxy;
        }
    }
}
