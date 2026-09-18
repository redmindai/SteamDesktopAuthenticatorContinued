using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using SteamAuth;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;

namespace Steam_Desktop_Authenticator
{
    public partial class LoginForm : Form
    {
        public SteamGuardAccount account;
        public LoginType LoginReason;
        public SessionData Session;

        /// <summary>Manifest passkey, needed to read other accounts' proxies and to save this one's.</summary>
        public string PassKey { get; set; }

        /// <summary>
        /// Proxy chosen before logging in. The login is the most IP-sensitive moment and it
        /// happens before a SteamID exists, so the choice cannot come from a sidecar.
        /// </summary>
        private ProxySettings loginProxy;
        private bool populatingProxies;

        /// <summary>
        /// The proxy the user picked, readable once ShowDialog returns. Import needs it:
        /// there the account is written by the caller, not by this form.
        /// </summary>
        public ProxySettings SelectedProxy
        {
            get { return loginProxy; }
        }

        public LoginForm(LoginType loginReason = LoginType.Initial, SteamGuardAccount account = null)
        {
            InitializeComponent();
            this.LoginReason = loginReason;
            this.account = account;

            try
            {
                if (this.LoginReason != LoginType.Initial)
                {
                    txtUsername.Text = account.AccountName;
                    txtUsername.Enabled = false;
                }

                if (this.LoginReason == LoginType.Refresh)
                {
                    labelLoginExplanation.Text = "Your Steam credentials have expired. For trade and market confirmations to work properly, please login again.";
                }
                else if (this.LoginReason == LoginType.Import)
                {
                    labelLoginExplanation.Text = "Please login to your Steam account import it.";
                }
            }
            catch (Exception)
            {
                MessageBox.Show(LocalizationManager.T("LoginForm.msg.AccountNotFound", "Failed to find your account. Try closing and re-opening SDA."), LocalizationManager.T("LoginForm.title.LoginFailed", "Login Failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
        }

        public void SetUsername(string username)
        {
            txtUsername.Text = username;
        }

        public string FilterPhoneNumber(string phoneNumber)
        {
            return phoneNumber.Replace("-", "").Replace("(", "").Replace(")", "");
        }

        public bool PhoneNumberOkay(string phoneNumber)
        {
            if (phoneNumber == null || phoneNumber.Length == 0) return false;
            if (phoneNumber[0] != '+') return false;
            return true;
        }

        private void ResetLoginButton()
        {
            btnSteamLogin.Enabled = true;
            btnSteamLogin.Text = "Login";
        }

        private async void btnSteamLogin_Click(object sender, EventArgs e)
        {
            // Disable button while we login
            btnSteamLogin.Enabled = false;
            btnSteamLogin.Text = "Logging in...";

            string username = txtUsername.Text;
            string password = txtPassword.Text;

            // Start a new SteamClient instance, routed through the chosen proxy
            SteamClient steamClient = new SteamClient(BuildSteamConfiguration());

            // Connect to Steam
            steamClient.Connect();

            // Really basic way to wait until Steam is connected
            while (!steamClient.IsConnected)
                await Task.Delay(500);

            // Create a new auth session
            CredentialsAuthSession authSession;
            try
            {
                authSession = await steamClient.Authentication.BeginAuthSessionViaCredentialsAsync(new AuthSessionDetails
                {
                    Username = username,
                    Password = password,
                    IsPersistentSession = false,
                    PlatformType = EAuthTokenPlatformType.k_EAuthTokenPlatformType_MobileApp,
                    ClientOSType = EOSType.Android9,
                    Authenticator = new UserFormAuthenticator(this.account),
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, LocalizationManager.T("LoginForm.title.SteamLoginError", "Steam Login Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return;
            }

            // Starting polling Steam for authentication response
            AuthPollResult pollResponse;
            try
            {
                pollResponse = await authSession.PollingWaitForResultAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, LocalizationManager.T("LoginForm.title.SteamLoginError", "Steam Login Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return;
            }

            // Build a SessionData object
            SessionData sessionData = new SessionData()
            {
                SteamID = authSession.SteamID.ConvertToUInt64(),
                AccessToken = pollResponse.AccessToken,
                RefreshToken = pollResponse.RefreshToken,
                WebProxy = loginProxy == null ? null : loginProxy.ToWebProxy(),
            };

            //Login succeeded
            this.Session = sessionData;

            // If we're only logging in for an account import, stop here
            if (LoginReason == LoginType.Import)
            {
                this.Close();
                return;
            }

            // If we're only logging in for a session refresh then save it and exit
            if (LoginReason == LoginType.Refresh)
            {
                Manifest man = Manifest.GetManifest();
                account.FullyEnrolled = true;
                account.Session = sessionData;
                HandleManifest(man, true);
                this.Close();
                return;
            }

            // Show a dialog to make sure they really want to add their authenticator
            var result = MessageBox.Show(LocalizationManager.T("LoginForm.msg.LoginSucceeded", "Steam account login succeeded. Press OK to continue adding SDA as your authenticator."), LocalizationManager.T("LoginForm.title.SteamLogin", "Steam Login"), MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (result == DialogResult.Cancel)
            {
                MessageBox.Show(LocalizationManager.T("LoginForm.msg.AddAborted", "Adding authenticator aborted."), LocalizationManager.T("LoginForm.title.SteamLogin", "Steam Login"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                ResetLoginButton();
                return;
            }

            // Begin linking mobile authenticator
            AuthenticatorLinker linker = new AuthenticatorLinker(sessionData);
            linker.WebProxy = sessionData.WebProxy;

            AuthenticatorLinker.LinkResult linkResponse = AuthenticatorLinker.LinkResult.GeneralFailure;
            while (linkResponse != AuthenticatorLinker.LinkResult.AwaitingFinalization)
            {
                try
                {
                    linkResponse = await linker.AddAuthenticator();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(LocalizationManager.T("LoginForm.msg.AddAuthenticatorError", "Error adding your authenticator: {0}"), ex.Message), LocalizationManager.T("LoginForm.title.SteamLogin", "Steam Login"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ResetLoginButton();
                    return;
                }

                switch (linkResponse)
                {
                    case AuthenticatorLinker.LinkResult.MustProvidePhoneNumber:

                        // Show the phone input form
                        PhoneInputForm phoneInputForm = new PhoneInputForm(account);
                        phoneInputForm.ShowDialog();
                        if (phoneInputForm.Canceled)
                        {
                            this.Close();
                            return;
                        }

                        linker.PhoneNumber = phoneInputForm.PhoneNumber;
                        linker.PhoneCountryCode = phoneInputForm.CountryCode;
                        break;

                    case AuthenticatorLinker.LinkResult.AuthenticatorPresent:
                        MessageBox.Show(LocalizationManager.T("LoginForm.msg.AuthenticatorPresent", "This account already has an authenticator linked. You must remove that authenticator to add SDA as your authenticator."), LocalizationManager.T("LoginForm.title.SteamLogin", "Steam Login"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        this.Close();
                        return;

                    case AuthenticatorLinker.LinkResult.FailureAddingPhone:
                        MessageBox.Show(LocalizationManager.T("LoginForm.msg.PhoneAddFailed", "Failed to add your phone number. Please try again or use a different phone number."), LocalizationManager.T("LoginForm.title.SteamLogin", "Steam Login"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        linker.PhoneNumber = null;
                        break;

                    case AuthenticatorLinker.LinkResult.MustRemovePhoneNumber:
                        linker.PhoneNumber = null;
                        break;

                    case AuthenticatorLinker.LinkResult.MustConfirmEmail:
                        MessageBox.Show(LocalizationManager.T("LoginForm.msg.ConfirmEmail", "Please check your email, and click the link Steam sent you before continuing."), LocalizationManager.T("LoginForm.title.SteamLogin", "Steam Login"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;

                    case AuthenticatorLinker.LinkResult.GeneralFailure:
                        MessageBox.Show(LocalizationManager.T("LoginForm.msg.AddAuthenticatorFailed", "Error adding your authenticator."), LocalizationManager.T("LoginForm.title.SteamLoginError", "Steam Login Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        this.Close();
                        return;
                }
            } // End while loop checking for AwaitingFinalization

            Manifest manifest = Manifest.GetManifest();
            string passKey = null;
            if (manifest.Entries.Count == 0)
            {
                passKey = manifest.PromptSetupPassKey("Please enter an encryption passkey. Leave blank or hit cancel to not encrypt (VERY INSECURE).");
            }
            else if (manifest.Entries.Count > 0 && manifest.Encrypted)
            {
                bool passKeyValid = false;
                while (!passKeyValid)
                {
                    InputForm passKeyForm = new InputForm("Please enter your current encryption passkey.");
                    passKeyForm.ShowDialog();
                    if (!passKeyForm.Canceled)
                    {
                        passKey = passKeyForm.txtBox.Text;
                        passKeyValid = manifest.VerifyPasskey(passKey);
                        if (!passKeyValid)
                        {
                            MessageBox.Show(LocalizationManager.T("LoginForm.msg.InvalidPasskey", "That passkey is invalid. Please enter the same passkey you used for your other accounts."));
                        }
                    }
                    else
                    {
                        this.Close();
                        return;
                    }
                }
            }

            //Save the file immediately; losing this would be bad.
            if (!manifest.SaveAccount(linker.LinkedAccount, passKey != null, passKey))
            {
                manifest.RemoveAccount(linker.LinkedAccount);
                MessageBox.Show(LocalizationManager.T("LoginForm.msg.SaveMaFileFailed", "Unable to save mobile authenticator file. The mobile authenticator has not been linked."));
                this.Close();
                return;
            }

            // The SteamID exists now, so the proxy can finally be keyed to it. Saved here
            // rather than at the end so an aborted link removes it along with the account.
            SaveLoginProxy(linker.LinkedAccount.Session.SteamID, passKey != null, passKey);

            MessageBox.Show(string.Format(LocalizationManager.T("LoginForm.msg.WriteDownRevocationCode", "The Mobile Authenticator has not yet been linked. Before finalizing the authenticator, please write down your revocation code: {0}"), linker.LinkedAccount.RevocationCode));

            AuthenticatorLinker.FinalizeResult finalizeResponse = AuthenticatorLinker.FinalizeResult.GeneralFailure;
            while (finalizeResponse != AuthenticatorLinker.FinalizeResult.Success)
            {
                InputForm smsCodeForm = new InputForm("Please input the SMS code sent to your phone.");
                smsCodeForm.ShowDialog();
                if (smsCodeForm.Canceled)
                {
                    manifest.RemoveAccount(linker.LinkedAccount);
                    this.Close();
                    return;
                }

                InputForm confirmRevocationCode = new InputForm("Please enter your revocation code to ensure you've saved it.");
                confirmRevocationCode.ShowDialog();
                if (confirmRevocationCode.txtBox.Text.ToUpper() != linker.LinkedAccount.RevocationCode)
                {
                    MessageBox.Show(LocalizationManager.T("LoginForm.msg.RevocationCodeIncorrect", "Revocation code incorrect; the authenticator has not been linked."));
                    manifest.RemoveAccount(linker.LinkedAccount);
                    this.Close();
                    return;
                }

                string smsCode = smsCodeForm.txtBox.Text;
                finalizeResponse = await linker.FinalizeAddAuthenticator(smsCode);

                switch (finalizeResponse)
                {
                    case AuthenticatorLinker.FinalizeResult.BadSMSCode:
                        continue;

                    case AuthenticatorLinker.FinalizeResult.UnableToGenerateCorrectCodes:
                        MessageBox.Show(string.Format(LocalizationManager.T("LoginForm.msg.CannotGenerateCodes", "Unable to generate the proper codes to finalize this authenticator. The authenticator should not have been linked. In the off-chance it was, please write down your revocation code, as this is the last chance to see it: {0}"), linker.LinkedAccount.RevocationCode));
                        manifest.RemoveAccount(linker.LinkedAccount);
                        this.Close();
                        return;

                    case AuthenticatorLinker.FinalizeResult.GeneralFailure:
                        MessageBox.Show(string.Format(LocalizationManager.T("LoginForm.msg.CannotFinalize", "Unable to finalize this authenticator. The authenticator should not have been linked. In the off-chance it was, please write down your revocation code, as this is the last chance to see it: {0}"), linker.LinkedAccount.RevocationCode));
                        manifest.RemoveAccount(linker.LinkedAccount);
                        this.Close();
                        return;
                }
            }

            //Linked, finally. Re-save with FullyEnrolled property.
            manifest.SaveAccount(linker.LinkedAccount, passKey != null, passKey);
            MessageBox.Show(string.Format(LocalizationManager.T("LoginForm.msg.LinkedSuccessfully", "Mobile authenticator successfully linked. Please write down your revocation code: {0}"), linker.LinkedAccount.RevocationCode));
            this.Close();
        }

        private void HandleManifest(Manifest man, bool IsRefreshing = false)
        {
            string passKey = null;
            if (man.Entries.Count == 0)
            {
                passKey = man.PromptSetupPassKey("Please enter an encryption passkey. Leave blank or hit cancel to not encrypt (VERY INSECURE).");
            }
            else if (man.Entries.Count > 0 && man.Encrypted)
            {
                bool passKeyValid = false;
                while (!passKeyValid)
                {
                    InputForm passKeyForm = new InputForm("Please enter your current encryption passkey.");
                    passKeyForm.ShowDialog();
                    if (!passKeyForm.Canceled)
                    {
                        passKey = passKeyForm.txtBox.Text;
                        passKeyValid = man.VerifyPasskey(passKey);
                        if (!passKeyValid)
                        {
                            MessageBox.Show(LocalizationManager.T("LoginForm.msg.InvalidPasskey", "That passkey is invalid. Please enter the same passkey you used for your other accounts."), LocalizationManager.T("LoginForm.title.SteamLogin", "Steam Login"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        this.Close();
                        return;
                    }
                }
            }

            man.SaveAccount(account, passKey != null, passKey);
            if (account.Session != null)
            {
                SaveLoginProxy(account.Session.SteamID, passKey != null, passKey);
                account.SetWebProxy(loginProxy == null ? null : loginProxy.ToWebProxy());
            }
            if (IsRefreshing)
            {
                MessageBox.Show(LocalizationManager.T("LoginForm.msg.SessionRefreshed", "Your session was refreshed."), LocalizationManager.T("LoginForm.title.SteamLogin", "Steam Login"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(string.Format(LocalizationManager.T("LoginForm.msg.LinkedSuccessfully", "Mobile authenticator successfully linked. Please write down your revocation code: {0}"), account.RevocationCode), LocalizationManager.T("LoginForm.title.SteamLogin", "Steam Login"), MessageBoxButtons.OK);
            }
            this.Close();
        }

        private void LoginForm_Load(object sender, EventArgs e)
        {
            LocalizationManager.ApplyTo(this);
            DarkTheme.Apply(this);
            if (account != null && account.AccountName != null)
            {
                txtUsername.Text = account.AccountName;
            }
            PopulateProxies();
        }

        /// <summary>
        /// Offers a direct connection, every proxy already configured for another account,
        /// and a custom one. Re-using an existing entry is the common case when several
        /// accounts are meant to share an IP.
        /// </summary>
        private void PopulateProxies()
        {
            populatingProxies = true;
            try
            {
                cmbProxy.Items.Clear();
                cmbProxy.Items.Add(ProxyChoice.Direct());

                ProxySettings preselect;
                foreach (ProxySettings known in LoadKnownProxies(out preselect))
                    cmbProxy.Items.Add(new ProxyChoice(known));

                cmbProxy.Items.Add(ProxyChoice.Custom());

                cmbProxy.SelectedIndex = 0;
                loginProxy = null;

                if (preselect != null)
                    SelectProxy(preselect);
            }
            finally
            {
                populatingProxies = false;
            }
        }

        /// <summary>
        /// Reads the proxies of accounts already in the manifest. An encrypted manifest with
        /// no passkey simply yields nothing, which is not an error here.
        /// </summary>
        private List<ProxySettings> LoadKnownProxies(out ProxySettings thisAccountProxy)
        {
            thisAccountProxy = null;
            var found = new List<ProxySettings>();
            var seen = new HashSet<string>();

            try
            {
                Manifest manifest = Manifest.GetManifest();
                foreach (SteamGuardAccount existing in manifest.GetAllAccounts(PassKey))
                {
                    if (existing == null || existing.Session == null) continue;

                    ProxySettings settings = ProxyStore.Load(existing.Session.SteamID, PassKey);
                    if (settings == null || !settings.UsesProxy) continue;

                    // Refreshing an existing account: start from whatever it already uses.
                    if (account != null && account.Session != null && existing.Session.SteamID == account.Session.SteamID)
                        thisAccountProxy = settings;

                    if (seen.Add(settings.ToProxyServerArgument()))
                        found.Add(settings);
                }
            }
            catch (Exception)
            {
                // A broken manifest must not block logging in.
            }

            return found;
        }

        private void SelectProxy(ProxySettings settings)
        {
            if (settings == null)
            {
                cmbProxy.SelectedIndex = 0;
                loginProxy = null;
                return;
            }

            for (int i = 0; i < cmbProxy.Items.Count; i++)
            {
                ProxyChoice choice = cmbProxy.Items[i] as ProxyChoice;
                if (choice != null && choice.Settings != null &&
                    choice.Settings.ToProxyServerArgument() == settings.ToProxyServerArgument())
                {
                    cmbProxy.SelectedIndex = i;
                    loginProxy = settings;
                    return;
                }
            }

            // Newly typed in: insert it just before the "Custom proxy..." entry.
            // Restore rather than clear the guard: SelectProxy is also called from inside
            // PopulateProxies, which is still populating and relies on it staying set.
            bool wasPopulating = populatingProxies;
            populatingProxies = true;
            try
            {
                cmbProxy.Items.Insert(cmbProxy.Items.Count - 1, new ProxyChoice(settings));
                cmbProxy.SelectedIndex = cmbProxy.Items.Count - 2;
            }
            finally
            {
                populatingProxies = wasPopulating;
            }
            loginProxy = settings;
        }

        private void cmbProxy_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (populatingProxies) return;

            ProxyChoice choice = cmbProxy.SelectedItem as ProxyChoice;
            if (choice == null) return;

            if (choice.IsCustom)
            {
                EditProxy(null);
                return;
            }

            loginProxy = choice.Settings;
        }

        private void btnProxyEdit_Click(object sender, EventArgs e)
        {
            EditProxy(loginProxy);
        }

        private void EditProxy(ProxySettings initial)
        {
            using (ProxySettingsForm editor = new ProxySettingsForm(initial))
            {
                if (editor.ShowDialog(this) == DialogResult.OK)
                    SelectProxy(editor.Result);
                else
                    SelectProxy(loginProxy);
            }
        }

        /// <summary>
        /// Pins the CM connection to WebSocket: the TCP transport is a raw socket that no
        /// proxy setting can reach, so the default would send some logins out directly.
        /// Every purpose gets the same proxy, so WebAPI and CDN stay on the account's IP too.
        /// </summary>
        private SteamConfiguration BuildSteamConfiguration()
        {
            IWebProxy proxy = loginProxy == null ? null : loginProxy.ToWebProxy();

            return SteamConfiguration.Create(builder =>
                builder
                    .WithProtocolTypes(ProtocolTypes.WebSocket)
                    .WithHttpClientFactory(purpose =>
                    {
                        var handler = new SocketsHttpHandler();
                        if (proxy != null)
                        {
                            handler.Proxy = proxy;
                            handler.UseProxy = true;
                        }
                        return new HttpClient(handler);
                    }));
        }

        /// <summary>Stores the proxy chosen at login time, now that the SteamID is known.</summary>
        private void SaveLoginProxy(ulong steamId, bool encrypted, string passKey)
        {
            ProxySettings chosen = loginProxy == null ? ProxySettings.DirectConnection() : loginProxy;
            ProxyStore.Save(steamId, chosen, encrypted, passKey);
        }

        private class ProxyChoice
        {
            public ProxySettings Settings { get; private set; }
            public bool IsCustom { get; private set; }
            private readonly string label;

            public ProxyChoice(ProxySettings settings)
            {
                Settings = settings;
                label = settings.ToProxyServerArgument() + (settings.HasCredentials ? "  (auth)" : "");
            }

            private ProxyChoice(string text, bool isCustom)
            {
                label = text;
                IsCustom = isCustom;
            }

            public static ProxyChoice Direct() { return new ProxyChoice("Direct connection", false); }
            public static ProxyChoice Custom() { return new ProxyChoice("Custom proxy...", true); }

            public override string ToString() { return label; }
        }

        public enum LoginType
        {
            Initial,
            Refresh,
            Import
        }
    }
}
