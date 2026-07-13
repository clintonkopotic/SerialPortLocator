namespace SerialPortLocator.CLI;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[SupportedOSPlatform("windows")]
internal partial class LaunchedFromWindowsGui : LaunchedFromGui
{
    // Import the native Windows API function
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint GetConsoleProcessList([Out] uint[] processList,
        uint processCount);

    protected override bool DetectCore()
    {
        // Allocate an array for a single process ID to probe the list size.
        uint[] processList = new uint[1];
        uint processCount = GetConsoleProcessList(processList, 1);

        // If count is 1, this app is the only process owning/attached to the
        // console.
        return processCount == 1;
    }
}
