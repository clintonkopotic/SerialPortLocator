namespace SerialPortLocator.CLI;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[SupportedOSPlatform("windows")]
internal partial class LaunchedFromWindowsGui : LaunchedFromGui
{
    /// <summary>
    /// Retrieves a list of the processes attached to the current console.
    /// </summary>
    /// <param name="processList">Receives an array of process identifiers upon
    /// success. This must be a valid buffer and cannot be NULL. The buffer must
    /// have space to receive at least 1 returned process id.</param>
    /// <param name="processCount">The maximum number of process identifiers
    /// that can be stored in <paramref name="processList"/>. This must be
    /// greater than 0.</param>
    /// <returns>If the function succeeds, the return value is less than or
    /// equal to <paramref name="processCount"/> and represents the number of
    /// process identifiers stored in the lpdwProcessList buffer.<br />
    /// If the buffer is too small to hold all the valid process identifiers,
    /// the return value is the required number of array elements. The function
    /// will have stored no identifiers in the buffer. In this situation, use
    /// the return value to allocate a buffer that is large enough to store the
    /// entire list and call the function again.<br />
    /// If the return value is zero, the function has failed, because every
    /// console has at least one process associated with it. To get extended
    /// error information, call GetLastError.
    /// If a NULL process list was provided or the process count was 0, the call
    /// will return 0 and GetLastError will return ERROR_INVALID_PARAMETER.
    /// Please provide a buffer of at least one element to call this function.
    /// Allocate a larger buffer and call again if the return code is larger
    /// than the length of the provided buffer.</returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint GetConsoleProcessList([Out] uint[] processList,
        uint processCount);

    protected override bool DetectCore()
    {
        // Allocate an array for a single process ID to probe the list size.
        var processList = new uint[1];
        uint processCount = GetConsoleProcessList(processList, 1);

        // If count is 1, this app is the only process owning/attached to the
        // console.
        return processCount == 1;
    }
}
