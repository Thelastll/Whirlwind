using System;
using System.Runtime.InteropServices;

namespace Whirlwind.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }
}
