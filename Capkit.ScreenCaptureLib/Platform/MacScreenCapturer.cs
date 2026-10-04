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
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Capkit.ScreenCaptureLib
{
    /// <summary>ScreenCaptureKit capture through libcapkit_mac.dylib (native/macos). Needs macOS 14 and Screen Recording permission.</summary>
    [SupportedOSPlatform("macos")]
    public sealed class MacScreenCapturer : IScreenCapturer
    {
        private const string Library = "capkit_mac";

        private readonly MacDesktopPlatform platform;

        public MacScreenCapturer(MacDesktopPlatform platform)
        {
            this.platform = platform;
            platform.PixelReader = ReadPixel;
        }

        public bool IsPermissionMissing => !MacDesktopPlatform.HasScreenCaptureAccess;

        public SKBitmap? CaptureRectangle(Rectangle rectangle, bool captureCursor, bool hdrColorCorrection)
        {
            if (rectangle.Width <= 0 || rectangle.Height <= 0)
            {
                return null;
            }

            double scale = platform.PixelsPerPoint;
            int result = ck_mac_capture(rectangle.X / scale, rectangle.Y / scale, rectangle.Width / scale, rectangle.Height / scale,
                scale, captureCursor, out IntPtr data, out int width, out int height, out int stride);

            if (result != 0)
            {
                throw new InvalidOperationException(DescribeError(result));
            }

            try
            {
                SKBitmap bitmap = new(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
                IntPtr pixels = bitmap.GetPixels();
                int rowBytes = bitmap.RowBytes;
                byte[] row = new byte[Math.Min(rowBytes, stride)];

                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data, y * stride), row, 0, row.Length);
                    Marshal.Copy(row, 0, IntPtr.Add(pixels, y * rowBytes), row.Length);
                }

                return bitmap;
            }
            finally
            {
                ck_mac_free(data);
            }
        }

        private Color ReadPixel(Point position)
        {
            try
            {
                using SKBitmap? bitmap = CaptureRectangle(new Rectangle(position, new Size(1, 1)), false, false);
                if (bitmap == null)
                {
                    return Color.Empty;
                }

                SKColor color = bitmap.GetPixel(0, 0);
                return Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e, "Reading a screen pixel failed.");
                return Color.Empty;
            }
        }

        private static string DescribeError(int code) => code switch
        {
            1 => "Screen capture needs macOS 14 or later.",
            2 => "Capkit needs the Screen Recording permission (System Settings > Privacy & Security > Screen Recording).",
            3 => "ScreenCaptureKit did not answer within 10 seconds.",
            5 => "No display was found for the capture region.",
            _ => $"ScreenCaptureKit capture failed (code {code})."
        };

        [DllImport(Library)]
        private static extern int ck_mac_capture(double x, double y, double width, double height, double scale,
            [MarshalAs(UnmanagedType.U1)] bool showCursor, out IntPtr data, out int outWidth, out int outHeight, out int stride);

        [DllImport(Library)]
        private static extern void ck_mac_free(IntPtr data);
    }
}
