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

using Capkit.AvaloniaUI.Integration;
using Capkit.HelpersLib;
using Capkit.ScreenCaptureLib;
using System;

namespace Capkit;

/// <summary>Chooses the operating system implementations of the capture, desktop and hotkey services.</summary>
internal static class PlatformRegistration
{
    /// <summary>Call once Avalonia runs; the macOS display scale comes from its screen list.</summary>
    public static void Initialize()
    {
        if (OperatingSystem.IsMacOS())
        {
            MacDesktopPlatform platform = new(() => DesktopServices.Run(() => DesktopServices.GetWindow().Screens.Primary?.Scaling ?? 1));
            DesktopPlatform.Current = platform;
            ScreenCapturer.Current = new MacScreenCapturer(platform);

            if (!MacDesktopPlatform.HasScreenCaptureAccess)
            {
                DebugHelper.WriteLine("Screen Recording permission is missing; asking macOS to show its prompt.");
                MacDesktopPlatform.RequestScreenCaptureAccess();
            }
        }

        DebugHelper.WriteLine($"Desktop platform: {DesktopPlatform.Current.GetType().Name}, capturer: {ScreenCapturer.Current.GetType().Name}");
    }

    public static IHotkeyHost CreateHotkeyHost()
    {
        if (OperatingSystem.IsMacOS())
        {
            return new MacHotkeyHost();
        }

        return new WindowsHotkeyHost();
    }
}
