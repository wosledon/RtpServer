using System;
using System.Text;
using RtpServer;
using Xunit;

namespace RtpServer.Tests
{
    public class RtpPacketTests
    {
        [Fact]
        public void Parse_MinimalHeader_Works()
        {
            var payload = Encoding.ASCII.GetBytes("hello");
            var buf = new byte[12 + payload.Length];
            buf[0] = 0x80; // V=2, no padding, no extension, CC=0
            buf[1] = 96;   // PT=96
            buf[2] = 0x30; buf[3] = 0x39; // seq 12345
            buf[4] = 1; buf[5] = 2; buf[6] = 3; buf[7] = 4; // ts
            buf[8] = 0xAA; buf[9] = 0xBB; buf[10] = 0xCC; buf[11] = 0xDD; // ssrc
            Array.Copy(payload, 0, buf, 12, payload.Length);

            var pkt = RtpPacket.Parse(buf, buf.Length);

            Assert.Equal(2, pkt.Version);
            Assert.Equal((ushort)12345, pkt.SequenceNumber);
            Assert.Equal((uint)0x01020304, pkt.Timestamp);
            Assert.Equal((uint)0xAABBCCDD, pkt.Ssrc);
            Assert.Equal(96, pkt.PayloadType);
            Assert.Equal("hello", Encoding.ASCII.GetString(pkt.Payload));
        }
    }
}
