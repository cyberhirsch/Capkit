using System.Diagnostics;
using System.Runtime.InteropServices;

static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CkFrame { public IntPtr Data; public nuint Len; public int Width, Height, Stride; }

    [DllImport("capkit_capture")] [return: MarshalAs(UnmanagedType.U1)] public static extern bool ck_is_supported();
    [DllImport("capkit_capture")] [return: MarshalAs(UnmanagedType.U1)] public static extern bool ck_has_permission();
    [DllImport("capkit_capture")] public static extern int ck_capture_main_display(double x, double y, double w, double h, [MarshalAs(UnmanagedType.U1)] bool cursor, out CkFrame frame);
    [DllImport("capkit_capture")] public static extern void ck_free_frame(ref CkFrame frame);

    [DllImport("user32")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32")] public static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("user32")] public static extern int GetSystemMetrics(int i);
    [DllImport("gdi32")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32")] public static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int w, int h);
    [DllImport("gdi32")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32")] public static extern bool BitBlt(IntPtr d, int x, int y, int w, int h, IntPtr s, int sx, int sy, uint rop);
    [DllImport("gdi32")] public static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32")] public static extern bool DeleteDC(IntPtr dc);
}

static class Program
{
    static int Main()
    {
        Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); // per-monitor v2, like the app
        Console.WriteLine($"supported={Native.ck_is_supported()} permission={Native.ck_has_permission()}");
        Console.WriteLine($"primary screen {Native.GetSystemMetrics(0)}x{Native.GetSystemMetrics(1)}");

        Run("full display", 0, 0, 0, 0, "scap_full.bmp");
        Run("region 100,100 800x600", 100, 100, 800, 600, "scap_region.bmp");
        Run("odd region 101,51 333x221", 101, 51, 333, 221, "scap_odd.bmp");

        var times = new List<double>();
        for (int i = 0; i < 5; i++) times.Add(Time(() => Capture(100, 100, 800, 600, null)));
        Console.WriteLine($"scap region x5 warm: {string.Join(", ", times.Select(t => $"{t:0}ms"))}");

        var gdi = new List<double>();
        for (int i = 0; i < 5; i++) gdi.Add(Time(() => Gdi(100, 100, 800, 600)));
        Console.WriteLine($"GDI BitBlt region x5:  {string.Join(", ", gdi.Select(t => $"{t:0.0}ms"))}");
        return 0;
    }

    static double Time(Action a) { var sw = Stopwatch.StartNew(); a(); return sw.Elapsed.TotalMilliseconds; }

    static void Run(string name, double x, double y, double w, double h, string file)
    {
        var sw = Stopwatch.StartNew();
        var (ok, info) = Capture(x, y, w, h, file);
        Console.WriteLine($"{name}: {info} in {sw.Elapsed.TotalMilliseconds:0}ms");
    }

    static (bool, string) Capture(double x, double y, double w, double h, string? file)
    {
        int rc = Native.ck_capture_main_display(x, y, w, h, false, out var f);
        if (rc != 0) return (false, $"error {rc}");
        try
        {
            if (file != null) WriteBmp(file, f);
            return (true, $"{f.Width}x{f.Height} ({(ulong)f.Len} bytes){(file != null ? " -> " + file : "")}");
        }
        finally { Native.ck_free_frame(ref f); }
    }

    static void Gdi(int x, int y, int w, int h)
    {
        IntPtr screen = Native.GetDC(IntPtr.Zero), mem = Native.CreateCompatibleDC(screen), bmp = Native.CreateCompatibleBitmap(screen, w, h);
        IntPtr old = Native.SelectObject(mem, bmp);
        Native.BitBlt(mem, 0, 0, w, h, screen, x, y, 0x00CC0020 | 0x40000000);
        Native.SelectObject(mem, old); Native.DeleteObject(bmp); Native.DeleteDC(mem); Native.ReleaseDC(IntPtr.Zero, screen);
    }

    static unsafe void WriteBmp(string path, Native.CkFrame f)
    {
        int rowBytes = f.Width * 4, size = rowBytes * f.Height;
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        bw.Write((ushort)0x4D42); bw.Write(54 + size); bw.Write(0); bw.Write(54);
        bw.Write(40); bw.Write(f.Width); bw.Write(-f.Height); bw.Write((ushort)1); bw.Write((ushort)32);
        bw.Write(0); bw.Write(size); bw.Write(2835); bw.Write(2835); bw.Write(0); bw.Write(0);
        bw.Write(new ReadOnlySpan<byte>((byte*)f.Data, Math.Min(size, (int)f.Len)));
    }
}
