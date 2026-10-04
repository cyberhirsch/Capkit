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

using Capkit.HelpersLib;
using System;
using System.Drawing;
using Bitmap = SkiaSharp.SKBitmap;

namespace Capkit.ScreenCaptureLib
{
    public partial class Screenshot
    {
        public bool CaptureCursor { get; set; } = false;
        public bool CaptureClientArea { get; set; } = false;
        public bool RemoveOutsideScreenArea { get; set; } = true;
        public bool CaptureShadow { get; set; } = false;
        public int ShadowOffset { get; set; } = 20;
        public bool AutoHideTaskbar { get; set; } = false;
        public bool HDRScreenshotColorCorrection { get; set; } = false;

        public Bitmap CaptureRectangle(Rectangle rect)
        {
            if (RemoveOutsideScreenArea)
            {
                Rectangle bounds = CaptureHelpers.GetScreenBounds();
                rect = Rectangle.Intersect(bounds, rect);
            }

            return CaptureRectangleNative(rect, CaptureCursor);
        }

        public Bitmap CaptureFullscreen()
        {
            Rectangle bounds = CaptureHelpers.GetScreenBounds();

            return CaptureRectangle(bounds);
        }

        public Bitmap CaptureWindow(IntPtr handle)
        {
            if (handle.ToInt64() > 0)
            {
                Rectangle rect = CaptureClientArea
                    ? DesktopPlatform.Current.GetWindowClientRectangle(handle)
                    : CaptureHelpers.GetWindowRectangle(handle);

                bool isTaskbarHide = false;

                try
                {
                    if (AutoHideTaskbar && OperatingSystem.IsWindows())
                    {
                        isTaskbarHide = NativeMethods.SetTaskbarVisibilityIfIntersect(false, rect);
                    }

                    return CaptureRectangle(rect);
                }
                finally
                {
                    if (isTaskbarHide)
                    {
                        NativeMethods.SetTaskbarVisibility(true);
                    }
                }
            }

            return null;
        }

        public Bitmap CaptureActiveWindow()
        {
            IntPtr handle = DesktopPlatform.Current.GetForegroundWindow();

            return CaptureWindow(handle);
        }

        public Bitmap CaptureActiveMonitor()
        {
            Rectangle bounds = CaptureHelpers.GetActiveScreenBounds();

            return CaptureRectangle(bounds);
        }

        private Bitmap CaptureRectangleNative(Rectangle rect, bool captureCursor = false)
        {
            return ScreenCapturer.Current.CaptureRectangle(rect, captureCursor, HDRScreenshotColorCorrection);
        }
    }
}
