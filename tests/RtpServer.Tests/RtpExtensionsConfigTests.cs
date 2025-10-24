using System;
using System.Collections.Generic;
using System.Text;
using RtpServer;
using Xunit;

namespace RtpServer.Tests
{
    public class RtpExtensionsConfigTests
    {
        [Fact]
        public void GetExtensionByName_And_ParseMidByName_Works()
        {
            // Build an RTP packet with one-byte extensions: id=2 is mid with payload "m1" (utf8)
            var mid = Encoding.UTF8.GetBytes("m1");
            var ext = new List<byte>();
            ext.Add((byte)((2 << 4) | (mid.Length - 1)));
            ext.AddRange(mid);
            while (ext.Count % 4 != 0) ext.Add(0);

            int extWords = ext.Count / 4;
            var buf = new byte[12 + 4 + ext.Count];
            buf[0] = 0x90; // V=2, X=1
            buf[1] = 0x60; // PT
            buf[2] = 0x00; buf[3] = 0x01; // seq
            // ts & ssrc zeros
            int off = 12;
            buf[off++] = 0xBE; buf[off++] = 0xDE; buf[off++] = (byte)((extWords >> 8) & 0xFF); buf[off++] = (byte)(extWords & 0xFF);
            ext.CopyTo(0, buf, off, ext.Count); off += ext.Count;

            var pkt = RtpPacket.Parse(buf, buf.Length);
            var mapping = new Dictionary<byte, string> { { 2, "mid" } };

            var got = RtpExtensions.ParseMidByName(pkt, mapping, "mid");
            Assert.Equal("m1", got);
        }

        [Fact]
        public void ParseAbsSendTimeSecondsByName_Works()
        {
            // id=3 abs-send-time raw 0x01_02_03
            var abs = new byte[] { 0x01, 0x02, 0x03 };
            var ext = new List<byte>();
            ext.Add((byte)((3 << 4) | (abs.Length - 1)));
            ext.AddRange(abs);
            while (ext.Count % 4 != 0) ext.Add(0);

            int extWords = ext.Count / 4;
            var buf = new byte[12 + 4 + ext.Count];
            buf[0] = 0x90; // V=2, X=1
            buf[1] = 0x60;
            buf[2] = 0x00; buf[3] = 0x02;
            int off = 12;
            buf[off++] = 0xBE; buf[off++] = 0xDE; buf[off++] = (byte)((extWords >> 8) & 0xFF); buf[off++] = (byte)(extWords & 0xFF);
            ext.CopyTo(0, buf, off, ext.Count); off += ext.Count;

            var pkt = RtpPacket.Parse(buf, buf.Length);
            var mapping = new Dictionary<byte, string> { { 3, "abs-send-time" } };

            var sec = RtpExtensions.ParseAbsSendTimeSecondsByName(pkt, mapping, "abs-send-time");
            Assert.NotNull(sec);
            Assert.Equal(((0x010203) / 65536.0), sec.Value, 10);
        }
    }
}
