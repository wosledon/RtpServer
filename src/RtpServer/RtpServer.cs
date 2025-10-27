using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RtpServer
{
    public sealed class RtpServer : IDisposable
    {
        /// <summary>
        /// Event raised when a parsed RTP packet has been received.
        /// Handlers are invoked on the worker thread — keep handlers fast.
        /// </summary>
        public event EventHandler<RtpPacket?>? PacketReceived;

        private readonly int _port;
        private readonly ILogger _logger;
        private Socket _socket = null!;
        private Socket _rtcpSocket = null!;
        private CancellationTokenSource? _cts;
        private Channel<(byte[] data, int length, EndPoint remote)> _channel = Channel.CreateBounded<(byte[] data, int length, EndPoint remote)>(1024);
        private Task? _receiverTask;
        private Task? _rtcpTask;
        private Task[]? _workers;

        public RtpServer(int port, ILogger? logger = null)
        {
            _port = port;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(new IPEndPoint(IPAddress.Any, _port));

            _rtcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _rtcpSocket.Bind(new IPEndPoint(IPAddress.Any, _port + 1));

            _receiverTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
            _rtcpTask = Task.Run(() => RtcpListenerAsync(_cts.Token));

            int workerCount = Math.Max(1, Environment.ProcessorCount / 2);
            _workers = new Task[workerCount];
            for (int i = 0; i < workerCount; i++) _workers[i] = Task.Run(() => WorkerLoopAsync(_cts.Token));

            // wait until cancellation
            try
            {
                await Task.Delay(Timeout.Infinite, _cts.Token);
            }
            catch (OperationCanceledException) { }

            _logger.LogInformation("Stopping server, waiting for tasks to complete...");
            _channel.Writer.Complete();
            await Task.WhenAll(_workers!);
            _socket.Dispose();
            _rtcpSocket.Dispose();
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[2048];
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var receiveResult = await _socket.ReceiveMessageFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, new IPEndPoint(IPAddress.Any, 0));
                    var copy = new byte[receiveResult.ReceivedBytes];
                    Array.Copy(buffer, 0, copy, 0, receiveResult.ReceivedBytes);
                    await _channel.Writer.WriteAsync((copy, receiveResult.ReceivedBytes, receiveResult.RemoteEndPoint), ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ReceiveLoop error");
                    await Task.Delay(100, ct);
                }
            }
        }

        private async Task WorkerLoopAsync(CancellationToken ct)
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    var pkt = RtpPacket.Parse(item.data, 0, item.length);
                    _logger.LogDebug("Received RTP packet: ssrc={Ssrc} seq={Seq} ts={Ts} payload={PayloadLen}", pkt.Ssrc, pkt.SequenceNumber, pkt.Timestamp, pkt.Payload?.Length ?? 0);
                    // Notify external subscribers
                    try
                    {
                        PacketReceived?.Invoke(this, pkt);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "PacketReceived handler threw an exception");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse RTP packet");
                }
            }
        }

        private async Task RtcpListenerAsync(CancellationToken ct)
        {
            var buffer = new byte[2048];
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var receiveResult = await _rtcpSocket.ReceiveMessageFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, new IPEndPoint(IPAddress.Any, 0));
                    var data = buffer.AsSpan(0, receiveResult.ReceivedBytes).ToArray();
                    var rtcp = RtcpPacket.Parse(data, 0, data.Length);
                    _logger.LogInformation("Received RTCP packet with {Count} report blocks", rtcp.ReportBlocks?.Length ?? 0);

                    // simple echo: send a RR back
                    var rr = RtcpBuilder.BuildReceiverReport(rtcp.SenderSsrc, rtcp.ReportBlocks);
                    await _rtcpSocket.SendToAsync(new ArraySegment<byte>(rr), SocketFlags.None, receiveResult.RemoteEndPoint);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RTCP listener error");
                    await Task.Delay(100, ct);
                }
            }
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            _socket?.Dispose();
            _rtcpSocket?.Dispose();
        }
    }
}
