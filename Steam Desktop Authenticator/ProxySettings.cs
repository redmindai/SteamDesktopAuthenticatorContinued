using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Net;

namespace Steam_Desktop_Authenticator
{
    public enum ProxyType
    {
        Http,
        Socks5
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

        [JsonIgnore]
        public bool HasCredentials
        {
            get { return !string.IsNullOrEmpty(Username); }
        }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Host) && Port > 0 && Port <= 65535;
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
            if (!IsValid()) return null;

            WebProxy proxy = new WebProxy(ToProxyServerArgument());
            if (HasCredentials)
                proxy.Credentials = new NetworkCredential(Username, Password);
            return proxy;
        }
    }
}
