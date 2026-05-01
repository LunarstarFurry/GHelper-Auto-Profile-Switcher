using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace GHelperAutoProfileSwitcher
{
    public static class GHelperHotkeys
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const int KEYEVENTF_KEYUP = 0x0002;

        private const byte VK_CONTROL = 0x11;
        private const byte VK_SHIFT = 0x10;
        private const byte VK_MENU = 0x12;

        private const byte VK_F16 = 0x7F;
        private const byte VK_F17 = 0x80;
        private const byte VK_F18 = 0x81;

        public static void SetMode(TargetMode mode)
        {
            byte fKey = VK_F17;
            switch (mode)
            {
                case TargetMode.Silent: fKey = VK_F16; break;
                case TargetMode.Balanced: fKey = VK_F17; break;
                case TargetMode.Turbo: fKey = VK_F18; break;
            }

            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, 0, UIntPtr.Zero);
            keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);

            keybd_event(fKey, 0, 0, UIntPtr.Zero);
            
            Thread.Sleep(50);

            keybd_event(fKey, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            
            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}