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
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Capkit.HelpersLib
{
    /// <summary>CoreFoundation and CoreGraphics C APIs used by the macOS platform layer.</summary>
    [SupportedOSPlatform("macos")]
    internal static class MacInterop
    {
        private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

        private const uint kCFStringEncodingUTF8 = 0x08000100;
        private const int kCFNumberSInt64Type = 4;

        public const uint kCGWindowListOptionOnScreenOnly = 1 << 0;
        public const uint kCGWindowListExcludeDesktopElements = 1 << 4;
        public const uint kCGNullWindowID = 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct CGPoint
        {
            public double X;
            public double Y;

            public CGPoint(double x, double y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CGRect
        {
            public double X;
            public double Y;
            public double Width;
            public double Height;
        }

        [DllImport(CoreFoundation)] public static extern void CFRelease(IntPtr cf);
        [DllImport(CoreFoundation)] private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string value, uint encoding);
        [DllImport(CoreFoundation)] private static extern long CFStringGetLength(IntPtr value);
        [DllImport(CoreFoundation)] private static extern long CFStringGetMaximumSizeForEncoding(long length, uint encoding);
        [DllImport(CoreFoundation)] [return: MarshalAs(UnmanagedType.U1)] private static extern bool CFStringGetCString(IntPtr value, byte[] buffer, long bufferSize, uint encoding);
        [DllImport(CoreFoundation)] public static extern long CFArrayGetCount(IntPtr array);
        [DllImport(CoreFoundation)] public static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, long index);
        [DllImport(CoreFoundation)] private static extern IntPtr CFDictionaryGetValue(IntPtr dictionary, IntPtr key);
        [DllImport(CoreFoundation)] [return: MarshalAs(UnmanagedType.U1)] private static extern bool CFNumberGetValue(IntPtr number, int type, out long value);

        [DllImport(CoreGraphics)] public static extern IntPtr CGEventCreate(IntPtr source);
        [DllImport(CoreGraphics)] public static extern CGPoint CGEventGetLocation(IntPtr evt);
        [DllImport(CoreGraphics)] public static extern int CGWarpMouseCursorPosition(CGPoint point);
        [DllImport(CoreGraphics)] public static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);
        [DllImport(CoreGraphics)] [return: MarshalAs(UnmanagedType.U1)] private static extern bool CGRectMakeWithDictionaryRepresentation(IntPtr dictionary, out CGRect rect);
        [DllImport(CoreGraphics)] [return: MarshalAs(UnmanagedType.U1)] public static extern bool CGPreflightScreenCaptureAccess();
        [DllImport(CoreGraphics)] [return: MarshalAs(UnmanagedType.U1)] public static extern bool CGRequestScreenCaptureAccess();

        public static string? GetString(IntPtr dictionary, string key)
        {
            IntPtr value = GetValue(dictionary, key);
            if (value == IntPtr.Zero)
            {
                return null;
            }

            long size = CFStringGetMaximumSizeForEncoding(CFStringGetLength(value), kCFStringEncodingUTF8) + 1;
            byte[] buffer = new byte[size];
            if (!CFStringGetCString(value, buffer, size, kCFStringEncodingUTF8))
            {
                return null;
            }

            int length = Array.IndexOf(buffer, (byte)0);
            return Encoding.UTF8.GetString(buffer, 0, length < 0 ? buffer.Length : length);
        }

        public static long GetNumber(IntPtr dictionary, string key)
        {
            IntPtr value = GetValue(dictionary, key);
            return value != IntPtr.Zero && CFNumberGetValue(value, kCFNumberSInt64Type, out long number) ? number : 0;
        }

        public static CGRect GetRect(IntPtr dictionary, string key)
        {
            IntPtr value = GetValue(dictionary, key);
            return value != IntPtr.Zero && CGRectMakeWithDictionaryRepresentation(value, out CGRect rect) ? rect : default;
        }

        private static IntPtr GetValue(IntPtr dictionary, string key)
        {
            IntPtr cfKey = CFStringCreateWithCString(IntPtr.Zero, key, kCFStringEncodingUTF8);
            try
            {
                return CFDictionaryGetValue(dictionary, cfKey);
            }
            finally
            {
                CFRelease(cfKey);
            }
        }
    }
}
