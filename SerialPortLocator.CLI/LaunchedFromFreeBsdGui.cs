namespace SerialPortLocator.CLI;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[SupportedOSPlatform("freebsd")]
internal partial class LaunchedFromFreeBsdGui : LaunchedFromGui
{
    // Import the native POSIX function used by FreeBSD
    [LibraryImport("libc", EntryPoint = "isatty", SetLastError = true)]
    private static partial int IsATty(int fd);

    /// <summary>
    /// Check if standard input/output streams are not bound to a physical
    /// terminal device using the POSIX <c>isatty</c> function, where file
    /// descriptor 0 is Standard Input (stdin) and file descriptor 1 is
    /// Standard Output (stdout).
    /// </summary>
    /// <returns><see langword="true"/> when <c>stdin</c> and <c>stdout</c> are
    /// not bound to a physical terminal device, otherwise
    /// <see langword="false"/>.</returns>
    protected override bool DetectCore() => IsATty(0) != 1 && IsATty(1) != 1;
}
