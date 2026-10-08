using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GHelperAutoProfileSwitcher
{
    public static class GHelperHotkeys
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private const byte VK_SHIFT = 0x10;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_MENU = 0x12; // Alt key

        private const byte VK_F16 = 0x7F; // Silent
        private const byte VK_F17 = 0x80; // Balanced
        private const byte VK_F18 = 0x81; // Turbo

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        private static INPUT CreateKeyInput(ushort vk, bool keyUp)
        {
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = (ushort)MapVirtualKey(vk, 0),
                        dwFlags = keyUp ? KEYEVENTF_KEYUP : 0,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };
        }

        /// <summary>
        /// Clears any physically held modifier keys (e.g. Alt held from Alt+F4)
        /// to ensure clean edge-triggered hotkey reception.
        /// </summary>
        private static void ClearPhysicalModifiers()
        {
            try
            {
                if ((GetAsyncKeyState(VK_MENU) & 0x8000) != 0)
                {
                    keybd_event(VK_MENU, (byte)MapVirtualKey(VK_MENU, 0), KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
                if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0)
                {
                    keybd_event(VK_CONTROL, (byte)MapVirtualKey(VK_CONTROL, 0), KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
                if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0)
                {
                    keybd_event(VK_SHIFT, (byte)MapVirtualKey(VK_SHIFT, 0), KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
            }
            catch
            {
                // Fallback safe ignore
            }
        }

        public static async Task<bool> SetModeAsync(TargetMode mode)
        {
            byte fKey = mode switch
            {
                TargetMode.Silent => VK_F16,
                TargetMode.Balanced => VK_F17,
                TargetMode.Turbo => VK_F18,
                _ => VK_F17
            };

            ClearPhysicalModifiers();

            // Prepare atomic key-down batch for Ctrl + Shift + Alt + F-key
            INPUT[] downInputs = new INPUT[]
            {
                CreateKeyInput(VK_CONTROL, false),
                CreateKeyInput(VK_SHIFT, false),
                CreateKeyInput(VK_MENU, false),
                CreateKeyInput(fKey, false)
            };

            INPUT[] upInputs = new INPUT[]
            {
                CreateKeyInput(fKey, true),
                CreateKeyInput(VK_MENU, true),
                CreateKeyInput(VK_SHIFT, true),
                CreateKeyInput(VK_CONTROL, true)
            };

            int inputSize = Marshal.SizeOf(typeof(INPUT));
            uint sentDown = SendInput((uint)downInputs.Length, downInputs, inputSize);

            await Task.Delay(50).ConfigureAwait(false);

            uint sentUp = SendInput((uint)upInputs.Length, upInputs, inputSize);

            // If SendInput succeeded, return true
            if (sentDown == downInputs.Length && sentUp == upInputs.Length)
            {
                return true;
            }

            // Fallback to legacy keybd_event if SendInput was blocked or failed
            SendViaKeybdEvent(fKey);
            return false;
        }

        public static void SetMode(TargetMode mode)
        {
            SetModeAsync(mode).GetAwaiter().GetResult();
        }

        private static void SendViaKeybdEvent(byte fKey)
        {
            byte scanCtrl = (byte)MapVirtualKey(VK_CONTROL, 0);
            byte scanShift = (byte)MapVirtualKey(VK_SHIFT, 0);
            byte scanAlt = (byte)MapVirtualKey(VK_MENU, 0);
            byte scanF = (byte)MapVirtualKey(fKey, 0);

            keybd_event(VK_CONTROL, scanCtrl, 0, UIntPtr.Zero);
            keybd_event(VK_SHIFT, scanShift, 0, UIntPtr.Zero);
            keybd_event(VK_MENU, scanAlt, 0, UIntPtr.Zero);
            keybd_event(fKey, scanF, 0, UIntPtr.Zero);

            System.Threading.Thread.Sleep(50);

            keybd_event(fKey, scanF, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_MENU, scanAlt, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_SHIFT, scanShift, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, scanCtrl, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}