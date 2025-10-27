using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using RtpServer;
using RtpServer.Flv;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Configurable RTP port (default 5004)
var rtpPort = int.TryParse(Environment.GetEnvironmentVariable("RTP_PORT"), out var p) ? p : 5004;

var broadcaster = new PayloadBroadcaster();
var cts = new CancellationTokenSource();

// Start UDP listener in background
_ = Task.Run(() => UdpListenerLoop(rtpPort, broadcaster, cts.Token));

app.MapGet("/", () => Results.Redirect("/flv"));

app.MapGet("/flv", async (HttpContext ctx) =>
{
    ctx.Response.Headers["Cache-Control"] = "no-cache";
    ctx.Response.ContentType = "video/x-flv";

    var ct = ctx.RequestAborted;
    // Subscribe for payloads
    var sub = broadcaster.Subscribe();
    try
    {
        var first = true;
    var reader = sub.Reader;
    await foreach (var payload in reader.ReadAllAsync(ct))
        {
            if (first)
            {
                // Use library helper to build a minimal FLV file for the first fragment
                var flv = RtpToFlvConverter.ConvertRtpPayloadToFlvTag(payload, isAudio: false);
                await ctx.Response.Body.WriteAsync(flv, ct);
                first = false;
            }
            else
            {
                // Append only a single tag (no header) for subsequent payloads
                var tag = BuildFlvTag(payload, isAudio: false);
                await ctx.Response.Body.WriteAsync(tag, ct);
            }

            await ctx.Response.Body.FlushAsync(ct);
        }
    }
    catch (OperationCanceledException) { }
    finally
    {
        broadcaster.Unsubscribe(sub);
    }
});

app.Lifetime.ApplicationStopping.Register(() => {
    try { cts.Cancel(); } catch { }
});

app.Run();

// UDP listener: receive RTP packets and publish their payload to subscribers
static async Task UdpListenerLoop(int port, PayloadBroadcaster broadcaster, CancellationToken ct)
{
    using var udp = new UdpClient(port);
    try
    {
        while (!ct.IsCancellationRequested)
        {
            var res = await udp.ReceiveAsync(ct);
            try
            {
                var pkt = RtpPacket.Parse(res.Buffer, 0, res.Buffer.Length);
                if (pkt.Payload != null && pkt.Payload.Length > 0)
                {
                    broadcaster.Broadcast(pkt.Payload);
                }
            }
            catch (Exception)
            {
                // ignore parse errors for robustness
            }
        }
    }
    catch (OperationCanceledException) { }
}

// Build a single FLV tag block (tag header + data + previousTagSize)
static byte[] BuildFlvTag(byte[] payload, bool isAudio)
{
    using var ms = new MemoryStream();
    byte tagType = (byte)(isAudio ? 8 : 9);
    ms.WriteByte(tagType);

    int dataSize = payload.Length;
    ms.WriteByte((byte)((dataSize >> 16) & 0xFF));
    ms.WriteByte((byte)((dataSize >> 8) & 0xFF));
    ms.WriteByte((byte)(dataSize & 0xFF));

    uint timestamp = 0;
    ms.WriteByte((byte)((timestamp >> 16) & 0xFF));
    ms.WriteByte((byte)((timestamp >> 8) & 0xFF));
    ms.WriteByte((byte)(timestamp & 0xFF));
    ms.WriteByte((byte)((timestamp >> 24) & 0xFF));

    // StreamID (3 bytes)
    ms.Write(new byte[] { 0x00, 0x00, 0x00 }, 0, 3);

    // Tag data
    ms.Write(payload, 0, payload.Length);

    // PreviousTagSize (4 bytes) = 11 + dataSize
    int prevTagSize = 11 + dataSize;
    ms.WriteByte((byte)((prevTagSize >> 24) & 0xFF));
    ms.WriteByte((byte)((prevTagSize >> 16) & 0xFF));
    ms.WriteByte((byte)((prevTagSize >> 8) & 0xFF));
    ms.WriteByte((byte)(prevTagSize & 0xFF));

    return ms.ToArray();
}

// Simple broadcaster that supports multiple subscribers (fan-out)
class PayloadBroadcaster
{
    private readonly ConcurrentDictionary<int, Channel<byte[]>> _subs = new();
    private int _idCounter = 0;

    public (int Id, ChannelReader<byte[]> Reader) Subscribe()
    {
        var id = Interlocked.Increment(ref _idCounter);
        var ch = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
        _subs[id] = ch;
        return (id, ch.Reader);
    }

    public void Unsubscribe((int Id, ChannelReader<byte[]> Reader) sub)
    {
        if (_subs.TryRemove(sub.Id, out var ch))
        {
            ch.Writer.TryComplete();
        }
    }

    public void Broadcast(byte[] data)
    {
        foreach (var kv in _subs)
        {
            var w = kv.Value.Writer;
            // try write without blocking; drop if can't
            _ = w.TryWrite(data);
        }
    }
}
