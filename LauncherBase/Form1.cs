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
    }
}
