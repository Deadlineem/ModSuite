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
        public string ExecutablePath { get; set; } // Path provided by user (not used directly)
        public string DllPath { get; set; } // Only used if it's a DLL
        public bool ClearCache { get; set; }
        public bool UninstallOption { get; set; }
    }

}