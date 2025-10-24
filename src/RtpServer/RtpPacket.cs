using System;
using System.Collections.Generic;

namespace RtpServer
{
    public sealed class RtpPacket
    {
        public int Version { get; init; }
        public bool Padding { get; init; }
        public bool Extension { get; init; }
        public int CsrcCount { get; init; }
        public bool Marker { get; init; }
        public int PayloadType { get; init; }
        public ushort SequenceNumber { get; init; }
        public uint Timestamp { get; init; }
        public uint Ssrc { get; init; }
        public byte[]? Payload { get; init; }
        public RtpExtension[]? OneByteExtensions { get; init; }
        public bool HasExtension => Extension;
        public byte[]? ExtensionData { get; init; }

        public sealed class RtpExtension
        {
            public byte id { get; init; }
            public byte[] data { get; init; } = Array.Empty<byte>();
        }

    public static RtpPacket Parse(byte[] buffer, int offset, int length)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || length <= 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException(nameof(length));

            int idx = offset;
            byte first = buffer[idx++];
            int version = (first >> 6) & 0x03;
            bool padding = ((first >> 5) & 0x01) != 0;
            bool extension = ((first >> 4) & 0x01) != 0;
            int csrcCount = first & 0x0F;

            byte second = buffer[idx++];
            bool marker = ((second >> 7) & 0x01) != 0;
            int payloadType = second & 0x7F;

            ushort seq = (ushort)((buffer[idx++] << 8) | buffer[idx++]);
            uint ts = (uint)((buffer[idx++] << 24) | (buffer[idx++] << 16) | (buffer[idx++] << 8) | buffer[idx++]);
            uint ssrc = (uint)((buffer[idx++] << 24) | (buffer[idx++] << 16) | (buffer[idx++] << 8) | buffer[idx++]);

            // skip CSRCs
            for (int i = 0; i < csrcCount; i++) idx += 4;

            RtpExtension[]? oneByte = null;
            byte[]? extensionRaw = null;
            if (extension)
            {
                // profile id
                ushort profile = (ushort)((buffer[idx++] << 8) | buffer[idx++]);
                ushort extLenWords = (ushort)((buffer[idx++] << 8) | buffer[idx++]);
                int extLenBytes = extLenWords * 4;
                var extEnd = idx + extLenBytes;
                extensionRaw = new byte[extLenBytes];
                Array.Copy(buffer, idx, extensionRaw, 0, extLenBytes);
                if (profile == 0xBE || profile == 0xBede) // One-Byte or Two-Byte
                {
                    oneByte = ParseOneByteExtensions(buffer, idx, extLenBytes);
                }
                idx = extEnd;
            }

            int payloadStart = idx;
            int payloadLen = length - (payloadStart - offset);
            if (padding)
            {
                byte padCount = buffer[offset + length - 1];
                payloadLen -= padCount;
            }

            var payload = new byte[payloadLen];
            Array.Copy(buffer, payloadStart, payload, 0, payloadLen);

            return new RtpPacket
            {
                Version = version,
                Padding = padding,
                Extension = extension,
                CsrcCount = csrcCount,
                Marker = marker,
                PayloadType = payloadType,
                SequenceNumber = seq,
                Timestamp = ts,
                Ssrc = ssrc,
                Payload = payload,
                OneByteExtensions = oneByte,
                ExtensionData = extensionRaw
            };
        }

        internal static RtpExtension[] ParseOneByteExtensions(byte[] buffer, int offset, int length)
        {
            var list = new List<RtpExtension>();
            int idx = offset;
            int end = offset + length;
            while (idx < end)
            {
                byte b = buffer[idx++];
                if (b == 0) continue; // padding
                int id = (b >> 4) & 0x0F;
                int len = (b & 0x0F) + 1; // length stored as len-1
                if (id == 15) break; // reserved
                if (idx + len > end) break;
                var data = new byte[len];
                Array.Copy(buffer, idx, data, 0, len);
                list.Add(new RtpExtension { id = (byte)id, data = data });
                idx += len;
            }
            return list.ToArray();
        }

        public static RtpPacket Parse(byte[] buffer, int length) => Parse(buffer, 0, length);
    }
}
