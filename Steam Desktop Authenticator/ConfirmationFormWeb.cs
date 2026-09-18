using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using SteamAuth;
using System.Drawing.Drawing2D;

namespace Steam_Desktop_Authenticator
{
    public partial class ConfirmationFormWeb : Form
    {
        private SteamGuardAccount steamAccount;

        public ConfirmationFormWeb(SteamGuardAccount steamAccount)
        {
            InitializeComponent();
            this.steamAccount = steamAccount;
            this.Text = String.Format("Trade Confirmations - {0}", steamAccount.AccountName);
        }
        private async Task LoadData()
        {
            this.splitContainer1.Panel2.Controls.Clear();

            // Check for a valid refresh token first
            if (steamAccount.Session.IsRefreshTokenExpired())
            {
                MessageBox.Show(LocalizationManager.T("ConfirmationFormWeb.msg.SessionExpired", "Your session has expired. Use the login again button under the selected account menu."), LocalizationManager.T("ConfirmationFormWeb.title.TradeConfirmations", "Trade Confirmations"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return;
            }

            // Check for a valid access token, refresh it if needed
            if (steamAccount.Session.IsAccessTokenExpired())
            {
                try
                {
                    await steamAccount.Session.RefreshAccessToken();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, LocalizationManager.T("ConfirmationFormWeb.title.SteamLoginError", "Steam Login Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.Close();
                    return;
                }
            }

            try
            {
                var confirmations = await steamAccount.FetchConfirmationsAsync();

                if (confirmations == null || confirmations.Length == 0)
                {
                    Label errorLabel = new Label() { Text = "Nothing to confirm/cancel", AutoSize = true, ForeColor = Color.Black, Location = new Point(150, 20) };
                    this.splitContainer1.Panel2.Controls.Add(errorLabel);
                }

                foreach (var confirmation in confirmations)
                {
                    Panel panel = new Panel() { Dock = DockStyle.Top, Height = 120 };
                    panel.Paint += (s, e) =>
                    {
                        using (LinearGradientBrush brush = new LinearGradientBrush(panel.ClientRectangle, Color.Black, Color.DarkCyan, 90F))
                        {
                            e.Graphics.FillRectangle(brush, panel.ClientRectangle);
                        }
                    };

                    if (!string.IsNullOrEmpty(confirmation.Icon))
                    {
                        PictureBox pictureBox = new PictureBox() { Width = 60, Height = 60, Location = new Point(20, 20), SizeMode = PictureBoxSizeMode.Zoom };
                        try
                        {
                            pictureBox.Load(confirmation.Icon);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Failed to load avatar: " + ex.Message);
                        }
                        panel.Controls.Add(pictureBox);
                    }

                    Label nameLabel = new Label()
                    {
                        Text = $"{confirmation.Headline}\n{confirmation.Creator.ToString()}",
                        AutoSize = true,
                        ForeColor = Color.Snow,
                        Location = new Point(90, 20),
                        BackColor = Color.Transparent
                    };
                    panel.Controls.Add(nameLabel);

                    ConfirmationButton acceptButton = new ConfirmationButton()
                    {
                        Text = confirmation.Accept,
                        Location = new Point(90, 50),
                        FlatStyle = FlatStyle.Flat,
                        FlatAppearance = { BorderSize = 0 },
                        BackColor = Color.Black,
                        ForeColor = Color.Snow,
                        Confirmation = confirmation
                    };
                    acceptButton.Click += btnAccept_Click;
                    panel.Controls.Add(acceptButton);

                    ConfirmationButton cancelButton = new ConfirmationButton()
                    {
                        Text = confirmation.Cancel,
                        Location = new Point(180, 50),
                        FlatStyle = FlatStyle.Flat,
                        FlatAppearance = { BorderSize = 0 },
                        BackColor = Color.Black,
                        ForeColor = Color.Snow,
                        Confirmation = confirmation
                    };
                    cancelButton.Click += btnCancel_Click;
                    panel.Controls.Add(cancelButton);

                    Label summaryLabel = new Label()
                    {
                        Text = String.Join("\n", confirmation.Summary),
                        AutoSize = true,
                        ForeColor = Color.Snow,
                        Location = new Point(90, 80),
                        BackColor = Color.Transparent
                    };
                    panel.Controls.Add(summaryLabel);

                    this.splitContainer1.Panel2.Controls.Add(panel);
                }
            }
            catch (Exception ex)
            {
                Label errorLabel = new Label() { Text = "Something went wrong:\n" + ex.Message, AutoSize = true, ForeColor = Color.Red, Location = new Point(20, 20) };
                this.splitContainer1.Panel2.Controls.Add(errorLabel);
            }
        }

        private async void btnAccept_Click(object sender, EventArgs e)
        {
            var button = (ConfirmationButton)sender;
            var confirmation = button.Confirmation;
            button.Enabled = false;
            try
            {
                bool result = await steamAccount.AcceptConfirmation(confirmation);
                if (!result)
                {
                    MessageBox.Show(LocalizationManager.T("ConfirmationFormWeb.msg.AcceptFailed", "Failed to accept. Your session may have expired, or Steam may require you to accept the trade restrictions popup in a browser first (log in at steamcommunity.com → Inventory → Trade Offers)."), LocalizationManager.T("ConfirmationFormWeb.title.TradeConfirmations", "Trade Confirmations"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LocalizationManager.T("ConfirmationFormWeb.msg.AcceptError", "Error accepting: {0}"), ex.Message), LocalizationManager.T("ConfirmationFormWeb.title.TradeConfirmations", "Trade Confirmations"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                button.Enabled = true;
            }
            await this.LoadData();
        }

        private async void btnCancel_Click(object sender, EventArgs e)
        {
            var button = (ConfirmationButton)sender;
            var confirmation = button.Confirmation;
            button.Enabled = false;
            try
            {
                bool result = await steamAccount.DenyConfirmation(confirmation);
                if (!result)
                {
                    MessageBox.Show(LocalizationManager.T("ConfirmationFormWeb.msg.DenyFailed", "Failed to deny confirmation. Your session may have expired."), LocalizationManager.T("ConfirmationFormWeb.title.TradeConfirmations", "Trade Confirmations"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LocalizationManager.T("ConfirmationFormWeb.msg.DenyError", "Error denying: {0}"), ex.Message), LocalizationManager.T("ConfirmationFormWeb.title.TradeConfirmations", "Trade Confirmations"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                button.Enabled = true;
            }
            await this.LoadData();
        }


        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            this.btnRefresh.Enabled = false;
            this.btnRefresh.Text = "Refreshing...";

            await this.LoadData();

            this.btnRefresh.Enabled = true;
            this.btnRefresh.Text = "Refresh";
        }

        private void ConfirmationFormWeb_Load(object sender, EventArgs e)
        {
            LocalizationManager.ApplyTo(this);
            DarkTheme.Apply(this);
        }

        private async void ConfirmationFormWeb_Shown(object sender, EventArgs e)
        {
            this.btnRefresh.Enabled = false;
            this.btnRefresh.Text = "Refreshing...";

            await this.LoadData();

            this.btnRefresh.Enabled = true;
            this.btnRefresh.Text = "Refresh";
        }
    }
}
