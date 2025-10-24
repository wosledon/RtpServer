using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RtpServer.Flv
{
    /// <summary>
    /// Simple in-memory hub for FLV streaming: publishers push RTP payloads, consumers connect to receive FLV stream.
    /// This is intentionally minimal for demos/tests.
    /// </summary>
    public class RtpStreamingService
    {
        private readonly ConcurrentDictionary<string, Channel<byte[]>> _channels = new();

        private Channel<byte[]> GetOrCreate(string id)
        {
            return _channels.GetOrAdd(id, _ => Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false }));
        }

        /// <summary>
        /// Publish an RTP payload (already depacketized) to all connected consumers for the stream id.
        /// </summary>
        public ValueTask PublishAsync(string id, byte[] rtpPayload, bool isAudio = true)
        {
            var ch = GetOrCreate(id);
            var flv = RtpToFlvConverter.ConvertRtpPayloadToFlvTag(rtpPayload, isAudio);
            // try to write, if can't, write async (rare for unbounded)
            if (!ch.Writer.TryWrite(flv))
            {
                return ch.Writer.WriteAsync(flv);
            }
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Connect to a stream and receive FLV chunks as an async enumerable.
        /// First yielded item will be the FLV header+previousTagSize0 (13 bytes). Subsequent items are tag bytes.
        /// </summary>
        public async IAsyncEnumerable<byte[]> ConnectAsync(string id)
        {
            var ch = GetOrCreate(id);

            // yield header (FLV header + PreviousTagSize0)
            var header = CreateFlvHeader();
            yield return header;

            await foreach (var chunk in ch.Reader.ReadAllAsync())
            {
                yield return chunk;
            }
        }

        private static byte[] CreateFlvHeader()
        {
            // 'FLV' + version + flags + data offset (9) + prevTagSize0 (0)
            return new byte[] { (byte)'F', (byte)'L', (byte)'V', 0x01, 0x05, 0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0x00, 0x00 };
        }
    }
}
