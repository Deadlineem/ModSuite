using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using System.Net.Http;
using Microsoft.VisualBasic;
using Guna.UI2.WinForms;


namespace LauncherBase
{
    public partial class LauncherBase : Form
    {

        // Injector Directives
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress,
            uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
            byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("ntdll.dll", SetLastError = true)]
        static extern uint NtCreateThreadEx(out IntPtr threadHandle,
            uint desiredAccess,
            IntPtr objectAttributes,
            IntPtr processHandle,
            IntPtr startAddress,
            IntPtr parameter,
            bool createSuspended,
            int stackZeroBits,
            int sizeOfStack,
            int maximumStackSize,
            IntPtr attributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess,
            IntPtr lpThreadAttributes,
            uint dwStackSize,
            IntPtr lpStartAddress,
            IntPtr lpParameter,
            uint dwCreationFlags,
            out uint lpThreadId);

        [DllImport("kernel32.dll")]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool IsWow64Process(IntPtr hProcess, out bool wow64);

        const int PROCESS_ALL_ACCESS = 0x1F0FFF;
        const uint MEM_COMMIT = 0x1000;
        const uint MEM_RESERVE = 0x2000;
        const uint PAGE_READWRITE = 0x04;

        public bool InjectDll(string processName, string dllPath)
        {
            if (!File.Exists(dllPath))
            {
                MessageBox.Show("DLL not found:\n" + dllPath);
                return false;
            }

            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                MessageBox.Show("Target process not found: " + processName);
                return false;
            }

            var process = processes[0];

            // Bitness check
            bool isTargetWow64;
            if (!IsWow64Process(process.Handle, out isTargetWow64))
            {
                MessageBox.Show("Failed to determine target process architecture.");
                return false;
            }

            bool isTarget64Bit = Environment.Is64BitOperatingSystem && !isTargetWow64;
            if (Environment.Is64BitProcess != isTarget64Bit)
            {
                MessageBox.Show("Bitness mismatch:\n" +
                    $"Injector: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}\n" +
                    $"Target: {(isTarget64Bit ? "64-bit" : "32-bit")}\n" +
                    "You must match bitness between injector and target process.");
                return false;
            }

            IntPtr hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, process.Id);
            if (hProcess == IntPtr.Zero)
            {
                MessageBox.Show("Failed to open target process.");
                return false;
            }

            byte[] dllBytes = Encoding.ASCII.GetBytes(dllPath + '\0');
            IntPtr allocMem = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)dllBytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (allocMem == IntPtr.Zero)
            {
                MessageBox.Show("Failed to allocate memory in target process.");
                CloseHandle(hProcess);
                return false;
            }

            bool written = WriteProcessMemory(hProcess, allocMem, dllBytes, (uint)dllBytes.Length, out _);
            if (!written)
            {
                MessageBox.Show("Failed to write DLL path into target process.");
                CloseHandle(hProcess);
                return false;
            }

            IntPtr kernel32 = GetModuleHandle("kernel32.dll");
            IntPtr loadLibraryAddr = GetProcAddress(kernel32, "LoadLibraryA");

            if (kernel32 == IntPtr.Zero || loadLibraryAddr == IntPtr.Zero)
            {
                MessageBox.Show("Failed to get LoadLibraryA address.");
                CloseHandle(hProcess);
                return false;
            }

            IntPtr threadHandle;
            uint ntResult = NtCreateThreadEx(out threadHandle, 0x1FFFFF, IntPtr.Zero, hProcess, loadLibraryAddr, allocMem, false, 0, 0, 0, IntPtr.Zero);

            if (ntResult != 0 || threadHandle == IntPtr.Zero)
            {
                // Fallback to CreateRemoteThread
                uint threadId;
                threadHandle = CreateRemoteThread(hProcess, IntPtr.Zero, 0, loadLibraryAddr, allocMem, 0, out threadId);
                if (threadHandle == IntPtr.Zero)
                {
                    MessageBox.Show("DLL injection failed.\nNtCreateThreadEx and CreateRemoteThread both failed.\nError: " + Marshal.GetLastWin32Error());
                    CloseHandle(hProcess);
                    return false;
                }
            }

            // Optional: wait for the thread to run
            WaitForSingleObject(threadHandle, 5000);

            CloseHandle(threadHandle);
            CloseHandle(hProcess);
            return true;
        }

        public LauncherBase()
        {
            InitializeComponent();
        }

        private void guna2CircleButton1_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void guna2CircleButton2_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void guna2GradientButton1_Click(object sender, EventArgs e)
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string targetFolder = Path.Combine(appDataPath, "ModSuite", "GTA V (Legacy)");
            string fileUrl = "https://github.com/Mr-X-GTA/YimMenu/releases/download/nightly/YimMenu.dll";
            string savePath = Path.Combine(targetFolder, "YimMenu.dll");

            try
            {
                // Create directory if it doesn't exist
                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(fileUrl, savePath);
                }

                MessageBox.Show("YimMenu.dll downloaded & saved to:\n" + savePath, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Download failed:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void guna2GradientButton2_Click(object sender, EventArgs e)
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string targetFolder = Path.Combine(appDataPath, "ModSuite", "GTA V (Legacy)");
            string fileUrl = "https://github.com/Deadlineem/Chronix/releases/download/nightly/Chronix.dll";
            string savePath = Path.Combine(targetFolder, "Chronix.dll");

            try
            {
                // Create directory if it doesn't exist
                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(fileUrl, savePath);
                }

                MessageBox.Show("Download completed and saved to:\n" + savePath, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Download failed:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void guna2GradientButton3_Click(object sender, EventArgs e)
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ModSuite\\GTA V (Legacy)\\YimMenu.dll");

            bool success = InjectDll("GTA5", path); // "GTA5" is the process name without .exe
        }

        private void guna2GradientButton4_Click(object sender, EventArgs e)
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ModSuite\\GTA V (Legacy)\\Chronix.dll");

            bool success = InjectDll("GTA5", path); // "GTA5" is the process name without .exe
        }

        private void guna2GradientButton5_Click(object sender, EventArgs e)
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string targetFolder = Path.Combine(appDataPath, "ModSuite", "GTA V (Enhanced)");
            string fileUrl = "https://github.com/YimMenu/YimMenuV2/releases/download/nightly/YimMenuV2.dll";
            string savePath = Path.Combine(targetFolder, "YimMenuV2.dll");

            try
            {
                // Create directory if it doesn't exist
                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(fileUrl, savePath);
                }

                MessageBox.Show("Download completed and saved to:\n" + savePath, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Download failed:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void guna2GradientButton6_Click(object sender, EventArgs e)
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ModSuite\\GTA V (Enhanced)\\YimMenuV2.dll");

            bool success = InjectDll("GTA5_Enhanced", path); // "GTA5" is the process name without .exe
        }

        private void guna2GradientButton7_Click(object sender, EventArgs e)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string[] cachePaths = {
        Path.Combine(appData, "YimMenu", "cache"),
        Path.Combine(appData, "Chronix", "cache")
    };

            foreach (string cacheDir in cachePaths)
            {
                try
                {
                    if (Directory.Exists(cacheDir))
                    {
                        Directory.Delete(cacheDir, true);
                        MessageBox.Show($"Deleted: {cacheDir}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Not found: {cacheDir}", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete {cacheDir}\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void guna2GradientButton9_Click(object sender, EventArgs e)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string yimMenuFolder = Path.Combine(appData, "YimMenu");
            string yimMenuDllPath = Path.Combine(appData, "ModSuite", "GTA V (Legacy)", "YimMenu.dll");

            try
            {
                // Delete YimMenu folder
                if (Directory.Exists(yimMenuFolder))
                {
                    Directory.Delete(yimMenuFolder, true); // true to delete all contents
                    MessageBox.Show("Deleted the YimMenu folder successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("YimMenu folder not found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                // Delete YimMenu.dll
                if (File.Exists(yimMenuDllPath))
                {
                    File.Delete(yimMenuDllPath);
                    MessageBox.Show("YimMenu has been uninstalled successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("YimMenu.dll not found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete the YimMenu folder and DLL.\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void guna2GradientButton10_Click(object sender, EventArgs e)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string chronixFolder = Path.Combine(appData, "Chronix");
            string chronixDllPath = Path.Combine(appData, "ModSuite", "GTA V (Legacy)", "Chronix.dll");

            try
            {
                // Delete YimMenu folder
                if (Directory.Exists(chronixFolder))
                {
                    Directory.Delete(chronixFolder, true); // true to delete all contents
                }
                else
                {
                    MessageBox.Show("Chronix folder not found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                // Delete YimMenu.dll
                if (File.Exists(chronixDllPath))
                {
                    File.Delete(chronixDllPath);
                    MessageBox.Show("Chronix has been uninstalled successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Chronix.dll not found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete Chronix.\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void guna2GradientButton8_Click(object sender, EventArgs e)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string yimSettings = Path.Combine(appData, "YimMenu", "settings.json");
            string chronixSettings = Path.Combine(appData, "Chronix", "settings.json");

            try
            {
                int deleted = 0;

                if (File.Exists(yimSettings))
                {
                    File.Delete(yimSettings);
                    deleted++;
                }

                if (File.Exists(chronixSettings))
                {
                    File.Delete(chronixSettings);
                    deleted++;
                }

                if (deleted > 0)
                {
                    MessageBox.Show("Deleted settings.json file(s) successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("No settings.json files found to delete.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting settings.json files:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void guna2GradientButton11_Click(object sender, EventArgs e)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string yimV2Folder = Path.Combine(appData, "YimMenuV2");
            string yimV2DllPath = Path.Combine(appData, "ModSuite", "GTA V (Enhanced)", "YimMenuV2.dll");

            try
            {
                // Delete YimMenu folder
                if (Directory.Exists(yimV2Folder))
                {
                    Directory.Delete(yimV2Folder, true); // true to delete all contents
                }
                else
                {
                    MessageBox.Show("YimMenuV2 folder not found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                // Delete YimMenu.dll
                if (File.Exists(yimV2DllPath))
                {
                    File.Delete(yimV2DllPath);
                    MessageBox.Show("YimMenuV2 has been uninstalled successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("YimMenuV2.dll not found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete YimMenuV2.\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public GameTab NewGameTab { get; private set; }

        private string jsonFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ModSuite", "user_tabs.json");
        private List<GameTab> userTabs = new List<GameTab>();
        private void Form1_Load(object sender, EventArgs e)
        {
            LoadUserTabs();
            InitializeAddGameTab();
        }
        private void LoadUserTabs()
        {
            // Check if the JSON file exists
            if (!File.Exists(jsonFilePath)) return;

            // Read the JSON file
            string json = File.ReadAllText(jsonFilePath);

            // Deserialize the JSON into the userTabs list
            userTabs = JsonConvert.DeserializeObject<List<GameTab>>(json);

            // Check for existing tabs, including their names
            List<string> existingTabNames = guna2TabControl1.TabPages
                .Cast<TabPage>()
                .Select(tab => tab.Text)
                .ToList();

            // Identify the + Add Game tab
            var addGameTab = guna2TabControl1.TabPages
                .Cast<TabPage>()
                .FirstOrDefault(tab => tab.Text == "+ Add Mod");

            // Remove the + Add Game tab temporarily if it exists
            if (addGameTab != null)
            {
                guna2TabControl1.TabPages.Remove(addGameTab);
            }

            // Add user tabs, ensuring no duplicates
            foreach (var tab in userTabs)
            {
                if (!string.IsNullOrWhiteSpace(tab.GameName) && !existingTabNames.Contains(tab.GameName))
                {
                    // Create and add the tab for the game
                    CreateGameTab(tab);
                }
            }

            // After adding user tabs, re-add the + Add Game tab
            if (addGameTab != null)
            {
                guna2TabControl1.TabPages.Add(addGameTab);
            }
        }

        private void CreateGameTab(GameTab tabData)
        {
            var tab = new TabPage(tabData.GameName);
            tab.BackColor = Color.FromArgb(24, 24, 24);

            string gameFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ModSuite", tabData.GameName);
            Directory.CreateDirectory(gameFolder);

            string fileName = Path.GetFileName(tabData.DownloadUrl);
            if (!fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".exe";
            }

            string filePath = Path.Combine(gameFolder, fileName);

            // Download button
            var downloadButton = new Guna.UI2.WinForms.Guna2GradientButton
            {
                Text = "Download " + tabData.GameName + " Mod",
                Location = new Point(8, 35),
                Size = new Size(180, 45),
                Animated = true,
                BackColor = Color.Transparent,
                BorderRadius = 5,
                FillColor = Color.Red,
                FillColor2 = Color.FromArgb(50, 0, 0),
                ForeColor = Color.White,
                GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Vertical,
                ShadowDecoration = { Depth = 10, Enabled = true }
            };

            downloadButton.Click += async (s, e) =>
            {
                using (var client = new HttpClient())
                {
                    try
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                        var data = await client.GetByteArrayAsync(tabData.DownloadUrl);
                        File.WriteAllBytes(filePath, data);
                        MessageBox.Show($"Download completed: {filePath}");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Download failed: " + ex.Message);
                    }
                }
            };


            // Launch/Inject button
            var actionButton = new Guna.UI2.WinForms.Guna2GradientButton
            {
                Text = filePath.EndsWith(".dll") ? "Inject DLL" : "Launch Trainer",
                Location = new Point(8, 87),
                Size = new Size(180, 45),
                Animated = true,
                BackColor = Color.Transparent,
                BorderRadius = 5,
                FillColor = Color.Red,
                FillColor2 = Color.FromArgb(50, 0, 0),
                ForeColor = Color.White,
                GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Vertical,
                ShadowDecoration = { Depth = 10, Enabled = true }
            };

            actionButton.Click += (s, e) =>
            {
                if (File.Exists(filePath))
                {
                    if (filePath.EndsWith(".dll"))
                    {
                        // Use a Guna2 dialog to get the process name
                        var processNameTextBox = new Guna.UI2.WinForms.Guna2TextBox
                        {
                            PlaceholderText = "Enter the process name (without .exe)",
                            Location = new Point(8, 90),
                            Size = new Size(280, 45),
                            BorderRadius = 5,
                            BorderColor = Color.Red,
                            BorderThickness = 1,
                            FillColor = Color.FromArgb(34, 34, 34),
                            ForeColor = Color.White,
                            PlaceholderForeColor = Color.Gray,
                            BackColor = Color.Transparent,
                            Animated = true,
                            FocusedState =
                            {
                                BorderColor = Color.DarkRed
                            },
                                                    HoverState =
                            {
                                BorderColor = Color.DarkOrange
                            }
                        };



                        var inputDialog = new Guna.UI2.WinForms.Guna2Panel
                        {
                            Size = new Size(420, 200),
                            Location = new Point(0, 120),
                            BackColor = Color.FromArgb(24, 24, 24)
                        };

                        var okButton = new Guna.UI2.WinForms.Guna2GradientButton
                        {
                            Text = "Inject",
                            Location = new Point(8, 145),
                            Size = new Size(180, 45),
                            Animated = true,
                            BackColor = Color.Transparent,
                            BorderRadius = 5,
                            FillColor = Color.Red,
                            FillColor2 = Color.FromArgb(50, 0, 0),
                            ForeColor = Color.White,
                            GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Vertical,
                            ShadowDecoration = { Depth = 10, Enabled = true }
                        };

                        okButton.Click += (sender, args) =>
                        {
                            string processName = processNameTextBox.Text;
                            if (!string.IsNullOrWhiteSpace(processName))
                            {
                                bool success = InjectDll(processName, filePath);
                                MessageBox.Show(success ? "DLL injected successfully." : "DLL injection failed.");
                            }
                            else
                            {
                                MessageBox.Show("Please enter a valid process name.");
                            }

                            inputDialog.Visible = false;  // Hide the dialog after pressing OK
                        };

                        inputDialog.Controls.Add(processNameTextBox);
                        inputDialog.Controls.Add(okButton);
                        tab.Controls.Add(inputDialog);
                    }
                    else
                    {
                        Process.Start(filePath);
                    }
                }
                else
                {
                    MessageBox.Show("File not found. Please download first.");
                }
            };

            // Uninstall button
            if (tabData.UninstallOption)
            {
                var uninstallButton = new Guna.UI2.WinForms.Guna2GradientButton
                {
                    Text = "Uninstall",
                    Location = new Point(8, 139),
                    Size = new Size(180, 45),
                    Animated = true,
                    BackColor = Color.Transparent,
                    BorderRadius = 5,
                    FillColor = Color.Red,
                    FillColor2 = Color.FromArgb(50, 0, 0),
                    ForeColor = Color.White,
                    GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Vertical,
                    ShadowDecoration = { Depth = 10, Enabled = true }
                };

                uninstallButton.Click += (s, e) =>
                {
                    if (Directory.Exists(gameFolder))
                    {
                        Directory.Delete(gameFolder, true);
                    }

                    // Load existing tab data
                    string jsonPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ModSuite", "user_tabs.json");
                    if (File.Exists(jsonPath))
                    {
                        try
                        {
                            string json = File.ReadAllText(jsonPath);
                            var tabList = JsonConvert.DeserializeObject<List<GameTab>>(json);

                            // Remove the tab with matching name
                            tabList.RemoveAll(t => t.GameName == tabData.GameName);

                            // Save updated list
                            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(tabList, Formatting.Indented));
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to update tab data: {ex.Message}");
                        }
                    }

                    // Remove tab from UI
                    guna2TabControl1.TabPages.Remove(tab);
                    MessageBox.Show("Game files and tab data deleted.");
                };


                tab.Controls.Add(uninstallButton);
            }

            var gamePathTextBox = new Guna.UI2.WinForms.Guna2TextBox
            {
                PlaceholderText = "Game exe path OR URI (ex. com.epicgames.launcher://apps/Fortnite?action=launch&silent=true)",
                Text = tabData.GamePath ?? string.Empty,
                Location = new Point(200, 35),
                Size = new Size(360, 45),
                BorderRadius = 5,
                BorderColor = Color.Red,
                BorderThickness = 1,
                FillColor = Color.FromArgb(34, 34, 34),
                ForeColor = Color.White,
                PlaceholderForeColor = Color.Gray,
                BackColor = Color.Transparent,
                Animated = true,
                FocusedState = { BorderColor = Color.DarkRed },
                HoverState = { BorderColor = Color.DarkOrange }
            };

            var browseButton = new Guna.UI2.WinForms.Guna2GradientButton
            {
                Text = "Browse...",
                Location = new Point(570, 35),
                Size = new Size(100, 45),
                Animated = true,
                BorderRadius = 5,
                FillColor = Color.Red,
                FillColor2 = Color.FromArgb(50, 0, 0),
                ForeColor = Color.White,
                GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Vertical,
                ShadowDecoration = { Depth = 10, Enabled = true }
            };

            browseButton.Click += (s, e) =>
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Executable files (*.exe)|*.exe";
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        gamePathTextBox.Text = dialog.FileName;
                        tabData.GamePath = dialog.FileName;
                    }
                }
            };

            var launchGameButton = new Guna.UI2.WinForms.Guna2GradientButton
            {
                Text = "Launch Game",
                Location = new Point(200, 87),
                Size = new Size(180, 45),
                Animated = true,
                BackColor = Color.Transparent,
                BorderRadius = 5,
                FillColor = Color.Red,
                FillColor2 = Color.FromArgb(50, 0, 0),
                ForeColor = Color.White,
                GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Vertical,
                ShadowDecoration = { Depth = 10, Enabled = true }
            };

            var launchAsAdminCheckBox = new Guna.UI2.WinForms.Guna2CheckBox
            {
                Text = "Run as Administrator",
                Location = new Point(390, 97),
                Size = new Size(180, 25),
                CheckedState = { FillColor = Color.Red },
                UncheckedState = { FillColor = Color.DarkGray },
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true
            };

            launchGameButton.Click += (s, e) =>
            {
                string pathToLaunch = gamePathTextBox.Text.Trim();

                if (!string.IsNullOrEmpty(pathToLaunch))
                {
                    try
                    {
                        // ✅ Update the GamePath in userTabs and save
                        var currentTab = userTabs.FirstOrDefault(t => t.GameName == tabData.GameName);
                        if (currentTab != null)
                        {
                            currentTab.GamePath = pathToLaunch;

                            string updatedJson = JsonConvert.SerializeObject(userTabs, Formatting.Indented);
                            File.WriteAllText(jsonFilePath, updatedJson);
                        }

                        // ✅ Check if it's a URI launch (Epic Games, etc.)
                        if (pathToLaunch.StartsWith("com.epicgames.launcher://", StringComparison.OrdinalIgnoreCase))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = pathToLaunch,
                                UseShellExecute = true
                            });
                        }
                        else if (File.Exists(pathToLaunch))
                        {
                            // ✅ Launch game with optional admin
                            var startInfo = new ProcessStartInfo(pathToLaunch);
                            if (launchAsAdminCheckBox.Checked)
                            {
                                startInfo.UseShellExecute = true;
                                startInfo.Verb = "runas";
                            }

                            Process.Start(startInfo);
                        }
                        else
                        {
                            MessageBox.Show("Game executable or URI not found. Please provide a valid path or Epic Games URI.");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to launch game.\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Please enter a valid game path or Epic URI.");
                }
            };

            tab.Controls.Add(downloadButton);
            tab.Controls.Add(actionButton);
            tab.Controls.Add(gamePathTextBox);
            tab.Controls.Add(browseButton);
            tab.Controls.Add(launchGameButton);
            tab.Controls.Add(launchAsAdminCheckBox);
            guna2TabControl1.TabPages.Insert(guna2TabControl1.TabPages.Count - 1, tab);
        }

        private void InitializeAddGameTab()
        {
            // Check if the "+ Add Game" tab already exists
            if (guna2TabControl1.TabPages["AddGameTab"] == null)
            {
                var addTab = new TabPage("+ Add Game");
                addTab.Name = "AddGameTab";
                addTab.BackColor = Color.FromArgb(24, 24, 24); // Set background color
                guna2TabControl1.TabPages.Add(addTab);
            }
        }


        private void Guna2TabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedTab = guna2TabControl1.SelectedTab;

            if (selectedTab != null && selectedTab.Text == "+ Add Game")
            {
                // Temporarily disable event to prevent recursion
                guna2TabControl1.SelectedIndexChanged -= Guna2TabControl1_SelectedIndexChanged;

                // Remove the Add Game tab to prevent blank tab showing
                guna2TabControl1.TabPages.Remove(selectedTab);

                var addForm = new AddGameTab();
                if (addForm.ShowDialog() == DialogResult.OK)
                {
                    var newTab = addForm.NewGameTab;
                    userTabs.Add(newTab);
                    SaveUserTabs();
                    CreateGameTab(newTab);
                }

                // Re-add "+ Add Game" tab at the end
                InitializeAddGameTab();

                // Re-subscribe event
                guna2TabControl1.SelectedIndexChanged += Guna2TabControl1_SelectedIndexChanged;
            }
        }
        private void guna2TabControl1_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (e.TabPage.Text == "+ Add Game")
            {
                e.Cancel = true; // Prevent it from rendering

                var addForm = new AddGameTab();
                if (addForm.ShowDialog() == DialogResult.OK)
                {
                    var newTab = addForm.NewGameTab;
                    userTabs.Add(newTab);
                    SaveUserTabs();
                    CreateGameTab(newTab);
                }

                // Re-add "+ Add Game" to the end in case it got removed
                guna2TabControl1.TabPages.RemoveByKey("+ Add Game");
                InitializeAddGameTab();
            }
        }

        private void SaveUserTabs()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(jsonFilePath));
            File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(userTabs, Formatting.Indented));
        }
    }
}
