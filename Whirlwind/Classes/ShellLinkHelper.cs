using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

public static class ShellLinkHelper
{
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPath, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetShowCmd(int iShowCmd);
        void SetHotkey(short wHotkey);
        void GetPath([MarshalAs(UnmanagedType.LPWStr)] out string pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
        void GetDescription([MarshalAs(UnmanagedType.LPWStr)] out string pszName, int cchMaxName);
        void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] out string pszDir, int cchMaxPath);
        void GetArguments([MarshalAs(UnmanagedType.LPWStr)] out string pszArgs, int cchMaxPath);
        void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] out string pszIconPath, out int iIcon);
        void GetShowCmd(out int piShowCmd);
        void GetHotkey(out short pwHotkey);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        uint GetCount(out uint cProps);
        uint GetAt(uint iProp, out PROPERTYKEY pkey);
        uint GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        uint SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        uint Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr p;
        public int p2;

        public static PROPVARIANT FromString(string value)
        {
            var pv = new PROPVARIANT();
            pv.vt = 31; // VT_LPWSTR
            pv.p = Marshal.StringToCoTaskMemUni(value);
            return pv;
        }
    }

    public static void CreateShortcut(string exePath, string shortcutPath, string aumid)
    {
        var link = (IShellLinkW)new ShellLink();
        link.SetPath(exePath);

        var propStore = (IPropertyStore)link;

        var key = new PROPERTYKEY
        {
            fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
            pid = 5
        };

        var value = PROPVARIANT.FromString(aumid);
        propStore.SetValue(ref key, ref value);
        propStore.Commit();

        ((IPersistFile)link).Save(shortcutPath, false);
    }
}
