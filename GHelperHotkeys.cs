using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GHelperAutoProfileSwitcher
{
    public static class GHelperHotkeys
    {
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const uint MAPVK_VK_TO_VSC = 0;

        private const ushort VK_SHIFT = 0x10;
        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_MENU = 0x12;

        private const ushort VK_LSHIFT = 0xA0;
        private const ushort VK_RSHIFT = 0xA1;
        private const ushort VK_LCONTROL = 0xA2;
        private const ushort VK_RCONTROL = 0xA3;
        private const ushort VK_LMENU = 0xA4;
        private const ushort VK_RMENU = 0xA5;

        private const ushort VK_F16 = 0x7F;
        private const ushort VK_F17 = 0x80;
        private const ushort VK_F18 = 0x81;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private static INPUT CreateKeyInput(ushort vk, bool keyUp)
        {
            ushort scan = (ushort)MapVirtualKey(vk, MAPVK_VK_TO_VSC);
            uint flags = keyUp ? KEYEVENTF_KEYUP : 0;
            if (scan != 0)
            {
                flags |= KEYEVENTF_SCANCODE;
            }

            return new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = scan,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
        }

        private static bool SendKeyInputs(params INPUT[] inputs)
        {
            if (inputs.Length == 0) return true;
            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            return sent == inputs.Length;
        }

        public static void ClearPhysicalModifiers()
        {
            ushort[] modifiers =
            {
                VK_LCONTROL, VK_RCONTROL, VK_CONTROL,
                VK_LSHIFT, VK_RSHIFT, VK_SHIFT,
                VK_LMENU, VK_RMENU, VK_MENU
            };

            var releaseInputs = new List<INPUT>();
            foreach (ushort vk in modifiers)
            {
                if ((GetAsyncKeyState(vk) & 0x8000) != 0)
                {
                    releaseInputs.Add(CreateKeyInput(vk, keyUp: true));
                }
            }

            if (releaseInputs.Count > 0)
            {
                SendKeyInputs(releaseInputs.ToArray());
            }
        }

        public static async Task SetModeAsync(TargetMode mode)
        {
            ushort fKey = mode switch
            {
                TargetMode.Silent => VK_F16,
                TargetMode.Balanced => VK_F17,
                TargetMode.Turbo => VK_F18,
                _ => VK_F17
            };

            ClearPhysicalModifiers();

            bool downOk = SendKeyInputs(
                CreateKeyInput(VK_CONTROL, keyUp: false),
                CreateKeyInput(VK_SHIFT, keyUp: false),
                CreateKeyInput(VK_MENU, keyUp: false),
                CreateKeyInput(fKey, keyUp: false)
            );

            if (!downOk)
            {
                keybd_event((byte)VK_CONTROL, 0, 0, UIntPtr.Zero);
                keybd_event((byte)VK_SHIFT, 0, 0, UIntPtr.Zero);
                keybd_event((byte)VK_MENU, 0, 0, UIntPtr.Zero);
                keybd_event((byte)fKey, 0, 0, UIntPtr.Zero);
            }

            await Task.Delay(50);

            bool upOk = SendKeyInputs(
                CreateKeyInput(fKey, keyUp: true),
                CreateKeyInput(VK_MENU, keyUp: true),
                CreateKeyInput(VK_SHIFT, keyUp: true),
                CreateKeyInput(VK_CONTROL, keyUp: true)
            );

            if (!upOk)
            {
                keybd_event((byte)fKey, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event((byte)VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event((byte)VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event((byte)VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
        }
    }
}
