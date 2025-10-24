using System;

namespace RtpServer
{
    public static class RtpExtensions
    {
        private static byte[]? GetExtensionData(RtpPacket pkt, byte id)
        {
            if (pkt.OneByteExtensions == null) return null;
            foreach (var e in pkt.OneByteExtensions)
            {
                if (e.id == id) return e.data;
            }
            return null;
        }

        /// <summary>
        /// Resolve an extension by name using a user-provided mapping of id->name and return its raw data.
        /// Returns null if no mapping or extension is present.
        /// </summary>
        public static byte[]? GetExtensionByName(RtpPacket pkt, System.Collections.Generic.IDictionary<byte, string>? mapping, string name)
        {
            if (mapping == null) return null;
            foreach (var kv in mapping)
            {
                if (string.Equals(kv.Value, name, StringComparison.OrdinalIgnoreCase))
                {
                    return GetExtensionData(pkt, kv.Key);
                }
            }
            return null;
        }

        public static (bool voiceActivity, int level)? ParseAudioLevel(RtpPacket pkt, byte id)
        {
            var data = GetExtensionData(pkt, id);
            if (data == null || data.Length == 0) return null;
            bool voice = (data[0] & 0x80) != 0;
            int level = data[0] & 0x7F;
            return (voice, level);
        }

        public static string? ParseMid(RtpPacket pkt, byte id)
        {
            var data = GetExtensionData(pkt, id);
            if (data == null) return null;
            return System.Text.Encoding.UTF8.GetString(data);
        }

        public static string? ParseMidByName(RtpPacket pkt, System.Collections.Generic.IDictionary<byte, string>? mapping, string name)
        {
            var d = GetExtensionByName(pkt, mapping, name);
            if (d == null) return null;
            return System.Text.Encoding.UTF8.GetString(d);
        }

        public static uint? ParseAbsSendTimeRaw(RtpPacket pkt, byte id)
        {
            var data = GetExtensionData(pkt, id);
            if (data == null || data.Length < 3) return null;
            return (uint)((data[0] << 16) | (data[1] << 8) | data[2]);
        }

        public static double? ParseAbsSendTimeSeconds(RtpPacket pkt, byte id)
        {
            var raw = ParseAbsSendTimeRaw(pkt, id);
            if (raw == null) return null;
            return raw.Value / 65536.0;
        }

        public static double? ParseAbsSendTimeSecondsByName(RtpPacket pkt, System.Collections.Generic.IDictionary<byte, string>? mapping, string name)
        {
            var d = GetExtensionByName(pkt, mapping, name);
            if (d == null || d.Length < 3) return null;
            uint raw = (uint)((d[0] << 16) | (d[1] << 8) | d[2]);
            return raw / 65536.0;
        }
    }
}
