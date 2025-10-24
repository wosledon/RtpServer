using System;
using System.Text;
using RtpServer;
using Xunit;

namespace RtpServer.Tests
{
    public class RtpExtensionsTests
    {
        [Fact]
        public void Parse_AudioLevel_Mid_And_AbsSendTime_Works()
        {
            // Build an RTP packet with three one-byte extensions: audio-level(id=1), mid(id=2), abs-send-time(id=3)
            var audioLevel = new byte[] { 0x85 }; // V=1, level=5
            var mid = Encoding.UTF8.GetBytes("mid-1");
            var abs = new byte[] { 0x01, 0x02, 0x03 }; // raw

            // Build ext payload: [hdr(1), data...] with header byte = (id<<4) | len
            // audioLevel: id=1, dataLen=1 => len=0
            // mid: id=2, dataLen=5 => len=4
            // abs: id=3, dataLen=3 => len=2
            var ext = new System.Collections.Generic.List<byte>();
            ext.Add((byte)((1 << 4) | 0)); ext.AddRange(audioLevel);
            ext.Add((byte)((2 << 4) | 4)); ext.AddRange(mid);
            // pad mid to 3+? Not necessary because dataLen=5 -> immediate next
            ext.Add((byte)((3 << 4) | 2)); ext.AddRange(abs);
            // pad to 32-bit boundary
            while (ext.Count % 4 != 0) ext.Add(0);

            // build RTP buffer with extension header (profile 0xBEDE, length words)
            int extWords = ext.Count / 4;
            var buf = new byte[12 + 4 + ext.Count];
            buf[0] = 0x90; // V=2, X=1
            buf[1] = 0x60; // PT
            // seq
            buf[2] = 0x00; buf[3] = 0x20;
            // ts & ssrc
            for (int i = 4; i < 12; i++) buf[i] = 0x00;
            int off = 12;
            buf[off++] = 0xBE; buf[off++] = 0xDE; buf[off++] = (byte)((extWords >> 8) & 0xFF); buf[off++] = (byte)(extWords & 0xFF);
            ext.CopyTo(0, buf, off, ext.Count); off += ext.Count;

            var pkt = RtpPacket.Parse(buf, buf.Length);
            Assert.True(pkt.HasExtension);
            var al = RtpExtensions.ParseAudioLevel(pkt, 1);
            Assert.NotNull(al);
            Assert.True(al.Value.voiceActivity);
            Assert.Equal(5, al.Value.level);
            var midStr = RtpExtensions.ParseMid(pkt, 2);
            Assert.Equal("mid-1", midStr);
            var raw = RtpExtensions.ParseAbsSendTimeRaw(pkt, 3);
            Assert.Equal((uint)0x010203, raw);
            var seconds = RtpExtensions.ParseAbsSendTimeSeconds(pkt, 3);
            Assert.NotNull(seconds);
            Assert.InRange(seconds.Value, 1.0, 2.0);
        }
    }
}
