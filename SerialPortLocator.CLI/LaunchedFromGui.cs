namespace SerialPortLocator.CLI;

using System;

internal class LaunchedFromGui
{
    protected virtual bool DetectCore()
        => Console.GetCursorPosition() == (0, 0);

    private static bool? _launchedFromGui = null;

    public static bool Detect()
    {
        if (_launchedFromGui is null)
        {
            LaunchedFromGui detector = new();

            if (OperatingSystem.IsFreeBSD())
            {
                detector = new LaunchedFromFreeBsdGui();
            }
            else if (OperatingSystem.IsLinux())
            {
                detector = new LaunchedFromLinuxGui();
            }
            else if (OperatingSystem.IsMacOS())
            {
                detector = new LaunchedFromMacOsGui();
            }
            else if (OperatingSystem.IsWindows())
            {
                detector = new LaunchedFromWindowsGui();
            }

            _launchedFromGui = detector.DetectCore();
        }

        return _launchedFromGui.Value;
    }
}
