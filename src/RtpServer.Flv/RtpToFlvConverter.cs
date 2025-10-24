using System;
using System.IO;

namespace RtpServer.Flv
{
    /// <summary>
    /// Minimal RTP -> FLV helper.
    /// Produces a small FLV stream containing a single tag whose data is the RTP payload.
    /// This is intentionally minimal (not a full codec muxer) and intended for testing and simple plumbing.
    /// </summary>
    public static class RtpToFlvConverter
    {
        /// <summary>
        /// Convert a single RTP payload (raw payload bytes) into a minimal FLV file containing one audio/video tag.
        /// </summary>
        /// <param name="rtpPayload">RTP payload bytes (already depacketized)</param>
        /// <param name="isAudio">true = create audio tag (type=8), false = video tag (type=9)</param>
        /// <returns>FLV byte array (header + one tag)</returns>
        public static byte[] ConvertRtpPayloadToFlvTag(byte[] rtpPayload, bool isAudio = true)
        {
            if (rtpPayload == null) throw new ArgumentNullException(nameof(rtpPayload));

            using var ms = new MemoryStream();
            // FLV header: 'F' 'L' 'V'
            ms.WriteByte((byte)'F'); ms.WriteByte((byte)'L'); ms.WriteByte((byte)'V');
            ms.WriteByte(0x01); // version
            byte flags = (byte)(isAudio ? 0x04 : 0x01);
            ms.WriteByte(flags);
            // data offset (4 bytes big-endian) = 9
            ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x09 }, 0, 4);

            // PreviousTagSize0 = 0 (4 bytes)
            ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 }, 0, 4);

            // Tag header
            byte tagType = (byte)(isAudio ? 8 : 9);
            ms.WriteByte(tagType);

            // DataSize (3 bytes big-endian)
            int dataSize = rtpPayload.Length;
            ms.WriteByte((byte)((dataSize >> 16) & 0xFF));
            ms.WriteByte((byte)((dataSize >> 8) & 0xFF));
            ms.WriteByte((byte)(dataSize & 0xFF));

            // Timestamp (3 bytes) + TimestampExtended (1)
            uint timestamp = 0;
            ms.WriteByte((byte)((timestamp >> 16) & 0xFF));
            ms.WriteByte((byte)((timestamp >> 8) & 0xFF));
            ms.WriteByte((byte)(timestamp & 0xFF));
            ms.WriteByte((byte)((timestamp >> 24) & 0xFF));

            // StreamID (3 bytes) -- always 0
            ms.Write(new byte[] { 0x00, 0x00, 0x00 }, 0, 3);

            // Tag data = the RTP payload (raw)
            ms.Write(rtpPayload, 0, rtpPayload.Length);

            // PreviousTagSize (4 bytes) = size of tag header (11) + dataSize
            int prevTagSize = 11 + dataSize;
            ms.WriteByte((byte)((prevTagSize >> 24) & 0xFF));
            ms.WriteByte((byte)((prevTagSize >> 16) & 0xFF));
            ms.WriteByte((byte)((prevTagSize >> 8) & 0xFF));
            ms.WriteByte((byte)(prevTagSize & 0xFF));

            return ms.ToArray();
        }
    }
}
