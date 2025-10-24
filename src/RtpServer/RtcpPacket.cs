using System;
using System.Collections.Generic;

namespace RtpServer
{
    public sealed class RtcpPacket
    {
        public int Version { get; init; }
        public int PacketType { get; init; }
        public int ReportCount { get; init; }
        public uint SenderSsrc { get; init; }
        public ulong? NtpTimestamp { get; init; }
        public uint? RtpTimestamp { get; init; }
        public uint? PacketCount { get; init; }
        public uint? OctetCount { get; init; }
    // Backwards-compatible aliases expected by tests
    public uint? SenderPacketCount => PacketCount;
    public uint? SenderOctetCount => OctetCount;
        public RtcpReportBlock[]? ReportBlocks { get; init; }

    public static RtcpPacket Parse(byte[] buffer, int offset, int length)
        {
            int idx = offset;
            byte first = buffer[idx++];
            int version = (first >> 6) & 0x03;
            int rc = first & 0x1F;
            byte pt = buffer[idx++];
            ushort lenWords = (ushort)((buffer[idx++] << 8) | buffer[idx++]);
            int pktLen = (lenWords + 1) * 4;

            uint ssrc = (uint)((buffer[idx++] << 24) | (buffer[idx++] << 16) | (buffer[idx++] << 8) | buffer[idx++]);

            ulong? ntp = null;
            uint? rtpTs = null, pktCount = null, octetCount = null;
            if (pt == 200) // SR
            {
                ulong ntp64 = ((ulong)buffer[idx++] << 56) | ((ulong)buffer[idx++] << 48) | ((ulong)buffer[idx++] << 40) | ((ulong)buffer[idx++] << 32)
                              | ((ulong)buffer[idx++] << 24) | ((ulong)buffer[idx++] << 16) | ((ulong)buffer[idx++] << 8) | buffer[idx++];
                ntp = ntp64;
                rtpTs = (uint)((buffer[idx++] << 24) | (buffer[idx++] << 16) | (buffer[idx++] << 8) | buffer[idx++]);
                pktCount = (uint)((buffer[idx++] << 24) | (buffer[idx++] << 16) | (buffer[idx++] << 8) | buffer[idx++]);
                octetCount = (uint)((buffer[idx++] << 24) | (buffer[idx++] << 16) | (buffer[idx++] << 8) | buffer[idx++]);
            }

            var blocks = new List<RtcpReportBlock>();
            // Parse available 24-byte report blocks found in the packet body regardless of the RC value
            while (idx + 24 <= offset + pktLen)
            {
                uint rbSsrc = (uint)((buffer[idx++] << 24) | (buffer[idx++] << 16) | (buffer[idx++] << 8) | buffer[idx++]);
                byte fraction = buffer[idx++];
                int cumulative = (buffer[idx++] << 16) | (buffer[idx++] << 8) | buffer[idx++];
                uint highestSeq = (uint)((buffer[idx++] << 24) | (buffer[idx++] << 16) | (buffer[idx++] << 8) | buffer[idx++]);
                uint jitter = (uint)((buffer[idx++] << 24) | (buffer[idx++] << 16) | (buffer[idx++] << 8) | buffer[idx++]);
                uint lsr = (uint)((buffer[idx++] << 24) | (buffer[idx++] << 16) | (buffer[idx++] << 8) | buffer[idx++]);
                uint dlsr = (uint)((buffer[idx++] << 24) | (buffer[idx++] << 16) | (buffer[idx++] << 8) | buffer[idx++]);

                blocks.Add(new RtcpReportBlock
                {
                    Ssrc = rbSsrc,
                    FractionLost = fraction,
                    CumulativeLost = cumulative,
                    HighestSeqNo = highestSeq,
                    Jitter = jitter,
                    LastSr = lsr,
                    DelaySinceLastSr = dlsr
                });
            }

            return new RtcpPacket
            {
                Version = version,
                PacketType = pt,
                ReportCount = rc,
                SenderSsrc = ssrc,
                NtpTimestamp = ntp,
                RtpTimestamp = rtpTs,
                PacketCount = pktCount,
                OctetCount = octetCount,
                ReportBlocks = blocks.ToArray()
            };
        }
        public static RtcpPacket Parse(byte[] buffer, int length) => Parse(buffer, 0, length);

        public sealed class RtcpReportBlock
        {
            public uint Ssrc { get; init; }
            public byte FractionLost { get; init; }
            public int CumulativeLost { get; init; }
            public uint HighestSeqNo { get; init; }
            public uint Jitter { get; init; }
            public uint LastSr { get; init; }
            public uint DelaySinceLastSr { get; init; }
        }
    }
    
}
