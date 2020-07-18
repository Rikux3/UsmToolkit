using System.Diagnostics;

namespace UsmToolkit
{
    public static class Helpers
    {
        public static void ExecuteProcess(string fileName, string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };

            Process process = new Process();
            process.StartInfo = startInfo;

            process.Start();
            process.WaitForExit();
        }
    }
}
