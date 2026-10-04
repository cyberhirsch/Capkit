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

namespace Capkit.HelpersLib
{
    /// <summary>A top-level window as the capture code sees it. <see cref="Id"/> is an HWND on Windows and a CGWindowID on macOS.</summary>
    public sealed record DesktopWindow(IntPtr Id, string Title, Rectangle Bounds, string ProcessName);

    /// <summary>
    /// Desktop queries the capture code needs from the operating system, in physical pixels on the virtual desktop
    /// (the same coordinate space as screenshots). Screens themselves come from <see cref="DesktopScreen"/>.
    /// </summary>
    public interface IDesktopPlatform
    {
        Point GetCursorPosition();

        void SetCursorPosition(Point position);

        Color GetPixelColor(Point position);

        IntPtr GetForegroundWindow();

        /// <summary>The visible bounds of a window, without the invisible resize border where the platform has one.</summary>
        Rectangle GetWindowRectangle(IntPtr window);

        Rectangle GetWindowClientRectangle(IntPtr window);

        /// <summary>Visible top-level windows with a title, front to back.</summary>
        IReadOnlyList<DesktopWindow> GetVisibleWindows();
    }

    public static class DesktopPlatform
    {
        private static IDesktopPlatform? current;

        /// <summary>The platform implementation. Defaults to Windows; other platforms register theirs at startup.</summary>
        public static IDesktopPlatform Current
        {
            get => current ??= OperatingSystem.IsWindows() ? new WindowsDesktopPlatform() : new UnsupportedDesktopPlatform();
            set => current = value;
        }
    }

    /// <summary>Answers neutrally so the UI still starts on a platform whose implementation is not registered yet.</summary>
    public sealed class UnsupportedDesktopPlatform : IDesktopPlatform
    {
        public Point GetCursorPosition() => Point.Empty;

        public void SetCursorPosition(Point position)
        {
        }

        public Color GetPixelColor(Point position) => Color.Empty;

        public IntPtr GetForegroundWindow() => IntPtr.Zero;

        public Rectangle GetWindowRectangle(IntPtr window) => Rectangle.Empty;

        public Rectangle GetWindowClientRectangle(IntPtr window) => Rectangle.Empty;

        public IReadOnlyList<DesktopWindow> GetVisibleWindows() => Array.Empty<DesktopWindow>();
    }
}
