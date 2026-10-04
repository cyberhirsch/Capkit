#region License Information (GPL v3)

/*
    Capkit - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;

namespace Capkit.HelpersLib
{
    /// <summary>
    /// macOS desktop queries through CoreGraphics. Quartz reports points with the origin at the top-left of the main
    /// display; the capture code works in pixels, so values are scaled by <see cref="PixelsPerPoint"/>.
    /// Known limit: with displays of different scale factors there is no single pixel grid, and this first version
    /// uses the main display's factor everywhere.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public sealed class MacDesktopPlatform : IDesktopPlatform
    {
        private readonly int ownProcessId = Environment.ProcessId;
        private readonly string ownProcessName = Process.GetCurrentProcess().ProcessName;

        /// <param name="pixelsPerPoint">Backing scale of the main display, e.g. 2 on Retina. Supplied by the UI layer.</param>
        public MacDesktopPlatform(Func<double> pixelsPerPoint)
        {
            PixelsPerPointProvider = pixelsPerPoint;
        }

        public Func<double> PixelsPerPointProvider { get; }

        public double PixelsPerPoint
        {
            get
            {
                double scale = PixelsPerPointProvider();
                return double.IsFinite(scale) && scale > 0 ? scale : 1;
            }
        }

        /// <summary>Whether the user granted Screen Recording; without it window titles and screen pixels are withheld.</summary>
        public static bool HasScreenCaptureAccess => MacInterop.CGPreflightScreenCaptureAccess();

        /// <summary>Shows the system prompt once; the grant takes effect after the app restarts.</summary>
        public static bool RequestScreenCaptureAccess() => MacInterop.CGRequestScreenCaptureAccess();

        public Point GetCursorPosition()
        {
            IntPtr evt = MacInterop.CGEventCreate(IntPtr.Zero);
            if (evt == IntPtr.Zero)
            {
                return Point.Empty;
            }

            try
            {
                MacInterop.CGPoint location = MacInterop.CGEventGetLocation(evt);
                return ToPixels(location.X, location.Y);
            }
            finally
            {
                MacInterop.CFRelease(evt);
            }
        }

        public void SetCursorPosition(Point position)
        {
            double scale = PixelsPerPoint;
            MacInterop.CGWarpMouseCursorPosition(new MacInterop.CGPoint(position.X / scale, position.Y / scale));
        }

        /// <summary>Read through the screen capturer, which owns the pixel access on macOS.</summary>
        public Func<Point, Color>? PixelReader { get; set; }

        public Color GetPixelColor(Point position) => PixelReader?.Invoke(position) ?? Color.Empty;

        public IntPtr GetForegroundWindow()
        {
            // The window list is ordered front to back; the first normal window of another app is the active one.
            foreach (DesktopWindow window in EnumerateWindows(includeUntitled: true))
            {
                return window.Id;
            }

            return IntPtr.Zero;
        }

        public Rectangle GetWindowRectangle(IntPtr window)
        {
            foreach (DesktopWindow candidate in EnumerateWindows(includeUntitled: true))
            {
                if (candidate.Id == window)
                {
                    return candidate.Bounds;
                }
            }

            return Rectangle.Empty;
        }

        // Quartz exposes no separate content rectangle for other apps' windows.
        public Rectangle GetWindowClientRectangle(IntPtr window) => GetWindowRectangle(window);

        public IReadOnlyList<DesktopWindow> GetVisibleWindows() => new List<DesktopWindow>(EnumerateWindows(includeUntitled: false));

        private IEnumerable<DesktopWindow> EnumerateWindows(bool includeUntitled)
        {
            List<DesktopWindow> windows = new();
            IntPtr list = MacInterop.CGWindowListCopyWindowInfo(
                MacInterop.kCGWindowListOptionOnScreenOnly | MacInterop.kCGWindowListExcludeDesktopElements, MacInterop.kCGNullWindowID);

            if (list == IntPtr.Zero)
            {
                return windows;
            }

            try
            {
                long count = MacInterop.CFArrayGetCount(list);

                for (long i = 0; i < count; i++)
                {
                    IntPtr info = MacInterop.CFArrayGetValueAtIndex(list, i);

                    // Layer 0 holds normal app windows; menus, the Dock and overlays live above it.
                    if (MacInterop.GetNumber(info, "kCGWindowLayer") != 0 ||
                        MacInterop.GetNumber(info, "kCGWindowOwnerPID") == ownProcessId)
                    {
                        continue;
                    }

                    string owner = MacInterop.GetString(info, "kCGWindowOwnerName") ?? string.Empty;
                    string title = MacInterop.GetString(info, "kCGWindowName") ?? string.Empty;

                    if (owner == ownProcessName || (!includeUntitled && title.Length == 0))
                    {
                        continue;
                    }

                    MacInterop.CGRect bounds = MacInterop.GetRect(info, "kCGWindowBounds");
                    if (bounds.Width < 1 || bounds.Height < 1)
                    {
                        continue;
                    }

                    Point location = ToPixels(bounds.X, bounds.Y);
                    Point corner = ToPixels(bounds.X + bounds.Width, bounds.Y + bounds.Height);
                    IntPtr id = new IntPtr(MacInterop.GetNumber(info, "kCGWindowNumber"));
                    windows.Add(new DesktopWindow(id, title.Length > 0 ? title : owner,
                        Rectangle.FromLTRB(location.X, location.Y, corner.X, corner.Y), owner));
                }
            }
            finally
            {
                MacInterop.CFRelease(list);
            }

            return windows;
        }

        private Point ToPixels(double x, double y)
        {
            double scale = PixelsPerPoint;
            return new Point((int)Math.Round(x * scale), (int)Math.Round(y * scale));
        }
    }
}
