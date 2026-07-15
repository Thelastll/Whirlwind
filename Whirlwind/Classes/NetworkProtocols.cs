using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Whirlwind
{
    internal static class NetworkProtocols
    {
        public static byte[] ip_to_bytes(string ip)
        {
            string[] parts = ip.Split('.');

            if (parts.Length != 4) return null;

            byte[] bytes = new byte[4];

            for (int i = 0; i < 4; i++)
                bytes[i] = byte.Parse(parts[i]);

            return bytes;
        }

        public static byte[] build_packet(byte protocolType, ushort protocolVersion, string senderIp, byte[] body)
        {
            byte[] ip_bytes = ip_to_bytes(senderIp);
            if (ip_bytes == null) return null;

            uint bodyLength = (uint)body.Length;

            byte[] packet = new byte[11 + body.Length];
            int offset = 0;

            packet[offset++] = protocolType;

            packet[offset++] = (byte)(protocolVersion >> 8);
            packet[offset++] = (byte)(protocolVersion & 0xFF);

            Array.Copy(ip_bytes, 0, packet, offset, 4);
            offset += 4;

            packet[offset++] = (byte)(bodyLength >> 24);
            packet[offset++] = (byte)(bodyLength >> 16);
            packet[offset++] = (byte)(bodyLength >> 8);
            packet[offset++] = (byte)(bodyLength & 0xFF);

            Array.Copy(body, 0, packet, offset, body.Length);

            return packet;
        }

        public static (ushort, byte[]) build_system_extra_data_v0(byte action, byte future_type, ushort future_version)
        {
            byte[] data = new byte[4];

            data[0] = action;
            data[1] = future_type;

            data[2] = (byte)(future_version >> 8);
            data[3] = (byte)(future_version & 0xFF);

            return (0, data);
        }

        public static byte[] build_system_packet(string senderIp, long seconds, (ushort protocol_version, byte[] data) extra)
        {
            if (extra.data == null)
                extra.data = Array.Empty<byte>();

            byte[] secBytes = BitConverter.GetBytes(seconds);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(secBytes);

            byte[] body = new byte[5 + extra.data.Length];
            int offset = 0;

            // seconds (5 bytes)
            Array.Copy(secBytes, secBytes.Length - 5, body, offset, 5);
            offset += 5;

            // extra_data
            Array.Copy(extra.data, 0, body, offset, extra.data.Length);

            return build_packet(protocolType: 0, protocolVersion: extra.protocol_version, senderIp: senderIp, body: body);
        }

        public static (ushort, byte[]) build_text_extra_data_v0()
        {
            return (0, null);
        }

        public static byte[] build_text_packet(string senderIp, long seconds, byte device_type, byte message_type, (ushort protocol_version, byte[] data) extra, string message)
        {
            if (extra.data == null)
                extra.data = Array.Empty<byte>();

            byte[] msgBytes = Encoding.UTF8.GetBytes(message);

            int headerInsideBody = 5 + 1 + 2;
            int bodySize = headerInsideBody + extra.data.Length + msgBytes.Length;

            byte[] body = new byte[bodySize];
            int offset = 0;

            byte[] secBytes = BitConverter.GetBytes(seconds);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(secBytes);
            Array.Copy(secBytes, secBytes.Length - 5, body, offset, 5);
            offset += 5;

            byte combined = (byte)((device_type << 5) | (message_type & 0b11111));
            body[offset++] = combined;

            ushort msgOffset = (ushort)(headerInsideBody + extra.data.Length);
            body[offset++] = (byte)(msgOffset >> 8);
            body[offset++] = (byte)(msgOffset & 0xFF);

            Array.Copy(extra.data, 0, body, offset, extra.data.Length);
            offset += extra.data.Length;

            Array.Copy(msgBytes, 0, body, offset, msgBytes.Length);

            return build_packet(
                protocolType: 1,
                protocolVersion: extra.protocol_version,
                senderIp: senderIp,
                body: body
            );
        }

        public static byte[] build_file_packet(string senderIp, long seconds, byte device_type, byte message_type, 
                                                (ushort protocol_version, byte[] data) extra,string fileName, byte[] fileContent)
        {
            if (extra.data == null)
                extra.data = Array.Empty<byte>();

            byte[] fileNameBytes = Encoding.UTF8.GetBytes(fileName);
            int fileNameLen = fileNameBytes.Length;
            int fileContentLen = fileContent.Length;

            int headerInsideBody = 5 + 1 + 2;
            int bodySize =
                headerInsideBody +
                extra.data.Length +
                2 +
                fileNameLen +
                fileContentLen;

            byte[] body = new byte[bodySize];
            int offset = 0;

            byte[] secBytes = BitConverter.GetBytes(seconds);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(secBytes);
            Array.Copy(secBytes, secBytes.Length - 5, body, offset, 5);
            offset += 5;

            byte combined = (byte)((device_type << 5) | (message_type & 0b11111));
            body[offset++] = combined;

            ushort ptrToPtrFileStart = (ushort)(11 + headerInsideBody + extra.data.Length);
            body[offset++] = (byte)(ptrToPtrFileStart >> 8);
            body[offset++] = (byte)(ptrToPtrFileStart & 0xFF);

            Array.Copy(extra.data, 0, body, offset, extra.data.Length);
            offset += extra.data.Length;

            ushort ptrFileStart = (ushort)(11 + headerInsideBody + extra.data.Length + 2 + fileNameLen);
            body[offset++] = (byte)(ptrFileStart >> 8);
            body[offset++] = (byte)(ptrFileStart & 0xFF);

            Array.Copy(fileNameBytes, 0, body, offset, fileNameLen);
            offset += fileNameLen;

            Array.Copy(fileContent, 0, body, offset, fileContentLen);

            return build_packet(
                protocolType: 2,
                protocolVersion: extra.protocol_version,
                senderIp: senderIp,
                body: body
            );
        }


        public static (byte action, byte future_type, ushort future_version) on_parse_system_extra_data_v0(byte[] extra_data)
        {
            if (extra_data == null || extra_data.Length < 4)
                return (0, 0, 0);

            byte action = extra_data[0];
            byte future_type = extra_data[1];
            ushort future_version = (ushort)((extra_data[2] << 8) | extra_data[3]);

            return (action, future_type, future_version);
        }


        public static (long seconds, byte[] extra_data) on_parse_system_packet(byte[] packet)
        {
            int offset = 11;

            byte[] secBytes = new byte[8];
            secBytes[0] = secBytes[1] = secBytes[2] = 0;
            Array.Copy(packet, offset, secBytes, 3, 5);
            offset += 5;

            if (BitConverter.IsLittleEndian)
                Array.Reverse(secBytes);

            long seconds = BitConverter.ToInt64(secBytes, 0);

            int extraLen = packet.Length - offset;
            byte[] extra_data = new byte[extraLen];
            Array.Copy(packet, offset, extra_data, 0, extraLen);

            return (seconds, extra_data);
        }

        public static (long seconds, byte device_type, byte message_type, byte[] extra_data, string Message) on_parse_text_packet(byte[] packet)
        {
            int offset = 11;

            byte[] secBytes = new byte[8];
            secBytes[0] = secBytes[1] = secBytes[2] = 0;
            Array.Copy(packet, offset, secBytes, 3, 5);
            offset += 5;

            if (BitConverter.IsLittleEndian)
                Array.Reverse(secBytes);

            long seconds = BitConverter.ToInt64(secBytes, 0);

            byte combined = packet[offset++];
            byte device_type = (byte)(combined >> 5);
            byte message_type = (byte)(combined & 0b11111);

            ushort msgOffset =
                (ushort)((packet[offset++] << 8) | packet[offset++]);

            int extraLen = msgOffset - offset;
            if (extraLen < 0) extraLen = 0;

            byte[] extra_data = new byte[extraLen];
            if (extraLen > 0)
                Array.Copy(packet, offset, extra_data, 0, extraLen);

            offset += extraLen;

            int msgStart = 11 + msgOffset;
            int msgLen = packet.Length - msgStart;
            if (msgLen < 0) msgLen = 0;

            byte[] msgBytes = new byte[msgLen];
            if (msgLen > 0)
                Array.Copy(packet, msgStart, msgBytes, 0, msgLen);

            string message = Encoding.UTF8.GetString(msgBytes);

            return (seconds, device_type, message_type, extra_data, message);
        }

        public static (long seconds, byte device_type, byte message_type, byte[] extra_data, string fileName, byte[] fileContent) 
        on_parse_file_packet(byte[] packet)
        {
            int offset = 11;

            byte[] secBytes = new byte[8];
            secBytes[0] = secBytes[1] = secBytes[2] = 0;
            Array.Copy(packet, offset, secBytes, 3, 5);
            offset += 5;

            if (BitConverter.IsLittleEndian)
                Array.Reverse(secBytes);

            long seconds = BitConverter.ToInt64(secBytes, 0);

            byte combined = packet[offset++];
            byte device_type = (byte)(combined >> 5);
            byte message_type = (byte)(combined & 0b11111);

            ushort ptrToPtrFileStart =
                (ushort)((packet[offset++] << 8) | packet[offset++]);

            int extraLen = ptrToPtrFileStart - offset;
            if (extraLen < 0) extraLen = 0;

            byte[] extra_data = new byte[extraLen];
            if (extraLen > 0)
                Array.Copy(packet, offset, extra_data, 0, extraLen);

            offset += extraLen;

            ushort ptrFileStart =
                (ushort)((packet[offset++] << 8) | packet[offset++]);

            int fileNameLen = ptrFileStart - offset;
            if (fileNameLen < 0) fileNameLen = 0;

            byte[] fileNameBytes = new byte[fileNameLen];
            if (fileNameLen > 0)
                Array.Copy(packet, offset, fileNameBytes, 0, fileNameLen);

            string fileName = Encoding.UTF8.GetString(fileNameBytes);

            offset += fileNameLen;

            int fileContentLen = packet.Length - offset;
            if (fileContentLen < 0) fileContentLen = 0;

            byte[] fileContent = new byte[fileContentLen];
            if (fileContentLen > 0)
                Array.Copy(packet, offset, fileContent, 0, fileContentLen);

            return (seconds, device_type, message_type, extra_data, fileName, fileContent);
        }

    }
}
