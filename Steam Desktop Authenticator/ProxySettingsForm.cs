using SteamAuth;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Steam_Desktop_Authenticator
{
    /// <summary>
    /// Edits the proxy sidecar for one account. The proxy applies to that account's embedded
    /// browser only; code generation and confirmations keep using the direct connection.
    /// </summary>
    public class ProxySettingsForm : Form
    {
        private readonly ulong steamId;
        private readonly bool encrypted;
        private readonly string passKey;

        private readonly TextBox txtHost;
        private readonly NumericUpDown numPort;
        private readonly TextBox txtUsername;
        private readonly TextBox txtPassword;
        private readonly ComboBox cmbType;
        private readonly Button btnSave;
        private readonly Button btnCancel;
        private readonly Button btnDelete;

        public ProxySettingsForm(SteamGuardAccount account, bool encrypted, string passKey)
        {
            this.steamId = account.Session.SteamID;
            this.encrypted = encrypted;
            this.passKey = passKey;

            this.Text = String.Format("Proxy - {0}", account.AccountName);
            this.ClientSize = new Size(340, 232);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int labelLeft = 12;
            int fieldLeft = 110;
            int fieldWidth = 216;
            int y = 15;
            int rowHeight = 30;

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
            this.btnDelete.Text = "Delete";
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
            this.btnSave.Text = "Save";
            this.btnSave.Location = new Point(246, y);
            this.btnSave.Size = new Size(80, 28);
            this.btnSave.Click += btnSave_Click;
            this.Controls.Add(this.btnSave);

            this.AcceptButton = this.btnSave;
            this.CancelButton = this.btnCancel;

            LoadExisting();
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

        private void LoadExisting()
        {
            bool sidecarExists = ProxyStore.Exists(this.steamId);
            this.btnDelete.Enabled = sidecarExists;

            ProxySettings existing = ProxyStore.Load(this.steamId, this.passKey);
            if (existing == null)
            {
                if (sidecarExists)
                {
                    // Present but unreadable: wrong or missing passkey, or a corrupt file.
                    // Saving would silently overwrite it, so make that the user's explicit choice.
                    MessageBox.Show("A proxy is saved for this account but could not be read. Saving will replace it.",
                        "Proxy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                UpdateCredentialFields();
                return;
            }

            this.cmbType.SelectedItem = existing.Type;
            this.txtHost.Text = existing.Host;
            if (existing.Port >= this.numPort.Minimum && existing.Port <= this.numPort.Maximum)
                this.numPort.Value = existing.Port;
            this.txtUsername.Text = existing.Username;
            this.txtPassword.Text = existing.Password;

            UpdateCredentialFields();
        }

        private void cmbType_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateCredentialFields();
        }

        /// <summary>
        /// Chromium only answers proxy auth challenges over HTTP(S); a SOCKS5 proxy never raises
        /// BasicAuthenticationRequested, so credentials there would be silently ignored.
        /// </summary>
        private void UpdateCredentialFields()
        {
            bool supported = (ProxyType)this.cmbType.SelectedItem == ProxyType.Http;
            this.txtUsername.Enabled = supported;
            this.txtPassword.Enabled = supported;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            string host = this.txtHost.Text.Trim();
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("Please enter a proxy host.", "Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (this.encrypted && string.IsNullOrEmpty(this.passKey))
            {
                MessageBox.Show("Your manifest is encrypted but no passkey is loaded, so the proxy cannot be saved securely.",
                    "Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ProxyType type = (ProxyType)this.cmbType.SelectedItem;
            ProxySettings settings = new ProxySettings
            {
                Type = type,
                Host = host,
                Port = (int)this.numPort.Value,
                Username = type == ProxyType.Http ? NullIfEmpty(this.txtUsername.Text) : null,
                Password = type == ProxyType.Http ? NullIfEmpty(this.txtPassword.Text) : null
            };

            if (!ProxyStore.Save(this.steamId, settings, this.encrypted, this.passKey))
            {
                MessageBox.Show("Unable to save the proxy settings.", "Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // The proxy is fixed when the browser environment is built, so force a rebuild.
            AccountBrowserManager.Invalidate(this.steamId);

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            DialogResult confirm = MessageBox.Show("Remove the proxy for this account?", "Proxy",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (confirm != DialogResult.OK) return;

            if (!ProxyStore.Delete(this.steamId))
            {
                MessageBox.Show("Unable to delete the proxy settings.", "Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            AccountBrowserManager.Invalidate(this.steamId);

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
