using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Whirlwind
{
    internal static class NetworkProtocols
    {
        public static byte[] build_packet_v0(byte version, string senderIp, long seconds, string message)
        {
            byte[] senderBytes = ip_to_bytes(senderIp);

            if (senderBytes == null) return null;

            byte[] msgBytes = Encoding.UTF8.GetBytes(message);

            byte[] packet = new byte[1 + 4 + 5 + msgBytes.Length];
            int offset = 0;

            packet[offset++] = version;

            Array.Copy(senderBytes, 0, packet, offset, 4);
            offset += 4;

            byte[] secBytes = BitConverter.GetBytes(seconds);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(secBytes);

            Array.Copy(secBytes, secBytes.Length - 5, packet, offset, 5);
            offset += 5;

            Array.Copy(msgBytes, 0, packet, offset, msgBytes.Length);

            return packet;
        }

        private static byte[] ip_to_bytes(string ip)
        {
            string[] parts = ip.Split('.');

            if (parts.Length != 4) return null;

            byte[] bytes = new byte[4];

            for (int i = 0; i < 4; i++)
                bytes[i] = byte.Parse(parts[i]);

            return bytes;
        }

        public static (string SenderIp, long Seconds, string Message) parse_packet(byte[] packet)
        {
            int offset = 0;

            byte version = packet[offset++];

            switch (version)
            {
                case (0):
                    return on_parse_packet_v0(packet);
                default:
                    return ("Error", 0, "Ошибка протокола");
            }
        }

        private static (string SenderIp, long Seconds, string Message) on_parse_packet_v0(byte[] packet)
        {
            int offset = 1;

            byte b1 = packet[offset++];
            byte b2 = packet[offset++];
            byte b3 = packet[offset++];
            byte b4 = packet[offset++];

            string senderIp = $"{b1}.{b2}.{b3}.{b4}";

            byte[] secBytes = new byte[8];

            secBytes[0] = 0;
            secBytes[1] = 0;
            secBytes[2] = 0;

            secBytes[3] = packet[offset++];
            secBytes[4] = packet[offset++];
            secBytes[5] = packet[offset++];
            secBytes[6] = packet[offset++];
            secBytes[7] = packet[offset++];

            if (BitConverter.IsLittleEndian)
                Array.Reverse(secBytes);

            long seconds = BitConverter.ToInt64(secBytes, 0);

            byte[] msgBytes = new byte[packet.Length - offset];
            Array.Copy(packet, offset, msgBytes, 0, msgBytes.Length);

            string message = Encoding.UTF8.GetString(msgBytes);

            return (senderIp, seconds, message);
        }
    }
}
