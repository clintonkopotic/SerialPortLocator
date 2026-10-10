namespace SerialPortLocator.CLI;

using System;
using System.Diagnostics;
using System.Runtime.Versioning;

[SupportedOSPlatform("macos")]
internal class LaunchedFromMacOsGui : LaunchedFromGui
{
    const string BundleIdentifier = "__CFBundleIdentifier";

    const string Unknown = "<:|Unknown|:>";

    protected override bool DetectCore()
    {
        var parentName = GetParentProcessName();

        if (parentName.Equals(Unknown))
        {
            // Finder-initiated terminal sessions inject this bundle identifier
            // variable.
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
                BundleIdentifier));
        }

        return !parentName.Contains("zsh") && !parentName.Contains("bash")
            && !parentName.Contains("fish");
    }

    static string GetParentProcessName()
    {
        try
        {
            // Get current process metadata to find the Parent Process ID
            // (PPID).
            using var currentProcess = Process.GetCurrentProcess();

            // Execute the native macOS 'ps' utility to find the command name of
            // the PPID.
            var startInfo = new ProcessStartInfo
            {
                FileName = "ps",
                Arguments = $"-p {currentProcess.Id} -o ppid=",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            string ppidStr = process?.StandardOutput.ReadToEnd().Trim()
                ?? string.Empty;
            process?.WaitForExit();

            if (int.TryParse(ppidStr, out int ppid))
            {
                // Query the actual command name belonging to that parent PID
                var nameStartInfo = new ProcessStartInfo
                {
                    FileName = "ps",
                    Arguments = $"-p {ppid} -o comm=",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var nameProcess = Process.Start(nameStartInfo);
                string parentComm = nameProcess?.StandardOutput.ReadToEnd()
                    .Trim() ?? string.Empty;
                nameProcess?.WaitForExit();

                return parentComm; // e.g., "/bin/zsh" or "/sbin/launchd".
            }
        }
        catch
        {
            // Fallback gracefully if process inspection fails due to
            // permissions.
        }

        return Unknown;
    }
}
