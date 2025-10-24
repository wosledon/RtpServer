using System;
using RtpServer;
using Xunit;

namespace RtpServer.Tests
{
    public class RtcpBuilderTests
    {
        [Fact]
        public void BuildReceiverReport_Then_Parse_Works()
        {
            var rb = new RtcpPacket.RtcpReportBlock
            {
                Ssrc = 0xAABBCCDD,
                FractionLost = 1,
                CumulativeLost = 0x0010,
                HighestSeqNo = 6,
                Jitter = 7,
                LastSr = 8,
                DelaySinceLastSr = 9
            };
            var rr = RtcpBuilder.BuildReceiverReport(0x11223344, new[] { rb });
            var pkt = RtcpPacket.Parse(rr, rr.Length);
            Assert.Equal(201, pkt.PacketType);
            Assert.Equal((uint)0x11223344, pkt.SenderSsrc);
            Assert.NotNull(pkt.ReportBlocks);
            Assert.Single(pkt.ReportBlocks);
            var got = pkt.ReportBlocks![0];
            Assert.Equal((uint)0xAABBCCDD, got.Ssrc);
            Assert.Equal((byte)1, got.FractionLost);
        }
    }
}
