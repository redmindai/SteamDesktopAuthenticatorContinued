using System;
using System.Windows.Forms;

namespace Steam_Desktop_Authenticator
{
    public partial class SettingsForm : Form
    {
        Manifest manifest;
        bool fullyLoaded = false;

        public SettingsForm()
        {
            InitializeComponent();

            // Get latest manifest
            manifest = Manifest.GetManifest(true);

            chkPeriodicChecking.Checked = manifest.PeriodicChecking;
            numPeriodicInterval.Value = manifest.PeriodicCheckingInterval;
            chkCheckAll.Checked = manifest.CheckAllAccounts;
            chkConfirmMarket.Checked = manifest.AutoConfirmMarketTransactions;
            chkConfirmTrades.Checked = manifest.AutoConfirmTrades;

            chkBrowserLogging.Checked = manifest.BrowserLogging;

            PopulateLanguages();

            SetControlsEnabledState(chkPeriodicChecking.Checked);

            fullyLoaded = true;
        }

        private void SettingsForm_Load(object sender, EventArgs e)
        {
            LocalizationManager.ApplyTo(this);
            DarkTheme.Apply(this);
        }

        private void PopulateLanguages()
        {
            cmbLanguage.Items.Clear();
            cmbLanguage.Items.AddRange(LocalizationManager.AvailableLanguages);

            string saved = string.IsNullOrWhiteSpace(manifest.Language)
                ? LocalizationManager.DefaultLanguage
                : manifest.Language;

            for (int i = 0; i < cmbLanguage.Items.Count; i++)
            {
                var option = (LocalizationManager.LanguageOption)cmbLanguage.Items[i];
                if (option.Code == saved)
                {
                    cmbLanguage.SelectedIndex = i;
                    return;
                }
            }

            cmbLanguage.SelectedIndex = 0;
        }

        private void SetControlsEnabledState(bool enabled)
        {
            numPeriodicInterval.Enabled = chkCheckAll.Enabled = chkConfirmMarket.Enabled = chkConfirmTrades.Enabled = enabled;
        }

        private void ShowWarning(CheckBox affectedBox)
        {
            if (!fullyLoaded) return;

            var result = MessageBox.Show(LocalizationManager.T("SettingsForm.msg.SecurityWarning", "Warning: enabling this will severely reduce the security of your items! Use of this option is at your own risk. Would you like to continue?"), LocalizationManager.T("SettingsForm.title.Warning", "Warning!"), MessageBoxButtons.YesNo);
            if (result == DialogResult.No)
            {
                affectedBox.Checked = false;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            manifest.PeriodicChecking = chkPeriodicChecking.Checked;
            manifest.PeriodicCheckingInterval = (int)numPeriodicInterval.Value;
            manifest.CheckAllAccounts = chkCheckAll.Checked;
            manifest.AutoConfirmMarketTransactions = chkConfirmMarket.Checked;
            manifest.AutoConfirmTrades = chkConfirmTrades.Checked;

            manifest.BrowserLogging = chkBrowserLogging.Checked;
            BrowserConsoleLog.Enabled = manifest.BrowserLogging;

            var language = cmbLanguage.SelectedItem as LocalizationManager.LanguageOption;
            bool languageChanged = language != null && language.Code != manifest.Language;
            if (language != null) manifest.Language = language.Code;

            manifest.Save();

            // Open windows keep the strings they were built with; re-translating them live
            // is not worth the complexity for a setting changed once.
            if (languageChanged)
            {
                MessageBox.Show(
                    LocalizationManager.T("SettingsForm.msg.RestartNeeded",
                        "The new language will be applied the next time you start SDA."),
                    LocalizationManager.T("SettingsForm.title.Settings", "Settings"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            this.Close();
        }

        private void chkPeriodicChecking_CheckedChanged(object sender, EventArgs e)
        {
            SetControlsEnabledState(chkPeriodicChecking.Checked);
        }

        private void chkConfirmMarket_CheckedChanged(object sender, EventArgs e)
        {
            if (chkConfirmMarket.Checked)
                ShowWarning(chkConfirmMarket);
        }

        private void chkConfirmTrades_CheckedChanged(object sender, EventArgs e)
        {
            if (chkConfirmTrades.Checked)
                ShowWarning(chkConfirmTrades);
        }
    }
}
