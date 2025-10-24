using System;
using System.Collections.Generic;

namespace RtpServer
{
    public static class RtcpBuilder
    {
        public static byte[] BuildReceiverReport(uint ssrc, RtcpPacket.RtcpReportBlock[]? blocks)
        {
            int rc = blocks?.Length ?? 0;
            int headerLen = 8;
            int blockLen = 24 * rc;
            int total = headerLen + blockLen;
            var buf = new byte[total];
            int idx = 0;
            buf[idx++] = (byte)((2 << 6) | (rc & 0x1F));
            buf[idx++] = 201; // RR
            ushort lenWords = (ushort)((total / 4) - 1);
            buf[idx++] = (byte)(lenWords >> 8);
            buf[idx++] = (byte)(lenWords & 0xFF);

            buf[idx++] = (byte)(ssrc >> 24);
            buf[idx++] = (byte)(ssrc >> 16);
            buf[idx++] = (byte)(ssrc >> 8);
            buf[idx++] = (byte)(ssrc & 0xFF);

            if (blocks != null)
            {
                foreach (var b in blocks)
                {
                    buf[idx++] = (byte)(b.Ssrc >> 24);
                    buf[idx++] = (byte)(b.Ssrc >> 16);
                    buf[idx++] = (byte)(b.Ssrc >> 8);
                    buf[idx++] = (byte)(b.Ssrc & 0xFF);
                    buf[idx++] = b.FractionLost;
                    buf[idx++] = (byte)((b.CumulativeLost >> 16) & 0xFF);
                    buf[idx++] = (byte)((b.CumulativeLost >> 8) & 0xFF);
                    buf[idx++] = (byte)(b.CumulativeLost & 0xFF);
                    buf[idx++] = (byte)(b.HighestSeqNo >> 24);
                    buf[idx++] = (byte)(b.HighestSeqNo >> 16);
                    buf[idx++] = (byte)(b.HighestSeqNo >> 8);
                    buf[idx++] = (byte)(b.HighestSeqNo & 0xFF);
                    buf[idx++] = (byte)(b.Jitter >> 24);
                    buf[idx++] = (byte)(b.Jitter >> 16);
                    buf[idx++] = (byte)(b.Jitter >> 8);
                    buf[idx++] = (byte)(b.Jitter & 0xFF);
                    buf[idx++] = (byte)(b.LastSr >> 24);
                    buf[idx++] = (byte)(b.LastSr >> 16);
                    buf[idx++] = (byte)(b.LastSr >> 8);
                    buf[idx++] = (byte)(b.LastSr & 0xFF);
                    buf[idx++] = (byte)(b.DelaySinceLastSr >> 24);
                    buf[idx++] = (byte)(b.DelaySinceLastSr >> 16);
                    buf[idx++] = (byte)(b.DelaySinceLastSr >> 8);
                    buf[idx++] = (byte)(b.DelaySinceLastSr & 0xFF);
                }
            }

            return buf;
        }

        public static byte[] BuildSenderReport(uint ssrc, ulong ntp, uint rtpTs, uint pktCount, uint octetCount, RtcpPacket.RtcpReportBlock[]? blocks)
        {
            int rc = blocks?.Length ?? 0;
            int headerLen = 28;
            int blockLen = 24 * rc;
            int total = headerLen + blockLen;
            var buf = new byte[total];
            int idx = 0;
            buf[idx++] = (byte)((2 << 6) | (rc & 0x1F));
            buf[idx++] = 200; // SR
            ushort lenWords = (ushort)((total / 4) - 1);
            buf[idx++] = (byte)(lenWords >> 8);
            buf[idx++] = (byte)(lenWords & 0xFF);

            buf[idx++] = (byte)(ssrc >> 24);
            buf[idx++] = (byte)(ssrc >> 16);
            buf[idx++] = (byte)(ssrc >> 8);
            buf[idx++] = (byte)(ssrc & 0xFF);

            buf[idx++] = (byte)(ntp >> 56);
            buf[idx++] = (byte)(ntp >> 48);
            buf[idx++] = (byte)(ntp >> 40);
            buf[idx++] = (byte)(ntp >> 32);
            buf[idx++] = (byte)(ntp >> 24);
            buf[idx++] = (byte)(ntp >> 16);
            buf[idx++] = (byte)(ntp >> 8);
            buf[idx++] = (byte)(ntp & 0xFF);

            buf[idx++] = (byte)(rtpTs >> 24);
            buf[idx++] = (byte)(rtpTs >> 16);
            buf[idx++] = (byte)(rtpTs >> 8);
            buf[idx++] = (byte)(rtpTs & 0xFF);

            buf[idx++] = (byte)(pktCount >> 24);
            buf[idx++] = (byte)(pktCount >> 16);
            buf[idx++] = (byte)(pktCount >> 8);
            buf[idx++] = (byte)(pktCount & 0xFF);

            buf[idx++] = (byte)(octetCount >> 24);
            buf[idx++] = (byte)(octetCount >> 16);
            buf[idx++] = (byte)(octetCount >> 8);
            buf[idx++] = (byte)(octetCount & 0xFF);

            if (blocks != null)
            {
                foreach (var b in blocks)
                {
                    buf[idx++] = (byte)(b.Ssrc >> 24);
                    buf[idx++] = (byte)(b.Ssrc >> 16);
                    buf[idx++] = (byte)(b.Ssrc >> 8);
                    buf[idx++] = (byte)(b.Ssrc & 0xFF);
                    buf[idx++] = b.FractionLost;
                    buf[idx++] = (byte)((b.CumulativeLost >> 16) & 0xFF);
                    buf[idx++] = (byte)((b.CumulativeLost >> 8) & 0xFF);
                    buf[idx++] = (byte)(b.CumulativeLost & 0xFF);
                    buf[idx++] = (byte)(b.HighestSeqNo >> 24);
                    buf[idx++] = (byte)(b.HighestSeqNo >> 16);
                    buf[idx++] = (byte)(b.HighestSeqNo >> 8);
                    buf[idx++] = (byte)(b.HighestSeqNo & 0xFF);
                    buf[idx++] = (byte)(b.Jitter >> 24);
                    buf[idx++] = (byte)(b.Jitter >> 16);
                    buf[idx++] = (byte)(b.Jitter >> 8);
                    buf[idx++] = (byte)(b.Jitter & 0xFF);
                    buf[idx++] = (byte)(b.LastSr >> 24);
                    buf[idx++] = (byte)(b.LastSr >> 16);
                    buf[idx++] = (byte)(b.LastSr >> 8);
                    buf[idx++] = (byte)(b.LastSr & 0xFF);
                    buf[idx++] = (byte)(b.DelaySinceLastSr >> 24);
                    buf[idx++] = (byte)(b.DelaySinceLastSr >> 16);
                    buf[idx++] = (byte)(b.DelaySinceLastSr >> 8);
                    buf[idx++] = (byte)(b.DelaySinceLastSr & 0xFF);
                }
            }

            return buf;
        }
    }
}
