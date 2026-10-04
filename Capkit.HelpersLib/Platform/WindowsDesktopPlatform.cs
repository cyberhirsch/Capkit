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
using System.Drawing;
using System.Linq;
using System.Runtime.Versioning;

namespace Capkit.HelpersLib
{
    [SupportedOSPlatform("windows")]
    public sealed class WindowsDesktopPlatform : IDesktopPlatform
    {
        private static readonly string[] ignoredWindowClasses = { "Progman", "Button" };

        public Point GetCursorPosition()
        {
            return NativeMethods.GetCursorPos(out POINT point) ? (Point)point : Point.Empty;
        }

        public void SetCursorPosition(Point position)
        {
            NativeMethods.SetCursorPos(position.X, position.Y);
        }

        public Color GetPixelColor(Point position)
        {
            IntPtr hdc = NativeMethods.GetDC(IntPtr.Zero);
            uint pixel = NativeMethods.GetPixel(hdc, position.X, position.Y);
            NativeMethods.ReleaseDC(IntPtr.Zero, hdc);
            return Color.FromArgb((int)(pixel & 0x000000FF), (int)(pixel & 0x0000FF00) >> 8, (int)(pixel & 0x00FF0000) >> 16);
        }

        public IntPtr GetForegroundWindow() => NativeMethods.GetForegroundWindow();

        public Rectangle GetWindowRectangle(IntPtr window)
        {
            Rectangle rect = Rectangle.Empty;

            if (NativeMethods.IsDWMEnabled() && NativeMethods.GetExtendedFrameBounds(window, out Rectangle tempRect))
            {
                rect = tempRect;
            }

            if (rect.IsEmpty)
            {
                rect = NativeMethods.GetWindowRect(window);
            }

            if (!Helpers.IsWindows10OrGreater() && NativeMethods.IsZoomed(window))
            {
                rect = NativeMethods.MaximizedWindowFix(window, rect);
            }

            return rect;
        }

        public Rectangle GetWindowClientRectangle(IntPtr window) => NativeMethods.GetClientRect(window);

        public IReadOnlyList<DesktopWindow> GetVisibleWindows()
        {
            List<IntPtr> handles = new();
            NativeMethods.EnumWindows((handle, _) =>
            {
                handles.Add(handle);
                return true;
            }, IntPtr.Zero);

            return handles
                .Select(handle => new WindowInfo(handle))
                .Where(window => window.IsVisible && !window.IsCloaked && !string.IsNullOrEmpty(window.Text) &&
                    !ignoredWindowClasses.Contains(window.ClassName, StringComparer.OrdinalIgnoreCase) && window.Rectangle.IsValid())
                .Select(window => new DesktopWindow(window.Handle, window.Text, window.Rectangle, window.ProcessName ?? string.Empty))
                .ToList();
        }
    }
}
