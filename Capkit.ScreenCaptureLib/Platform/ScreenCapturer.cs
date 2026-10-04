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

using Capkit.HelpersLib;
using SkiaSharp;
using System;
using System.Drawing;
using System.Runtime.Versioning;

namespace Capkit.ScreenCaptureLib
{
    /// <summary>Grabs pixels from the screen. Rectangles are physical pixels on the virtual desktop.</summary>
    public interface IScreenCapturer
    {
        /// <summary>True when capturing needs a permission the user has not granted (macOS Screen Recording).</summary>
        bool IsPermissionMissing { get; }

        SKBitmap? CaptureRectangle(Rectangle rectangle, bool captureCursor, bool hdrColorCorrection);
    }

    public static class ScreenCapturer
    {
        private static IScreenCapturer? current;

        /// <summary>The platform implementation. Defaults to Windows; other platforms register theirs at startup.</summary>
        public static IScreenCapturer Current
        {
            get => current ??= OperatingSystem.IsWindows() ? new WindowsScreenCapturer() : new UnsupportedScreenCapturer();
            set => current = value;
        }
    }

    public sealed class UnsupportedScreenCapturer : IScreenCapturer
    {
        public bool IsPermissionMissing => false;

        public SKBitmap? CaptureRectangle(Rectangle rectangle, bool captureCursor, bool hdrColorCorrection) =>
            throw new PlatformNotSupportedException("Screen capture is not implemented on this platform yet.");
    }

    /// <summary>GDI BitBlt of the desktop window, with optional HDR tone mapping and the cursor drawn on top.</summary>
    [SupportedOSPlatform("windows")]
    public sealed class WindowsScreenCapturer : IScreenCapturer
    {
        public bool IsPermissionMissing => false;

        public SKBitmap? CaptureRectangle(Rectangle rectangle, bool captureCursor, bool hdrColorCorrection) =>
            CaptureWindowRectangle(NativeMethods.GetDesktopWindow(), rectangle, captureCursor, hdrColorCorrection);

        internal static SKBitmap? CaptureWindowRectangle(IntPtr handle, Rectangle rect, bool captureCursor, bool hdrColorCorrection)
        {
            if (rect.Width == 0 || rect.Height == 0)
            {
                return null;
            }

            if (hdrColorCorrection)
            {
                SKBitmap bitmap = CaptureGDI(handle, rect, false);

                try
                {
                    HDRScreenCapture.ApplyColorCorrection(bitmap, rect);
                }
                catch (Exception e)
                {
                    DebugHelper.WriteException(e, "HDR screenshot color correction failed.");
                }

                if (captureCursor)
                {
                    try
                    {
                        CursorData cursorData = new CursorData();
                        cursorData.DrawCursor(bitmap, rect.Location);
                    }
                    catch (Exception e)
                    {
                        DebugHelper.WriteException(e, "Cursor capture failed.");
                    }
                }

                return bitmap;
            }

            return CaptureGDI(handle, rect, captureCursor);
        }

        private static SKBitmap CaptureGDI(IntPtr handle, Rectangle rect, bool captureCursor)
        {
            return WindowsImageInterop.Capture(rect, captureCursor ? dc =>
            {
                try { new CursorData().DrawCursor(dc, rect.Location); }
                catch (Exception exception) { DebugHelper.WriteException(exception, "Cursor capture failed."); }
            }
            : null, handle);
        }
    }
}
