using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SteamAuth;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace Steam_Desktop_Authenticator
{
    public partial class ImportAccountForm : Form
    {
        private Manifest mManifest;

        /// <summary>
        /// Passkey of the local manifest. Lets the login form list the proxies of existing
        /// accounts, and lets an imported account's proxy be encrypted like its maFile.
        /// </summary>
        public string PassKey { get; set; }

        public ImportAccountForm()
        {
            InitializeComponent();
            this.mManifest = Manifest.GetManifest();
        }

        private void ImportAccountForm_Load(object sender, EventArgs e)
        {
            LocalizationManager.ApplyTo(this);
            DarkTheme.Apply(this);
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            // check if data already added is encripted
            #region check if data already added is encripted
            string ContiuneImport = "0";

            string ManifestFile = "maFiles/manifest.json";
            if (File.Exists(ManifestFile))
            {
                string AppManifestContents = File.ReadAllText(ManifestFile);
                AppManifest AppManifestData = JsonConvert.DeserializeObject<AppManifest>(AppManifestContents);
                bool AppManifestData_encrypted = AppManifestData.Encrypted;
                if (AppManifestData_encrypted == true)
                {
                    MessageBox.Show(LocalizationManager.T("ImportAccountForm.msg.ExistingEncrypted", "You can't import an .maFile because the existing account in the app is encrypted.\nDecrypt it and try again."));
                    this.Close();
                }
                else if (AppManifestData_encrypted == false)
                {
                    ContiuneImport = "1";
                }
                else
                {
                    MessageBox.Show(LocalizationManager.T("ImportAccountForm.msg.InvalidEncryptedFlag", "invalid value for variable 'encrypted' inside manifest.json"));
                    this.Close();
                }
            }
            else
            {
                MessageBox.Show(LocalizationManager.T("ImportAccountForm.msg.GenericError", "An Error occurred, Restart the program!"));
            }
            #endregion

            // Continue
            #region Continue
            if (ContiuneImport == "1")
            {
                this.Close();

                // read EncriptionKey from imput box
                string ImportUsingEncriptionKey = txtBox.Text;

                // Open file browser > to select the file
                OpenFileDialog openFileDialog1 = new OpenFileDialog();

                // Set filter options and filter index.
                openFileDialog1.Filter = "maFiles (.maFile)|*.maFile|All Files (*.*)|*.*";
                openFileDialog1.FilterIndex = 1;
                openFileDialog1.Multiselect = false;

                // Call the ShowDialog method to show the dialog box.
                DialogResult userClickedOK = openFileDialog1.ShowDialog();

                // Process input if the user clicked OK.
                if (userClickedOK == DialogResult.OK)
                {
                    // Open the selected file to read.
                    System.IO.Stream fileStream = openFileDialog1.OpenFile();
                    string fileContents = null;

                    using (System.IO.StreamReader reader = new System.IO.StreamReader(fileStream))
                    {
                        fileContents = reader.ReadToEnd();
                    }
                    fileStream.Close();

                    try
                    {
                        if (ImportUsingEncriptionKey == "")
                        {
                            // Import maFile
                            //-------------------------------------------
                            #region Import maFile
                            SteamGuardAccount maFile = JsonConvert.DeserializeObject<SteamGuardAccount>(fileContents);

                            ProxySettings importedProxy = null;

                            if (maFile.Session == null || maFile.Session.SteamID == 0 || maFile.Session.IsAccessTokenExpired())
                            {
                                // Have the user to relogin to steam to get a new session
                                LoginForm loginForm = new LoginForm(LoginForm.LoginType.Import, maFile);
                                loginForm.PassKey = this.PassKey;
                                loginForm.ShowDialog();

                                if (loginForm.Session == null || loginForm.Session.SteamID == 0)
                                {
                                    MessageBox.Show(LocalizationManager.T("ImportAccountForm.msg.LoginFailed", "Login failed. Try to import this account again."), LocalizationManager.T("ImportAccountForm.title.AccountImport", "Account Import"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    return;
                                }

                                // Save new session to the maFile
                                maFile.Session = loginForm.Session;
                                importedProxy = loginForm.SelectedProxy;
                            }

                            // Save account
                            if (!SaveImportedAccount(maFile)) return;

                            ApplyImportedProxy(maFile, importedProxy);
                            MessageBox.Show(LocalizationManager.T("ImportAccountForm.msg.Imported", "Account Imported!"), LocalizationManager.T("ImportAccountForm.title.AccountImport", "Account Import"), MessageBoxButtons.OK);
                            #endregion
                        }
                        else
                        {
                            // Import Encripted maFile
                            //-------------------------------------------
                            #region Import Encripted maFile
                            //Read manifest.json encryption_iv encryption_salt
                            string ImportFileName_Found = "0";
                            string Salt_Found = null;
                            string IV_Found = null;
                            string ReadManifestEx = "0";

                            //No directory means no manifest file anyways.
                            ImportManifest newImportManifest = new ImportManifest();
                            newImportManifest.Encrypted = false;
                            newImportManifest.Entries = new List<ImportManifestEntry>();

                            // extract folder path
                            string fullPath = openFileDialog1.FileName;
                            string fileName = openFileDialog1.SafeFileName;
                            string path = fullPath.Replace(fileName, "");

                            // extract fileName
                            string ImportFileName = fullPath.Replace(path, "");

                            string ImportManifestFile = path + "manifest.json";


                            if (File.Exists(ImportManifestFile))
                            {
                                string ImportManifestContents = File.ReadAllText(ImportManifestFile);


                                try
                                {
                                    ImportManifest account = JsonConvert.DeserializeObject<ImportManifest>(ImportManifestContents);
                                    //bool Import_encrypted = account.Encrypted;

                                    List<ImportManifest> newEntries = new List<ImportManifest>();

                                    foreach (var entry in account.Entries)
                                    {
                                        string FileName = entry.Filename;
                                        string encryption_iv = entry.IV;
                                        string encryption_salt = entry.Salt;

                                        if (ImportFileName == FileName)
                                        {
                                            ImportFileName_Found = "1";
                                            IV_Found = entry.IV;
                                            Salt_Found = entry.Salt;
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                    ReadManifestEx = "1";
                                    MessageBox.Show(LocalizationManager.T("ImportAccountForm.msg.InvalidManifestContent", "Invalid content inside manifest.json!\nImport Failed."));
                                }


                                // DECRIPT & Import
                                //--------------------
                                #region DECRIPT & Import
                                if (ReadManifestEx == "0")
                                {
                                    if (ImportFileName_Found == "1" && Salt_Found != null && IV_Found != null)
                                    {
                                        string decryptedText = FileEncryptor.DecryptData(ImportUsingEncriptionKey, Salt_Found, IV_Found, fileContents);

                                        if (decryptedText == null)
                                        {
                                            MessageBox.Show(LocalizationManager.T("ImportAccountForm.msg.DecryptionFailed", "Decryption Failed.\nImport Failed."));
                                        }
                                        else
                                        {
                                            string fileText = decryptedText;

                                            SteamGuardAccount maFile = JsonConvert.DeserializeObject<SteamGuardAccount>(fileText);
                                            ProxySettings importedProxy = null;

                                            if (maFile.Session == null || maFile.Session.SteamID == 0 || maFile.Session.IsAccessTokenExpired())
                                            {
                                                // Have the user to relogin to steam to get a new session
                                                LoginForm loginForm = new LoginForm(LoginForm.LoginType.Import, maFile);
                                                loginForm.PassKey = this.PassKey;
                                                loginForm.ShowDialog();

                                                if (loginForm.Session == null || loginForm.Session.SteamID == 0)
                                                {
                                                    MessageBox.Show(LocalizationManager.T("ImportAccountForm.msg.LoginFailed", "Login failed. Try to import this account again."), LocalizationManager.T("ImportAccountForm.title.AccountImport", "Account Import"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                    return;
                                                }

                                                // Save new session to the maFile
                                                maFile.Session = loginForm.Session;
                                                importedProxy = loginForm.SelectedProxy;
                                            }

                                            // Save account
                                            if (!SaveImportedAccount(maFile)) return;

                                            ApplyImportedProxy(maFile, importedProxy);
                                            MessageBox.Show(mManifest.Encrypted
                                                ? LocalizationManager.T("ImportAccountForm.msg.ImportedReencrypted", "Account Imported!\nIt has been re-encrypted with your passkey.")
                                                : LocalizationManager.T("ImportAccountForm.msg.ImportedDecrypted", "Account Imported!\nYour Account in now Decrypted!"),
                                                LocalizationManager.T("ImportAccountForm.title.AccountImport", "Account Import"), MessageBoxButtons.OK);
                                        }
                                    }
                                    else
                                    {
                                        if (ImportFileName_Found == "0")
                                        {
                                            MessageBox.Show(LocalizationManager.T("ImportAccountForm.msg.AccountNotInManifest", "Account not found inside manifest.json.\nImport Failed."));
                                        }
                                        else if (Salt_Found == null && IV_Found == null)
                                        {
                                            MessageBox.Show(LocalizationManager.T("ImportAccountForm.msg.NoEncryptedData", "manifest.json does not contain encrypted data.\nYour account may be unencrypted!\nImport Failed."));
                                        }
                                        else
                                        {
                                            if (IV_Found == null)
                                            {
                                                MessageBox.Show(LocalizationManager.T("ImportAccountForm.msg.MissingIV", "manifest.json does not contain: encryption_iv\nImport Failed."));
                                            }
                                            else if (IV_Found == null)
                                            {
                                                MessageBox.Show(LocalizationManager.T("ImportAccountForm.msg.MissingSalt", "manifest.json does not contain: encryption_salt\nImport Failed."));
                                            }
                                        }
                                    }
                                }
                                #endregion //DECRIPT & Import END


                            }
                            else
                            {
                                MessageBox.Show(LocalizationManager.T("ImportAccountForm.msg.ManifestMissing", "manifest.json is missing!\nImport Failed."));
                            }
                            #endregion //Import Encripted maFile END
                        }

                    }
                    catch (Exception)
                    {
                        MessageBox.Show(LocalizationManager.T("ImportAccountForm.msg.NotAMaFile", "This file is not a valid SteamAuth maFile.\nImport Failed."));
                    }
                }
            }
            #endregion // Continue End
        }

        /// <summary>
        /// Saves an imported account using the manifest's own encryption state.
        ///
        /// This used to pass encrypt=false unconditionally, which Manifest.SaveAccount
        /// rejects outright when the manifest is encrypted. The result was returned and
        /// ignored, so importing into an encrypted manifest reported success and wrote
        /// nothing. Mirrors what LoginForm.HandleManifest does: obtain a passkey when one
        /// is needed, then save with it.
        /// </summary>
        /// <returns>False when nothing was written; the caller must not report success.</returns>
        private bool SaveImportedAccount(SteamGuardAccount maFile)
        {
            if (mManifest.Encrypted && string.IsNullOrEmpty(this.PassKey))
            {
                this.PassKey = mManifest.PromptForPassKey();
                if (string.IsNullOrEmpty(this.PassKey))
                {
                    MessageBox.Show(LocalizationManager.T("ImportAccountForm.msg.PasskeyRequired", "Your manifest is encrypted, so its passkey is required to import an account.\nImport Failed."),
                        LocalizationManager.T("ImportAccountForm.title.AccountImport", "Account Import"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }

            if (!mManifest.SaveAccount(maFile, mManifest.Encrypted, this.PassKey))
            {
                MessageBox.Show(LocalizationManager.T("ImportAccountForm.msg.SaveFailed", "Unable to save the imported account.\nImport Failed."),
                    LocalizationManager.T("ImportAccountForm.title.AccountImport", "Account Import"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Stores the proxy chosen during an import login and puts it on the account right
        /// away, so the very next request it makes already goes out through that proxy
        /// instead of waiting for the account list to be reloaded.
        /// </summary>
        private void ApplyImportedProxy(SteamGuardAccount maFile, ProxySettings chosen)
        {
            if (maFile == null || maFile.Session == null) return;

            ulong steamId = maFile.Session.SteamID;

            if (chosen != null)
            {
                ProxyStore.Save(steamId, chosen, mManifest.Encrypted, this.PassKey);
            }
            else
            {
                // No login happened, so nothing was picked: keep whatever this account
                // already had, in case it is being re-imported over an existing sidecar.
                chosen = ProxyStore.Load(steamId, this.PassKey);
            }

            maFile.SetWebProxy(chosen == null ? null : chosen.ToWebProxy());
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Import_maFile_Form_FormClosing(object sender, FormClosingEventArgs e)
        {
        }
    }


    public class AppManifest
    {
        [JsonProperty("encrypted")]
        public bool Encrypted { get; set; }
    }


    public class ImportManifest
    {
        [JsonProperty("encrypted")]
        public bool Encrypted { get; set; }

        [JsonProperty("entries")]
        public List<ImportManifestEntry> Entries { get; set; }
    }

    public class ImportManifestEntry
    {
        [JsonProperty("encryption_iv")]
        public string IV { get; set; }

        [JsonProperty("encryption_salt")]
        public string Salt { get; set; }

        [JsonProperty("filename")]
        public string Filename { get; set; }

        [JsonProperty("steamid")]
        public ulong SteamID { get; set; }
    }
}
