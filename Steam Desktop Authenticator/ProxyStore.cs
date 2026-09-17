using Newtonsoft.Json;
using System;
using System.IO;

namespace Steam_Desktop_Authenticator
{
    /// <summary>
    /// Reads and writes the per-account proxy sidecar at maFiles/accounts/{steamid}.proxy.json.
    ///
    /// The sidecar is invisible to the existing manifest parsing: Manifest only ever reads
    /// manifest.json plus the filenames listed in its entries, and its one directory scan
    /// (GenerateNewManifest with scanDir) is non-recursive and filters on the .maFile extension.
    ///
    /// Encryption mirrors what Manifest does for .maFile: a per-file PBKDF2 salt and AES IV
    /// fed into FileEncryptor with the same passkey. The salt and IV are not secret, so unlike
    /// the manifest entries they are stored in the sidecar itself, which keeps manifest.json's
    /// schema untouched.
    /// </summary>
    public static class ProxyStore
    {
        private const string FileSuffix = ".proxy.json";

        public static string GetAccountsDir()
        {
            return Path.Combine(Manifest.GetExecutableDir(), "maFiles", "accounts");
        }

        public static string GetSidecarPath(ulong steamId)
        {
            return Path.Combine(GetAccountsDir(), steamId.ToString() + FileSuffix);
        }

        public static bool Exists(ulong steamId)
        {
            return File.Exists(GetSidecarPath(steamId));
        }

        /// <summary>
        /// Returns the stored proxy settings, or null when there is no sidecar, when it cannot
        /// be parsed, or when it is encrypted and the passkey is missing or wrong.
        /// </summary>
        public static ProxySettings Load(ulong steamId, string passKey)
        {
            Sidecar sidecar = ReadSidecar(steamId);
            if (sidecar == null) return null;

            string json = Unwrap(sidecar, passKey);
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                return JsonConvert.DeserializeObject<ProxySettings>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool Save(ulong steamId, ProxySettings settings, bool encrypted, string passKey)
        {
            if (settings == null) return false;
            if (encrypted && string.IsNullOrEmpty(passKey)) return false;

            string json = JsonConvert.SerializeObject(settings);
            return WriteSidecar(steamId, json, encrypted, passKey);
        }

        public static bool Delete(ulong steamId)
        {
            string path = GetSidecarPath(steamId);
            if (!File.Exists(path)) return true;

            try
            {
                File.Delete(path);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Re-wraps an existing sidecar under a new passkey, so it follows the maFiles through
        /// Manifest.ChangeEncryptionKey instead of being left behind readable. A null newKey
        /// writes it back as plaintext. No sidecar for this account is a no-op success.
        /// </summary>
        public static bool Reencrypt(ulong steamId, string oldKey, string newKey)
        {
            Sidecar sidecar = ReadSidecar(steamId);
            if (sidecar == null) return true;

            string json = Unwrap(sidecar, oldKey);
            if (string.IsNullOrEmpty(json)) return false;

            return WriteSidecar(steamId, json, newKey != null, newKey);
        }

        private static Sidecar ReadSidecar(ulong steamId)
        {
            string path = GetSidecarPath(steamId);
            if (!File.Exists(path)) return null;

            try
            {
                return JsonConvert.DeserializeObject<Sidecar>(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Returns the plaintext ProxySettings JSON held by the sidecar, or null.</summary>
        private static string Unwrap(Sidecar sidecar, string passKey)
        {
            if (string.IsNullOrEmpty(sidecar.Payload)) return null;
            if (!sidecar.Encrypted) return sidecar.Payload;

            if (string.IsNullOrEmpty(passKey) || string.IsNullOrEmpty(sidecar.Salt) || string.IsNullOrEmpty(sidecar.IV))
                return null;

            try
            {
                return FileEncryptor.DecryptData(passKey, sidecar.Salt, sidecar.IV, sidecar.Payload);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool WriteSidecar(ulong steamId, string json, bool encrypt, string passKey)
        {
            Sidecar sidecar = new Sidecar { Encrypted = false, Payload = json };

            if (encrypt)
            {
                if (string.IsNullOrEmpty(passKey)) return false;

                string salt = FileEncryptor.GetRandomSalt();
                string iv = FileEncryptor.GetInitializationVector();
                string ciphertext;
                try
                {
                    ciphertext = FileEncryptor.EncryptData(passKey, salt, iv, json);
                }
                catch (Exception)
                {
                    return false;
                }

                if (ciphertext == null) return false;

                sidecar.Encrypted = true;
                sidecar.Salt = salt;
                sidecar.IV = iv;
                sidecar.Payload = ciphertext;
            }

            try
            {
                Directory.CreateDirectory(GetAccountsDir());
                File.WriteAllText(GetSidecarPath(steamId), JsonConvert.SerializeObject(sidecar));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// On-disk envelope. Payload is the serialized ProxySettings: base64 ciphertext when
        /// Encrypted, plain JSON otherwise.
        /// </summary>
        private class Sidecar
        {
            [JsonProperty("encrypted")]
            public bool Encrypted { get; set; }

            [JsonProperty("encryption_salt")]
            public string Salt { get; set; }

            [JsonProperty("encryption_iv")]
            public string IV { get; set; }

            [JsonProperty("payload")]
            public string Payload { get; set; }
        }
    }
}
