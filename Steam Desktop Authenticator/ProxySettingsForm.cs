using SteamAuth;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Steam_Desktop_Authenticator
{
    /// <summary>
    /// Edits proxy settings, in one of two modes.
    ///
    /// Bound to an account: reads and writes that account's sidecar, and pushes the change
    /// onto the live SteamGuardAccount so it takes effect without a restart.
    ///
    /// Detached: edits a ProxySettings and hands it back through <see cref="Result"/> without
    /// touching disk. Used while adding a new account, where no SteamID exists yet.
    /// </summary>
    public class ProxySettingsForm : Form
    {
        private readonly bool persist;
        private readonly SteamGuardAccount account;
        private readonly ulong steamId;
        private readonly bool encrypted;
        private readonly string passKey;

        private readonly TextBox txtHost;
        private readonly NumericUpDown numPort;
        private readonly TextBox txtUsername;
        private readonly TextBox txtPassword;
        private readonly ComboBox cmbType;
        private readonly RadioButton radDirect;
        private readonly RadioButton radProxy;
        private readonly Button btnSave;
        private readonly Button btnCancel;
        private readonly Button btnDelete;
        private readonly ToolTip toolTip = new ToolTip();

        /// <summary>Detached mode only: what the user entered, or null if they cleared it.</summary>
        public ProxySettings Result { get; private set; }

        /// <summary>Account-bound mode: persists to the sidecar and to the live account.</summary>
        public ProxySettingsForm(SteamGuardAccount account, bool encrypted, string passKey)
            : this(String.Format("Proxy - {0}", account.AccountName), true)
        {
            this.account = account;
            this.steamId = account.Session.SteamID;
            this.encrypted = encrypted;
            this.passKey = passKey;

            bool sidecarExists = ProxyStore.Exists(this.steamId);
            this.btnDelete.Enabled = sidecarExists;

            ProxySettings existing = ProxyStore.Load(this.steamId, passKey);
            if (existing == null && sidecarExists)
            {
                // Present but unreadable: wrong or missing passkey, or a corrupt file.
                // Saving would silently overwrite it, so make that the user's explicit choice.
                MessageBox.Show("A proxy is saved for this account but could not be read. Saving will replace it.",
                    "Proxy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            Populate(existing);
        }

        /// <summary>Detached mode: edits a value, saves nothing.</summary>
        public ProxySettingsForm(ProxySettings initial)
            : this("Proxy", false)
        {
            this.btnDelete.Enabled = initial != null;
            Populate(initial);
        }

        private ProxySettingsForm(string title, bool persist)
        {
            this.persist = persist;

            this.Text = title;
            this.ClientSize = new Size(340, 276);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int labelLeft = 12;
            int fieldLeft = 110;
            int fieldWidth = 216;
            int y = 12;
            int rowHeight = 30;

            // The connection mode comes first: everything below it only matters for a proxy.
            this.radDirect = new RadioButton();
            this.radDirect.Text = "Direct connection (host IP)";
            this.radDirect.Location = new Point(labelLeft, y);
            this.radDirect.AutoSize = true;
            this.radDirect.Checked = true;
            this.radDirect.CheckedChanged += mode_CheckedChanged;
            this.Controls.Add(this.radDirect);
            y += 22;

            this.radProxy = new RadioButton();
            this.radProxy.Text = "Use proxy";
            this.radProxy.Location = new Point(labelLeft, y);
            this.radProxy.AutoSize = true;
            this.radProxy.CheckedChanged += mode_CheckedChanged;
            this.Controls.Add(this.radProxy);
            y += 28;

            this.Controls.Add(MakeLabel("Type", labelLeft, y + 3));
            this.cmbType = new ComboBox();
            this.cmbType.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbType.Location = new Point(fieldLeft, y);
            this.cmbType.Size = new Size(fieldWidth, 23);
            this.cmbType.Items.AddRange(new object[] { ProxyType.Http, ProxyType.Socks5 });
            this.cmbType.SelectedIndex = 0;
            this.cmbType.SelectedIndexChanged += cmbType_SelectedIndexChanged;
            this.Controls.Add(this.cmbType);
            y += rowHeight;

            this.Controls.Add(MakeLabel("Host", labelLeft, y + 3));
            this.txtHost = new TextBox();
            this.txtHost.Location = new Point(fieldLeft, y);
            this.txtHost.Size = new Size(fieldWidth, 23);
            this.Controls.Add(this.txtHost);
            y += rowHeight;

            this.Controls.Add(MakeLabel("Port", labelLeft, y + 3));
            this.numPort = new NumericUpDown();
            this.numPort.Location = new Point(fieldLeft, y);
            this.numPort.Size = new Size(80, 23);
            this.numPort.Minimum = 1;
            this.numPort.Maximum = 65535;
            this.numPort.Value = 8080;
            this.Controls.Add(this.numPort);
            y += rowHeight;

            this.Controls.Add(MakeLabel("Username", labelLeft, y + 3));
            this.txtUsername = new TextBox();
            this.txtUsername.Location = new Point(fieldLeft, y);
            this.txtUsername.Size = new Size(fieldWidth, 23);
            this.Controls.Add(this.txtUsername);
            y += rowHeight;

            this.Controls.Add(MakeLabel("Password", labelLeft, y + 3));
            this.txtPassword = new TextBox();
            this.txtPassword.Location = new Point(fieldLeft, y);
            this.txtPassword.Size = new Size(fieldWidth, 23);
            this.txtPassword.UseSystemPasswordChar = true;
            this.Controls.Add(this.txtPassword);
            y += rowHeight + 8;

            this.btnDelete = new Button();
            this.btnDelete.Text = persist ? "Delete" : "Clear";
            this.btnDelete.Location = new Point(labelLeft, y);
            this.btnDelete.Size = new Size(85, 28);
            this.btnDelete.Click += btnDelete_Click;
            this.Controls.Add(this.btnDelete);

            this.btnCancel = new Button();
            this.btnCancel.Text = "Cancel";
            this.btnCancel.Location = new Point(155, y);
            this.btnCancel.Size = new Size(85, 28);
            this.btnCancel.DialogResult = DialogResult.Cancel;
            this.Controls.Add(this.btnCancel);

            this.btnSave = new Button();
            this.btnSave.Text = persist ? "Save" : "OK";
            this.btnSave.Location = new Point(246, y);
            this.btnSave.Size = new Size(80, 28);
            this.btnSave.Click += btnSave_Click;
            this.Controls.Add(this.btnSave);

            this.AcceptButton = this.btnSave;
            this.CancelButton = this.btnCancel;

            DarkTheme.Apply(this);
        }

        private static Label MakeLabel(string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.AutoSize = true;
            label.BackColor = Color.Transparent;
            return label;
        }

        private void Populate(ProxySettings existing)
        {
            if (existing != null)
            {
                this.cmbType.SelectedItem = existing.Type;
                this.txtHost.Text = existing.Host;
                if (existing.Port >= this.numPort.Minimum && existing.Port <= this.numPort.Maximum)
                    this.numPort.Value = existing.Port;
                this.txtUsername.Text = existing.Username;
                this.txtPassword.Text = existing.Password;

                this.radProxy.Checked = existing.Mode == ProxyMode.Proxy;
                this.radDirect.Checked = existing.Mode != ProxyMode.Proxy;
            }

            UpdateModeFields();
            UpdateCredentialFields();
        }

        private void mode_CheckedChanged(object sender, EventArgs e)
        {
            UpdateModeFields();
        }

        /// <summary>Direct means the proxy fields describe nothing, so they are greyed out.</summary>
        private void UpdateModeFields()
        {
            bool useProxy = this.radProxy.Checked;
            this.cmbType.Enabled = useProxy;
            this.txtHost.Enabled = useProxy;
            this.numPort.Enabled = useProxy;
            this.txtUsername.Enabled = useProxy;
            this.txtPassword.Enabled = useProxy;
        }

        private void cmbType_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateCredentialFields();
        }

        /// <summary>
        /// .NET speaks SOCKS5 with RFC 1929, but Chromium offers only "no authentication",
        /// so an authenticated SOCKS5 proxy would work everywhere except the built-in browser.
        /// </summary>
        private void UpdateCredentialFields()
        {
            bool socks = (ProxyType)this.cmbType.SelectedItem == ProxyType.Socks5;
            this.txtUsername.Enabled = this.txtPassword.Enabled = true;

            string warning = socks ? "Chromium ignores SOCKS5 credentials, so the built-in browser cannot use them." : string.Empty;
            this.toolTip.SetToolTip(this.txtUsername, warning);
            this.toolTip.SetToolTip(this.txtPassword, warning);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            ProxySettings settings;

            if (this.radDirect.Checked)
            {
                settings = ProxySettings.DirectConnection();
                Commit(settings);
                return;
            }

            string host = this.txtHost.Text.Trim();
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("Please enter a proxy host.", "Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ProxyType type = (ProxyType)this.cmbType.SelectedItem;
            settings = new ProxySettings
            {
                Mode = ProxyMode.Proxy,
                Type = type,
                Host = host,
                Port = (int)this.numPort.Value,
                Username = NullIfEmpty(this.txtUsername.Text),
                Password = NullIfEmpty(this.txtPassword.Text)
            };

            if (type == ProxyType.Socks5 && settings.HasCredentials)
            {
                DialogResult go = MessageBox.Show(
                    "Chromium does not support SOCKS5 authentication, so the built-in browser will not be able to use this proxy. " +
                    "Steam code generation and confirmations will still work.\n\nSave anyway?",
                    "Proxy", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (go != DialogResult.OK) return;
            }

            Commit(settings);
        }

        private void Commit(ProxySettings settings)
        {
            if (!this.persist)
            {
                this.Result = settings;
                this.DialogResult = DialogResult.OK;
                this.Close();
                return;
            }

            if (this.encrypted && string.IsNullOrEmpty(this.passKey))
            {
                MessageBox.Show("Your manifest is encrypted but no passkey is loaded, so the proxy cannot be saved securely.",
                    "Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!ProxyStore.Save(this.steamId, settings, this.encrypted, this.passKey))
            {
                MessageBox.Show("Unable to save the proxy settings.", "Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ApplyToLiveAccount(settings);

            this.Result = settings;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (!this.persist)
            {
                this.Result = null;
                this.DialogResult = DialogResult.OK;
                this.Close();
                return;
            }

            DialogResult confirm = MessageBox.Show("Remove the proxy for this account?", "Proxy",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (confirm != DialogResult.OK) return;

            if (!ProxyStore.Delete(this.steamId))
            {
                MessageBox.Show("Unable to delete the proxy settings.", "Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ApplyToLiveAccount(null);

            this.Result = null;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// Pushes the change onto the in-memory account and drops the cached browser
        /// environment, so neither keeps using the previous proxy until a restart.
        /// </summary>
        private void ApplyToLiveAccount(ProxySettings settings)
        {
            if (this.account != null)
                this.account.SetWebProxy(settings == null ? null : settings.ToWebProxy());

            AccountBrowserManager.Invalidate(this.steamId);
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
