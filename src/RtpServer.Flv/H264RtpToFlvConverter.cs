using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RtpServer.Flv
{
    /// <summary>
    /// Minimal H.264 RTP -> FLV muxer.
    /// - Reassembles FU-A and STAP-A NAL units.
    /// - Extracts SPS/PPS and emits an AVC sequence header (AVCDecoderConfigurationRecord) wrapped in an FLV tag.
    /// - Emits subsequent NALUs as AVCC-formatted FLV video tags.
    /// This is intentionally minimal and designed for testing/demoing; it does not implement full jitter/packet-loss recovery.
    /// </summary>
    public sealed class H264RtpToFlvConverter
    {
        private List<byte>? _fuBuffer;
        private byte[]? _sps;
        private byte[]? _pps;
        private bool _seqHeaderEmitted;
        // pending tags emitted before seq header (flush after seq header emitted)
        private List<byte[]>? _pendingTags;
        private uint? _baseRtpTimestamp;
        private const uint RtpClockRate = 90000;
        private readonly Microsoft.Extensions.Logging.ILogger? _logger;

        // Cached initialization segment (FLV header + AVC sequence header tag)
        public byte[]? InitSegment { get; private set; }

        public H264RtpToFlvConverter(Microsoft.Extensions.Logging.ILogger? logger = null)
        {
            _logger = logger;
            _pendingTags = new List<byte[]>();
        }

        private uint ComputeTimestampMs(uint rtpTimestamp)
        {
            if (!_baseRtpTimestamp.HasValue)
            {
                _baseRtpTimestamp = rtpTimestamp;
                return 0;
            }
            uint delta = unchecked(rtpTimestamp - _baseRtpTimestamp.Value);
            return (uint)((delta * 1000UL) / RtpClockRate);
        }

        public IEnumerable<byte[]> ProcessRtpPacket(RtpPacket pkt)
        {
            var outList = new List<byte[]>();
            try
            {
                try { Console.Error.WriteLine($"H264RtpToFlvConverter.ProcessRtpPacket ssrc={pkt.Ssrc} seq={pkt.SequenceNumber} ts={pkt.Timestamp} pt={pkt.PayloadType} payloadLen={(pkt.Payload?.Length ?? 0)}"); } catch { }
                var payload = pkt.Payload;
                if (payload == null || payload.Length == 0) return outList;

                // must have at least one byte for NAL header
                if (payload.Length < 1) return outList;

                // compute FLV timestamp in milliseconds based on RTP timestamp (assume 90kHz clock)
                uint timestampMs = ComputeTimestampMs(pkt.Timestamp);

                int nalType = payload[0] & 0x1F;

                if (nalType == 28) // FU-A
                {
                    if (payload.Length < 2) { Console.Error.WriteLine("H264RtpToFlvConverter: FU-A too short"); return outList; }
                    bool start = (payload[1] & 0x80) != 0;
                    bool end = (payload[1] & 0x40) != 0;
                    byte fuHeader = (byte)(payload[1] & 0x1F);
                    if (start)
                    {
                        _fuBuffer = new List<byte>();
                        byte nalHeader = (byte)((payload[0] & 0xE0) | fuHeader);
                        _fuBuffer.Add(nalHeader);
                        int copyLen = Math.Max(0, payload.Length - 2);
                        if (copyLen > 0) _fuBuffer.AddRange(new ArraySegment<byte>(payload, 2, copyLen));
                    }
                    else if (_fuBuffer != null)
                    {
                        int copyLen = Math.Max(0, payload.Length - 2);
                        if (copyLen > 0) _fuBuffer.AddRange(new ArraySegment<byte>(payload, 2, copyLen));
                    }

                    if (end && _fuBuffer != null)
                    {
                        var nal = _fuBuffer.ToArray();
                        foreach (var tag in ProcessNalUnit(nal, timestampMs)) outList.Add(tag);
                        _fuBuffer = null;
                    }
                }
                else if (nalType == 24) // STAP-A (aggregation)
                {
                    int idx = 1;
                    while (idx + 2 <= payload.Length)
                    {
                        int size = (payload[idx] << 8) | payload[idx + 1];
                        idx += 2;
                        if (size <= 0) { Console.Error.WriteLine($"H264RtpToFlvConverter: STAP-A non-positive size {size}"); continue; }
                        if (idx + size > payload.Length) { Console.Error.WriteLine($"H264RtpToFlvConverter: STAP-A size out of range idx={idx} size={size} payloadLen={payload.Length}"); break; }
                        var nal = new byte[size];
                        // safe copy (should not throw because of the check above)
                        Array.Copy(payload, idx, nal, 0, size);
                        foreach (var tag in ProcessNalUnit(nal, timestampMs)) outList.Add(tag);
                        idx += size;
                    }
                }
                else // Single NAL unit
                {
                    var nal = new byte[payload.Length];
                    if (payload.Length > 0) Array.Copy(payload, 0, nal, 0, payload.Length);
                    foreach (var tag in ProcessNalUnit(nal, timestampMs)) outList.Add(tag);
                }

            }
            catch (Exception ex)
            {
                // swallow errors from bad packets to avoid crashing worker; caller may log
                try { Console.Error.WriteLine($"H264RtpToFlvConverter.ProcessRtpPacket error: {ex.Message}"); } catch { }
                return outList;
            }

            return outList;
        }

        private IEnumerable<byte[]> ProcessNalUnit(byte[] nal, uint timestampMs = 0)
        {
            var outList = new List<byte[]>();
            if (nal.Length == 0) return outList;

            int nalType = nal[0] & 0x1F;
            try { Console.Error.WriteLine($"Processing NAL type: {nalType}, length: {nal.Length}"); } catch { }
            if (nalType == 7) // SPS
                {
                    // SPS ID is typically 0, but for now assume it's correct
                    // store RBSP (skip NAL header)
                    _sps = new byte[nal.Length - 1];
                    Array.Copy(nal, 1, _sps, 0, _sps.Length);
                    try { Console.Error.WriteLine($"Stored SPS, length: {_sps.Length}"); } catch { }
                    // if we already have PPS and haven't emitted seq header, emit it now
                    if (_pps != null && !_seqHeaderEmitted)
                    {
                        var seq = BuildInitSegment(_sps, _pps);
                        InitSegment = seq;
                        outList.Add(seq); // also return so live clients get it
                                          // flush any pending tags buffered before seq header
                        if (_pendingTags != null && _pendingTags.Count > 0)
                        {
                            outList.AddRange(_pendingTags);
                            _pendingTags.Clear();
                        }
                        _seqHeaderEmitted = true;
                        try { Console.Error.WriteLine("Emitted sequence header after SPS"); } catch { }
                    }
                }
                else if (nalType == 8) // PPS
                {
                    // PPS ID is the first byte after NAL header
                    if (nal.Length < 2 || nal[1] != 0)
                    {
                        // Only support PPS ID 0 for simplicity
                        try { Console.Error.WriteLine($"H264RtpToFlvConverter: Ignoring PPS with ID {nal[1]}, only ID 0 supported"); } catch { }
                        return outList;
                    }
                    _pps = new byte[nal.Length - 1];
                    Array.Copy(nal, 1, _pps, 0, _pps.Length);
                    try { Console.Error.WriteLine($"Stored PPS ID 0, length: {_pps.Length}"); } catch { }
                    if (_sps != null && !_seqHeaderEmitted)
                    {
                        var seq = BuildInitSegment(_sps, _pps);
                        InitSegment = seq;
                        outList.Add(seq);
                        // flush pending tags
                        if (_pendingTags != null && _pendingTags.Count > 0)
                        {
                            outList.AddRange(_pendingTags);
                            _pendingTags.Clear();
                        }
                        _seqHeaderEmitted = true;
                        try { Console.Error.WriteLine("Emitted sequence header after PPS"); } catch { }
                    }
                }
                else
                {
                    // For video frames, build AVCC NAL length prefixed data
                    var avcc = new MemoryStream();
                    // 4-byte length
                    avcc.WriteByte((byte)((nal.Length >> 24) & 0xFF));
                    avcc.WriteByte((byte)((nal.Length >> 16) & 0xFF));
                    avcc.WriteByte((byte)((nal.Length >> 8) & 0xFF));
                    avcc.WriteByte((byte)(nal.Length & 0xFF));
                    avcc.Write(nal, 0, nal.Length);

                    bool isKey = (nal[0] & 0x1F) == 5;
                    var tag = BuildFlvVideoTag(avcc.ToArray(), isKeyframe: isKey, avcPacketType: 1, timestampMs: timestampMs);
                    if (!_seqHeaderEmitted)
                    {
                        // buffer until sequence header emitted
                        _pendingTags ??= new List<byte[]>();
                        _pendingTags.Add(tag);
                    }
                    else
                    {
                        outList.Add(tag);
                }
            }

            return outList;
        }        private byte[] BuildInitSegment(byte[] sps, byte[] pps)
        {
            // Build AVCDecoderConfigurationRecord
            using var ms = new MemoryStream();
            ms.WriteByte(0x01); // configurationVersion
            // profile, compat, level from SPS first 3 bytes
            if (sps.Length >= 3)
            {
                ms.WriteByte(sps[0]);
                ms.WriteByte(sps[1]);
                ms.WriteByte(sps[2]);
            }
            else
            {
                ms.WriteByte(0);
                ms.WriteByte(0);
                ms.WriteByte(0);
            }
            // lengthSizeMinusOne: 6 bits reserved (111111) + 2 bits lengthSizeMinusOne (3 -> 11)
            ms.WriteByte(0xFF);
            // numOfSPS: 3 bits reserved (111) + 5 bits SPS count (1)
            ms.WriteByte(0xE1);
            // SPS length
            ms.WriteByte((byte)((sps.Length >> 8) & 0xFF));
            ms.WriteByte((byte)(sps.Length & 0xFF));
            ms.Write(sps, 0, sps.Length);
            // PPS
            ms.WriteByte(0x01);
            ms.WriteByte((byte)((pps.Length >> 8) & 0xFF));
            ms.WriteByte((byte)(pps.Length & 0xFF));
            ms.Write(pps, 0, pps.Length);

            var config = ms.ToArray();

            // Build FLV file header + single video tag containing AVC sequence header
            using var outMs = new MemoryStream();
            // FLV header
            outMs.WriteByte((byte)'F'); outMs.WriteByte((byte)'L'); outMs.WriteByte((byte)'V');
            outMs.WriteByte(0x01); // version
            outMs.WriteByte(0x01); // flags: video only
            outMs.Write(new byte[] { 0x00, 0x00, 0x00, 0x09 }, 0, 4);
            // PreviousTagSize0
            outMs.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 }, 0, 4);

            // Build tag header: TagType (video=9) + DataSize (3 bytes)
            byte tagType = 9; // video

            // DataSize = 1 (Frame+Codec) + 4 (AVC header: 1 byte AVCPacketType + 3 bytes composition time) + config length
            int dataSize = 1 + 4 + config.Length;

            outMs.WriteByte(tagType);
            outMs.WriteByte((byte)((dataSize >> 16) & 0xFF));
            outMs.WriteByte((byte)((dataSize >> 8) & 0xFF));
            outMs.WriteByte((byte)(dataSize & 0xFF));

            // Timestamp (3) + TimestampExtended (1)
            outMs.WriteByte(0x00); outMs.WriteByte(0x00); outMs.WriteByte(0x00); outMs.WriteByte(0x00);
            // StreamID
            outMs.Write(new byte[] { 0x00, 0x00, 0x00 }, 0, 3);

            // Tag data: first byte is FrameType+CodecID
            byte frameAndCodec = (byte)((1 << 4) | 7); // keyframe + AVC
            outMs.WriteByte(frameAndCodec);

            // AVC header: AVCPacketType = 0 (sequence header)
            outMs.WriteByte(0x00);
            // CompositionTime 3 bytes
            outMs.WriteByte(0x00); outMs.WriteByte(0x00); outMs.WriteByte(0x00);

            // AVCDecoderConfigurationRecord
            outMs.Write(config, 0, config.Length);

            // PreviousTagSize
            int prevTagSize = 11 + dataSize;
            outMs.WriteByte((byte)((prevTagSize >> 24) & 0xFF));
            outMs.WriteByte((byte)((prevTagSize >> 16) & 0xFF));
            outMs.WriteByte((byte)((prevTagSize >> 8) & 0xFF));
            outMs.WriteByte((byte)(prevTagSize & 0xFF));

            return outMs.ToArray();
        }

        private byte[] BuildFlvVideoTag(byte[] avccPayload, bool isKeyframe, byte avcPacketType, uint timestampMs = 0)
        {
            using var ms = new MemoryStream();

            // TagType (video=9)
            byte tagType = 9;
            // DataSize = 1 (Frame+Codec) + 4 (AVC header: AVCPacketType + composition time 3 bytes) + payload length
            int dataSize = 1 + 4 + avccPayload.Length;

            ms.WriteByte(tagType);
            ms.WriteByte((byte)((dataSize >> 16) & 0xFF));
            ms.WriteByte((byte)((dataSize >> 8) & 0xFF));
            ms.WriteByte((byte)(dataSize & 0xFF));

            // Timestamp (3 bytes) + TimestampExtended (1)
            ms.WriteByte((byte)((timestampMs >> 16) & 0xFF));
            ms.WriteByte((byte)((timestampMs >> 8) & 0xFF));
            ms.WriteByte((byte)(timestampMs & 0xFF));
            ms.WriteByte((byte)((timestampMs >> 24) & 0xFF));
            // StreamID
            ms.Write(new byte[] { 0x00, 0x00, 0x00 }, 0, 3);

            // Tag data: FrameType + CodecID
            byte frameAndCodec = (byte)(((isKeyframe ? 1 : 2) << 4) | 7);
            ms.WriteByte(frameAndCodec);

            // AVC header
            ms.WriteByte(avcPacketType);
            // composition time (3 bytes) - set to 0 (no offset)
            ms.WriteByte(0x00); ms.WriteByte(0x00); ms.WriteByte(0x00);

            // payload (AVCC formatted)
            ms.Write(avccPayload, 0, avccPayload.Length);

            int prevTagSize = 11 + dataSize;
            ms.WriteByte((byte)((prevTagSize >> 24) & 0xFF));
            ms.WriteByte((byte)((prevTagSize >> 16) & 0xFF));
            ms.WriteByte((byte)((prevTagSize >> 8) & 0xFF));
            ms.WriteByte((byte)(prevTagSize & 0xFF));

            return ms.ToArray();
        }
    }
}
