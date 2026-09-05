using System;
using System.Runtime.InteropServices;

namespace Whirlwind.Interop
{
    public static class Win32
    {
        // ---------------- ICON CONSTANTS ----------------

        public const int WM_SETICON = 0x0080;
        public const int ICON_SMALL = 0;
        public const int ICON_BIG = 1;

        // ---------------- SendMessage ----------------

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(
            IntPtr hWnd,
            int Msg,
            int wParam,
            IntPtr lParam);

        // ---------------- DestroyIcon ----------------

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool DestroyIcon(IntPtr hIcon);
    }
}
