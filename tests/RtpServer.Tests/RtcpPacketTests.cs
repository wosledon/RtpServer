using System;
using Xunit;
using RtpServer;

namespace RtpServer.Tests
{
    public class RtcpPacketTests
    {
        [Fact]
        public void Parse_RtcpSr_Works()
        {
            // Build a minimal RTCP SR packet with one report block
            // Header (4) + sender SSRC(4) + NTP(8) + RTP ts(4) + pkt count(4) + octet count(4) = 28
            // To include one report block (24 bytes) total length bytes = (28+24)/4 - 1 = (52/4)-1 = 13-1=12 => length=12
            var totalBytes = 28 + 24;
            var lenWords = (totalBytes / 4) - 1;
            var buf = new byte[totalBytes];
            buf[0] = 0x80; // V=2
            buf[1] = 200; // PT=200 (SR)
            buf[2] = (byte)((lenWords >> 8) & 0xFF); buf[3] = (byte)(lenWords & 0xFF);
            int off = 4;
            // sender SSRC
            buf[off++] = 0x11; buf[off++] = 0x22; buf[off++] = 0x33; buf[off++] = 0x44;
            // NTP (8)
            buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x01;
            buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x02;
            // RTP ts
            buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x03;
            // pkt count
            buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x04;
            // octet count
            buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x05;
            // report block (24)
            // ssrc
            buf[off++] = 0xAA; buf[off++] = 0xBB; buf[off++] = 0xCC; buf[off++] = 0xDD;
            // fraction lost + cumulative lost (3)
            buf[off++] = 0x01; buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x10;
            // highest seq
            buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x06;
            // jitter
            buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x07;
            // lsr
            buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x08;
            // dlsr
            buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x00; buf[off++] = 0x09;

            var pkt = RtcpPacket.Parse(buf, buf.Length);
            Assert.Equal(2, pkt.Version);
            Assert.Equal(200, pkt.PacketType);
            Assert.Equal(0, pkt.ReportCount);
            Assert.Equal((uint)0x11223344, pkt.SenderSsrc);
            Assert.Equal((ulong)0x0000000100000002, pkt.NtpTimestamp);
            Assert.Equal((uint)3, pkt.RtpTimestamp);
            Assert.Equal((uint)4, pkt.SenderPacketCount);
            Assert.Equal((uint)5, pkt.SenderOctetCount);
            Assert.NotNull(pkt.ReportBlocks);
            Assert.Single(pkt.ReportBlocks);
            var rb = pkt.ReportBlocks![0];
            Assert.Equal((uint)0xAABBCCDD, rb.Ssrc);
            Assert.Equal((byte)1, rb.FractionLost);
            Assert.Equal(0x0010, rb.CumulativeLost & 0xFFFF);
            Assert.Equal((uint)6, rb.HighestSeqNo);
            Assert.Equal((uint)7, rb.Jitter);
            Assert.Equal((uint)8, rb.LastSr);
            Assert.Equal((uint)9, rb.DelaySinceLastSr);
        }
    }
}
