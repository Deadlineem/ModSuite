using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LauncherBase
{
    public class GameTab
    {
        public string GameName { get; set; }
        public string DownloadUrl { get; set; }
        public string ExecutablePath { get; set; } // Menu/Trainer Executable
        public string DllPath { get; set; } // DLL
        public string GamePath { get; set; } // Game Executable
        public bool ClearCache { get; set; }
        public bool UninstallOption { get; set; }
    }

}