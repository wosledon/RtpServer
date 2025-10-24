using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RtpServer.Flv;
using Xunit;

namespace RtpServer.Tests
{
    public class FlvStreamingTests
    {
        [Fact]
        public async Task FlvStream_GetReceivesHeaderAndTag_WhenPublished()
        {
            var builder = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    // required for endpoint routing used in the test app
                    services.AddRouting();
                    services.AddFlvStreaming();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapFlvStreaming());
                });

            using var server = new TestServer(builder);
            using var client = server.CreateClient();

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // publish an RTP payload via POST first so the channel already has data when a client connects
            var payload = new byte[] { 0x11, 0x22, 0x33 };
            using var pubClient = server.CreateClient();
            using var content = new ByteArrayContent(payload);
            var pub = await pubClient.PostAsync("/publish/test1?audio=1", content, cts.Token);
            Assert.Equal(System.Net.HttpStatusCode.NoContent, pub.StatusCode);

            // now connect as a consumer and read header + tag
            var streamTask = Task.Run(async () =>
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "/flv/test1");
                var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                resp.EnsureSuccessStatusCode();
                var s = await resp.Content.ReadAsStreamAsync(cts.Token);
                // read FLV header (13 bytes)
                var header = new byte[13];
                int read = 0;
                while (read < header.Length)
                {
                    var r = await s.ReadAsync(header, read, header.Length - read, cts.Token);
                    if (r == 0) break;
                    read += r;
                }
                return (header, s);
            }, cts.Token);

            var (hdr, stream) = await streamTask;
            Assert.Equal((byte)'F', hdr[0]);
            Assert.Equal((byte)'L', hdr[1]);
            Assert.Equal((byte)'V', hdr[2]);

            // try to read some tag bytes (should arrive after publish)
            var buf = new byte[32];
            int got = await stream.ReadAsync(buf, 0, buf.Length, cts.Token);
            Assert.True(got > 0);
        }
    }
}
