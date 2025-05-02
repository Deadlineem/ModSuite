using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace LauncherBase
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Handle the assembly resolution for embedded resources
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                // First, check if the requested assembly is Newtonsoft.Json
                if (args.Name.StartsWith("Newtonsoft.Json"))
                {
                    string resourceName = "LauncherBase.Newtonsoft.Json.dll"; // Adjust the namespace and file name as needed

                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                    {
                        if (stream == null) return null; // Couldn't find the embedded resource

                        byte[] assemblyData = new byte[stream.Length];
                        stream.Read(assemblyData, 0, assemblyData.Length);
                        return Assembly.Load(assemblyData); // Load the embedded assembly
                    }
                }

                // Handle the Guna.UI2.dll assembly resolution (if necessary)
                if (args.Name.StartsWith("Guna.UI2"))
                {
                    string resourceName = "LauncherBase.Guna.UI2.dll"; // Adjust the namespace and file name as needed

                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                    {
                        if (stream == null) return null;

                        byte[] assemblyData = new byte[stream.Length];
                        stream.Read(assemblyData, 0, assemblyData.Length);
                        return Assembly.Load(assemblyData);
                    }
                }

                return null; // Let the default resolution mechanism take over for other assemblies
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LauncherBase());
        }
    }
}
