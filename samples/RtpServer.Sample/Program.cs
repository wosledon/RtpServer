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

// Start library RtpServer and subscribe to parsed packets
var rtpServer = new RtpServer.RtpServer(rtpPort);
// converters per SSRC
var converters = new System.Collections.Concurrent.ConcurrentDictionary<uint, RtpServer.Flv.H264RtpToFlvConverter>();
uint latestSsrc = 0;
var loggerFactory = app.Services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();

rtpServer.PacketReceived += (s, pkt) =>
{
    try
    {
        if (pkt == null) return;
        app.Logger.LogDebug("PacketReceived ssrc={Ssrc} seq={Seq} ts={Ts} payloadLen={Len}", pkt.Ssrc, pkt.SequenceNumber, pkt.Timestamp, pkt.Payload?.Length ?? 0);
        var conv = converters.GetOrAdd(pkt.Ssrc, _ => {
            latestSsrc = pkt.Ssrc;
            var log = loggerFactory.CreateLogger($"H264Conv-{pkt.Ssrc}");
            return new RtpServer.Flv.H264RtpToFlvConverter(log);
        });
        latestSsrc = pkt.Ssrc;
        foreach (var flv in conv.ProcessRtpPacket(pkt))
        {
            broadcaster.Broadcast(flv);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Exception in PacketReceived handler");
    }
};
_ = Task.Run(() => rtpServer.StartAsync(cts.Token));

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
    // send init segment for latest SSRC if available
    if (latestSsrc != 0 && converters.TryGetValue(latestSsrc, out var latestConv) && latestConv.InitSegment != null)
    {
        await ctx.Response.Body.WriteAsync(latestConv.InitSegment, ct);
        await ctx.Response.Body.FlushAsync(ct);
        first = false; // we already wrote initialization
    }

    await foreach (var payload in reader.ReadAllAsync(ct))
        {
            if (first)
            {
                // first live fragment (if we didn't already send init segment) may be a sequence header produced by converter
                await ctx.Response.Body.WriteAsync(payload, ct);
                first = false;
            }
            else
            {
                // payload coming from converter already contains a full FLV tag; write as-is
                await ctx.Response.Body.WriteAsync(payload, ct);
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

// debug endpoint: return latest init segment for current latest SSRC
app.MapGet("/flv/init", (HttpContext ctx) =>
{
    if (latestSsrc == 0) return Results.NotFound();
    if (!converters.TryGetValue(latestSsrc, out var conv)) return Results.NotFound();
    if (conv.InitSegment == null) return Results.NotFound();
    return Results.File(conv.InitSegment, "application/octet-stream", fileDownloadName: $"init-{latestSsrc}.flv");
});

app.Lifetime.ApplicationStopping.Register(() => {
    try { cts.Cancel(); } catch { }
    try { rtpServer.Dispose(); } catch { }
});

app.Run();

// NOTE: UDP listening is handled by RtpServer internally; we subscribe to PacketReceived event above.

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
