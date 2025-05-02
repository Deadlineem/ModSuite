using System;
using System.IO;
using System.Windows.Forms;

namespace LauncherBase
{
    public partial class AddGameTab : Form
    {
        public GameTab NewGameTab { get; private set; }

        public AddGameTab()
        {
            InitializeComponent();
        }

        private void guna2GradientButton1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(guna2TextBox1.Text))
            {
                MessageBox.Show("Please enter a game name.");
                return;
            }

            // Automatically detect if user entered a DLL or EXE path
            string pathInput = guna2TextBox2.Text.Trim();

            string exePath = Path.GetExtension(pathInput).Equals(".exe", StringComparison.OrdinalIgnoreCase) ? pathInput : "";
            string dllPath = Path.GetExtension(pathInput).Equals(".dll", StringComparison.OrdinalIgnoreCase) ? pathInput : "";

            NewGameTab = new GameTab
            {
                GameName = guna2TextBox1.Text.Trim(),
                DownloadUrl = guna2TextBox2.Text.Trim(),
                ExecutablePath = exePath,
                DllPath = dllPath,
                ClearCache = guna2CustomCheckBox2.Checked,
                UninstallOption = guna2CustomCheckBox1.Checked
            };

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        // Optional: Clear default events if not used
        private void guna2TextBox1_TextChanged(object sender, EventArgs e) { }
        private void guna2TextBox2_TextChanged(object sender, EventArgs e) { }
        private void guna2TextBox3_TextChanged(object sender, EventArgs e) { }
        private void guna2CustomCheckBox1_Click(object sender, EventArgs e) { }
        private void guna2CustomCheckBox2_Click(object sender, EventArgs e) { }

        private void guna2CircleButton1_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
