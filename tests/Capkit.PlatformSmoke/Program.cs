// Smoke test for the platform layer: exercises the native bindings on the machine it runs on.
// Missing entry points or libraries fail the run; a missing Screen Recording permission (normal on CI) does not.
using Capkit.HelpersLib;
using Capkit.ScreenCaptureLib;

int failures = 0;

void Step(string name, Func<string> action)
{
    try
    {
        Console.WriteLine($"OK    {name}: {action()}");
    }
    catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or System.Runtime.InteropServices.MarshalDirectiveException or BadImageFormatException)
    {
        Console.WriteLine($"FAIL  {name}: {e.GetType().Name}: {e.Message}");
        failures++;
    }
    catch (Exception e)
    {
        Console.WriteLine($"INFO  {name}: {e.GetType().Name}: {e.Message}");
    }
}

Console.WriteLine($"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription} ({System.Runtime.InteropServices.RuntimeInformation.OSArchitecture})");

if (OperatingSystem.IsMacOS())
{
    MacDesktopPlatform platform = new(() => 1);
    DesktopPlatform.Current = platform;
    ScreenCapturer.Current = new MacScreenCapturer(platform);
    Step("screen recording permission", () => MacDesktopPlatform.HasScreenCaptureAccess.ToString());

    using MacHotkeyHost hotkeys = new();
    Step("carbon hotkey handler", () => { hotkeys.Initialize(); return "installed"; });
    Step("carbon hotkey register", () =>
    {
        HotkeyInfo info = new(InputKey.F12 | InputKey.Control | InputKey.Shift);
        hotkeys.RegisterHotkey(info);
        string status = info.Status.ToString();
        hotkeys.UnregisterHotkey(info);
        return status;
    });
}

IDesktopPlatform desktop = DesktopPlatform.Current;
Step("cursor position", () => desktop.GetCursorPosition().ToString());
Step("visible windows", () => desktop.GetVisibleWindows().Count.ToString());
Step("foreground window", () => desktop.GetForegroundWindow().ToString());
Step("capture 200x100", () =>
{
    using SkiaSharp.SKBitmap? bitmap = ScreenCapturer.Current.CaptureRectangle(new System.Drawing.Rectangle(0, 0, 200, 100), false, false);
    return bitmap == null ? "null" : $"{bitmap.Width}x{bitmap.Height}";
});

Console.WriteLine(failures == 0 ? "PLATFORM SMOKE PASSED" : $"PLATFORM SMOKE FAILED ({failures})");
return failures;
