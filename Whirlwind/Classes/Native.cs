using System;
using System.Runtime.InteropServices;
using static Whirlwind.NetworkProtocols;

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

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void add_expected_protocol(byte protocol_type, ushort protocol_version, byte[] ip);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void remove_expected_protocol(byte protocol_type, ushort protocol_version, byte[] ip);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void free_rust(IntPtr ptr, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void listening_port_messages(string ip, string port, GetBytes cb);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void listening_port_files(string ip, string port, GetBytes cb);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void test_ip_port_sender(string ip, string port, GetBytes cb_ok, GetBytes cb_err);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void send_message(string ip, string port, byte[] data, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void send_file_message(string ip, string port, byte[] data, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void set_timeouts(ulong connect_ms, ulong write_ms);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void listening_udp(string ip, string port, GetBytes cb);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void send_udp(string ip, string port, byte[] data, int len);
    }
}
