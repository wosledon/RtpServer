using System;
using System.Text;
using RtpServer;
using Xunit;

namespace RtpServer.Tests
{
    public class RtpPacketOneByteTests
    {
        [Fact]
        public void Parse_OneByteExtension_Works()
        {
            var payload = Encoding.ASCII.GetBytes("p");
            // RTP header (12) + extension header (4) + ext data (1 header + 1 data + padding to 32-bit) + payload
            // Build extension data: one-byte header: id=3 (0x3), len=0 => header byte = (id<<4) | len = 0x30, data=0xAA
            var extData = new byte[] { 0x30, 0xAA, 0x00, 0x00 }; // pad to 4 bytes
            var buf = new byte[12 + 4 + extData.Length + payload.Length];
            buf[0] = 0x90; // V=2, X=1
            buf[1] = 0x61; // PT=97
            buf[2] = 0x00; buf[3] = 0x02; // seq=2
            buf[4] = buf[5] = buf[6] = buf[7] = 0x00; // ts
            buf[8] = buf[9] = buf[10] = buf[11] = 0x01; // ssrc
            int off = 12;
            // extension: profile 0xBEDE, length = 1 word
            buf[off++] = 0xBE; buf[off++] = 0xDE; buf[off++] = 0x00; buf[off++] = 0x01;
            Array.Copy(extData, 0, buf, off, extData.Length); off += extData.Length;
            Array.Copy(payload, 0, buf, off, payload.Length);

            var pkt = RtpPacket.Parse(buf, buf.Length);
            Assert.True(pkt.HasExtension);
            Assert.NotNull(pkt.OneByteExtensions);
            var ext = pkt.OneByteExtensions!;
            Assert.Single(ext);
            Assert.Equal((byte)3, ext[0].id);
            Assert.Single(ext[0].data);
            Assert.Equal(0xAA, ext[0].data[0]);
        }
    }
}
