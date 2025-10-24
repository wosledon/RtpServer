using System;
using System.Text;
using RtpServer;
using Xunit;

namespace RtpServer.Tests
{
    public class RtpPacketExtensionTests
    {
        [Fact]
        public void Parse_WithHeaderExtension_Works()
        {
            var payload = Encoding.ASCII.GetBytes("data");
            // RTP header (12) + extension header (4) + ext data (4) + payload
            var buf = new byte[12 + 4 + 4 + payload.Length];
            buf[0] = 0x90; // V=2, X=1
            buf[1] = 0x11; // PT=17
            buf[2] = 0x00; buf[3] = 0x01; // seq=1
            buf[4] = buf[5] = buf[6] = buf[7] = 0x00; // ts
            buf[8] = buf[9] = buf[10] = buf[11] = 0x01; // ssrc
            int off = 12;
            // extension: profile 0xABCD, length=1 (one 32-bit word)
            buf[off++] = 0xAB; buf[off++] = 0xCD; buf[off++] = 0x00; buf[off++] = 0x01;
            // extension data 4 bytes
            buf[off++] = 0x11; buf[off++] = 0x22; buf[off++] = 0x33; buf[off++] = 0x44;
            Array.Copy(payload, 0, buf, off, payload.Length);

            var pkt = RtpPacket.Parse(buf, buf.Length);
            Assert.True(pkt.HasExtension);
            Assert.NotNull(pkt.ExtensionData);
            Assert.Equal(4, pkt.ExtensionData!.Length);
            Assert.Equal("data", Encoding.ASCII.GetString(pkt.Payload));
        }
    }
}
