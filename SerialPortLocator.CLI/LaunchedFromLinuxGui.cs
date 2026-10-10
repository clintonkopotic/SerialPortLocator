namespace SerialPortLocator.CLI;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;

[SupportedOSPlatform("linux")]
internal class LaunchedFromLinuxGui : LaunchedFromGui
{
    // Common Linux shells.
    static HashSet<string> CommonShells { get; }
        = ["bash", "zsh", "sh", "fish", "dash", "tcsh", "csh"];

    protected override bool DetectCore()
    {
        try
        {
            // Read the Parent Process ID (PPID) from file /proc/[pid]/stat
            // The 4th element in the space-separated string is the PPID.
            string statText = File.ReadAllText(
                $"/proc/{Environment.ProcessId}/stat");
            string[] statParts = statText.Split(' ');
            string ppid = statParts[3];

            // Get the name of that parent process from /proc/[ppid]/comm
            string parentName = File.ReadAllText($"/proc/{ppid}/comm").Trim();

            // If parentName is not a common shell, then this process was
            // launched from the GUI.
            return !CommonShells.Contains(parentName);
        }
        catch
        {
            // Fallback strategy.
            // NOTE: Can return false positives if the application was
            // intentionally ran in a shell script with piped inputs.
            return Console.IsInputRedirected;
        }
    }
}
