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

using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Capkit.HelpersLib;

/// <summary>
/// Global hotkeys on macOS through Carbon's RegisterEventHotKey, which still works for sandbox-free apps and needs no
/// Accessibility permission. Hotkeys are stored as Windows virtual-key codes, so they are translated to macOS key codes;
/// keys without a Mac equivalent (Print Screen) report <see cref="HotkeyStatus.Failed"/>. The Windows key maps to Command.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacHotkeyHost : IHotkeyHost
{
    private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";
    private const uint Signature = 0x43504B54; // 'CPKT'
    private const uint kEventClassKeyboard = 0x6B657962; // 'keyb'
    private const uint kEventHotKeyPressed = 5;
    private const uint kEventParamDirectObject = 0x2D2D2D2D; // '----'
    private const uint typeEventHotKeyID = 0x686B6964; // 'hkid'
    private const uint cmdKey = 0x0100, shiftKey = 0x0200, optionKey = 0x0800, controlKey = 0x1000;

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTypeSpec
    {
        public uint EventClass;
        public uint EventKind;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventHotKeyID
    {
        public uint Signature;
        public uint Id;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EventHandlerProc(IntPtr callRef, IntPtr eventRef, IntPtr userData);

    [DllImport(Carbon)] private static extern IntPtr GetApplicationEventTarget();
    [DllImport(Carbon)] private static extern int InstallEventHandler(IntPtr target, EventHandlerProc handler, uint numTypes, EventTypeSpec[] list, IntPtr userData, out IntPtr handlerRef);
    [DllImport(Carbon)] private static extern int RemoveEventHandler(IntPtr handlerRef);
    [DllImport(Carbon)] private static extern int RegisterEventHotKey(uint keyCode, uint modifiers, EventHotKeyID id, IntPtr target, uint options, out IntPtr hotKeyRef);
    [DllImport(Carbon)] private static extern int UnregisterEventHotKey(IntPtr hotKeyRef);
    [DllImport(Carbon)] private static extern int GetEventParameter(IntPtr eventRef, uint name, uint desiredType, IntPtr actualType, nuint bufferSize, IntPtr actualSize, out EventHotKeyID data);

    private readonly Dictionary<ushort, (HotkeyInfo Info, IntPtr Ref)> registered = new();
    private readonly Stopwatch repeatLimitTimer = Stopwatch.StartNew();
    private EventHandlerProc? handler; // Kept in a field so the GC does not collect the callback Carbon holds.
    private IntPtr handlerRef;
    private ushort nextId = 1;

    public event HotkeyEventHandler? HotkeyPress;
    public event EventHandler? Closed;
    public event EventHandler<NativeWindowMessageEventArgs>? NativeMessageReceived { add { } remove { } }

    public IntPtr Handle => IntPtr.Zero;
    public bool IsDisposed { get; private set; }
    public int HotkeyRepeatLimit { get; set; } = 1000;

    public void Initialize()
    {
        if (handler != null)
        {
            return;
        }

        handler = OnHotkeyEvent;
        EventTypeSpec[] types = { new() { EventClass = kEventClassKeyboard, EventKind = kEventHotKeyPressed } };
        int status = InstallEventHandler(GetApplicationEventTarget(), handler, 1, types, IntPtr.Zero, out handlerRef);

        if (status != 0)
        {
            DebugHelper.WriteLine($"Installing the Carbon hotkey handler failed: {status}");
        }
    }

    public void RegisterHotkey(HotkeyInfo hotkeyInfo)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (hotkeyInfo == null || hotkeyInfo.Status == HotkeyStatus.Registered) return;

        if (!hotkeyInfo.IsValidHotkey)
        {
            hotkeyInfo.Status = HotkeyStatus.NotConfigured;
            return;
        }

        Initialize();

        if (!MacKeyCodes.TryGetValue(hotkeyInfo.KeyCode, out uint keyCode))
        {
            DebugHelper.WriteLine("No macOS key for hotkey: " + hotkeyInfo);
            hotkeyInfo.Status = HotkeyStatus.Failed;
            return;
        }

        if (hotkeyInfo.ID == 0)
        {
            hotkeyInfo.ID = nextId++;
        }

        int status = RegisterEventHotKey(keyCode, ToCarbonModifiers(hotkeyInfo.ModifiersEnum),
            new EventHotKeyID { Signature = Signature, Id = hotkeyInfo.ID }, GetApplicationEventTarget(), 0, out IntPtr hotKeyRef);

        if (status != 0)
        {
            DebugHelper.WriteLine($"Unable to register hotkey ({status}): {hotkeyInfo}");
            hotkeyInfo.ID = 0;
            hotkeyInfo.Status = HotkeyStatus.Failed;
            return;
        }

        registered[hotkeyInfo.ID] = (hotkeyInfo, hotKeyRef);
        hotkeyInfo.Status = HotkeyStatus.Registered;
    }

    public bool UnregisterHotkey(HotkeyInfo hotkeyInfo)
    {
        if (hotkeyInfo == null) return false;

        if (hotkeyInfo.ID > 0 && registered.Remove(hotkeyInfo.ID, out var entry) && UnregisterEventHotKey(entry.Ref) == 0)
        {
            hotkeyInfo.ID = 0;
            hotkeyInfo.Status = HotkeyStatus.NotConfigured;
            return true;
        }

        hotkeyInfo.Status = HotkeyStatus.Failed;
        return false;
    }

    private int OnHotkeyEvent(IntPtr callRef, IntPtr eventRef, IntPtr userData)
    {
        try
        {
            if (GetEventParameter(eventRef, kEventParamDirectObject, typeEventHotKeyID, IntPtr.Zero,
                    (nuint)Marshal.SizeOf<EventHotKeyID>(), IntPtr.Zero, out EventHotKeyID id) == 0 &&
                id.Signature == Signature && registered.TryGetValue((ushort)id.Id, out var entry) && CheckRepeatLimitTime())
            {
                HotkeyInfo info = entry.Info;
                Dispatcher.UIThread.Post(() => HotkeyPress?.Invoke(info.ID, info.KeyCode, info.ModifiersEnum));
            }
        }
        catch (Exception e)
        {
            // Never let a managed exception unwind into Carbon.
            DebugHelper.WriteException(e);
        }

        return 0;
    }

    private bool CheckRepeatLimitTime()
    {
        if (HotkeyRepeatLimit > 0)
        {
            if (repeatLimitTimer.ElapsedMilliseconds < HotkeyRepeatLimit) return false;
            repeatLimitTimer.Restart();
        }

        return true;
    }

    private static uint ToCarbonModifiers(Modifiers modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(Modifiers.Win)) result |= cmdKey;
        if (modifiers.HasFlag(Modifiers.Control)) result |= controlKey;
        if (modifiers.HasFlag(Modifiers.Alt)) result |= optionKey;
        if (modifiers.HasFlag(Modifiers.Shift)) result |= shiftKey;
        return result;
    }

    public void Close()
    {
        if (IsDisposed) return;

        foreach ((HotkeyInfo info, IntPtr hotKeyRef) in registered.Values)
        {
            UnregisterEventHotKey(hotKeyRef);
            info.ID = 0;
            info.Status = HotkeyStatus.NotConfigured;
        }

        registered.Clear();

        if (handlerRef != IntPtr.Zero)
        {
            RemoveEventHandler(handlerRef);
            handlerRef = IntPtr.Zero;
        }

        IsDisposed = true;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Close();

    /// <summary>Windows virtual-key codes (as stored in hotkeys) to macOS ANSI key codes (Events.h kVK_*).</summary>
    private static readonly Dictionary<InputKey, uint> MacKeyCodes = new()
    {
        [InputKey.A] = 0x00, [InputKey.S] = 0x01, [InputKey.D] = 0x02, [InputKey.F] = 0x03, [InputKey.H] = 0x04,
        [InputKey.G] = 0x05, [InputKey.Z] = 0x06, [InputKey.X] = 0x07, [InputKey.C] = 0x08, [InputKey.V] = 0x09,
        [InputKey.B] = 0x0B, [InputKey.Q] = 0x0C, [InputKey.W] = 0x0D, [InputKey.E] = 0x0E, [InputKey.R] = 0x0F,
        [InputKey.Y] = 0x10, [InputKey.T] = 0x11, [InputKey.O] = 0x1F, [InputKey.U] = 0x20, [InputKey.I] = 0x22,
        [InputKey.P] = 0x23, [InputKey.L] = 0x25, [InputKey.J] = 0x26, [InputKey.K] = 0x28, [InputKey.N] = 0x2D,
        [InputKey.M] = 0x2E,
        [InputKey.D1] = 0x12, [InputKey.D2] = 0x13, [InputKey.D3] = 0x14, [InputKey.D4] = 0x15, [InputKey.D5] = 0x17,
        [InputKey.D6] = 0x16, [InputKey.D7] = 0x1A, [InputKey.D8] = 0x1C, [InputKey.D9] = 0x19, [InputKey.D0] = 0x1D,
        [InputKey.F1] = 0x7A, [InputKey.F2] = 0x78, [InputKey.F3] = 0x63, [InputKey.F4] = 0x76, [InputKey.F5] = 0x60,
        [InputKey.F6] = 0x61, [InputKey.F7] = 0x62, [InputKey.F8] = 0x64, [InputKey.F9] = 0x65, [InputKey.F10] = 0x6D,
        [InputKey.F11] = 0x67, [InputKey.F12] = 0x6F,
        [InputKey.Space] = 0x31, [InputKey.Return] = 0x24, [InputKey.Escape] = 0x35, [InputKey.Tab] = 0x30,
        [InputKey.Left] = 0x7B, [InputKey.Right] = 0x7C, [InputKey.Down] = 0x7D, [InputKey.Up] = 0x7E
    };
}
