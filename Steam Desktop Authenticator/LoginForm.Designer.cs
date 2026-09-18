namespace Steam_Desktop_Authenticator
{
    partial class LoginForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LoginForm));
            this.label1 = new System.Windows.Forms.Label();
            this.txtUsername = new System.Windows.Forms.TextBox();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.btnSteamLogin = new System.Windows.Forms.Button();
            this.labelLoginExplanation = new System.Windows.Forms.Label();
            this.labelProxy = new System.Windows.Forms.Label();
            this.cmbProxy = new System.Windows.Forms.ComboBox();
            this.btnProxyEdit = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label1.Location = new System.Drawing.Point(12, 30);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(86, 25);
            this.label1.TabIndex = 0;
            this.label1.Text = "Username:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtUsername
            // 
            this.txtUsername.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtUsername.Location = new System.Drawing.Point(101, 31);
            this.txtUsername.Name = "txtUsername";
            this.txtUsername.Size = new System.Drawing.Size(220, 25);
            this.txtUsername.TabIndex = 1;
            // 
            // txtPassword
            // 
            this.txtPassword.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtPassword.Location = new System.Drawing.Point(101, 63);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.PasswordChar = '*';
            this.txtPassword.Size = new System.Drawing.Size(220, 25);
            this.txtPassword.TabIndex = 3;
            // 
            // label2
            // 
            this.label2.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label2.Location = new System.Drawing.Point(12, 62);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(83, 25);
            this.label2.TabIndex = 2;
            this.label2.Text = "Password:";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // labelProxy
            // 
            this.labelProxy.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelProxy.ForeColor = System.Drawing.SystemColors.ControlText;
            this.labelProxy.Location = new System.Drawing.Point(12, 94);
            this.labelProxy.Name = "labelProxy";
            this.labelProxy.Size = new System.Drawing.Size(83, 25);
            this.labelProxy.TabIndex = 4;
            this.labelProxy.Text = "Proxy:";
            this.labelProxy.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // cmbProxy
            // 
            this.cmbProxy.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbProxy.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmbProxy.Location = new System.Drawing.Point(101, 95);
            this.cmbProxy.Name = "cmbProxy";
            this.cmbProxy.Size = new System.Drawing.Size(158, 25);
            this.cmbProxy.TabIndex = 5;
            this.cmbProxy.SelectedIndexChanged += new System.EventHandler(this.cmbProxy_SelectedIndexChanged);
            // 
            // btnProxyEdit
            // 
            this.btnProxyEdit.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnProxyEdit.Location = new System.Drawing.Point(265, 94);
            this.btnProxyEdit.Name = "btnProxyEdit";
            this.btnProxyEdit.Size = new System.Drawing.Size(56, 27);
            this.btnProxyEdit.TabIndex = 6;
            this.btnProxyEdit.Text = "Edit...";
            this.btnProxyEdit.UseVisualStyleBackColor = true;
            this.btnProxyEdit.Click += new System.EventHandler(this.btnProxyEdit_Click);
            // 
            // btnSteamLogin
            // 
            this.btnSteamLogin.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSteamLogin.Location = new System.Drawing.Point(224, 186);
            this.btnSteamLogin.Name = "btnSteamLogin";
            this.btnSteamLogin.Size = new System.Drawing.Size(110, 33);
            this.btnSteamLogin.TabIndex = 8;
            this.btnSteamLogin.Text = "Login";
            this.btnSteamLogin.UseVisualStyleBackColor = true;
            this.btnSteamLogin.Click += new System.EventHandler(this.btnSteamLogin_Click);
            // 
            // labelLoginExplanation
            // 
            this.labelLoginExplanation.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelLoginExplanation.Location = new System.Drawing.Point(15, 133);
            this.labelLoginExplanation.Name = "labelLoginExplanation";
            this.labelLoginExplanation.Size = new System.Drawing.Size(306, 46);
            this.labelLoginExplanation.TabIndex = 7;
            this.labelLoginExplanation.Text = "This will activate Steam Desktop Authenticator on your Steam account. This requir" +
    "es a phone number that can receive SMS.";
            // 
            // LoginForm
            // 
            this.AcceptButton = this.btnSteamLogin;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(346, 232);
            this.Controls.Add(this.btnProxyEdit);
            this.Controls.Add(this.cmbProxy);
            this.Controls.Add(this.labelProxy);
            this.Controls.Add(this.labelLoginExplanation);
            this.Controls.Add(this.btnSteamLogin);
            this.Controls.Add(this.txtPassword);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.txtUsername);
            this.Controls.Add(this.label1);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.ImeMode = System.Windows.Forms.ImeMode.On;
            this.MaximizeBox = false;
            this.Name = "LoginForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Steam Login";
            this.Load += new System.EventHandler(this.LoginForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelProxy;
        private System.Windows.Forms.ComboBox cmbProxy;
        private System.Windows.Forms.Button btnProxyEdit;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtUsername;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnSteamLogin;
        private System.Windows.Forms.Label labelLoginExplanation;
    }
}