using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Whirlwind
{
    internal static class Native
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void GetBytes(IntPtr bytes, int len);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void PassDelegate();

        private const string Dll = "Network_Module.dll";

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void init_module();

        //[DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        //public static extern void init_callback_thread();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void add_expected_protocol(byte protocol_type, ushort protocol_version, byte[] ip);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void remove_expected_protocol(byte protocol_type, ushort protocol_version, byte[] ip);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void free_rust(IntPtr ptr, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void listening_port(string ip, string port, GetBytes cb);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void test_ip_port_sender(string ip, string port, GetBytes cb_ok, GetBytes cb_err);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void send_message(string ip, string port, byte[] data, int len, PassDelegate pd, GetBytes cb);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void set_timeouts(ulong connect_ms, ulong write_ms);
    }
}
