// removed System.Net.Http to avoid HttpContent/HttpContext ambiguity
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace RtpServer.Flv
{
    public static class FlvEndpoints
    {
        public static IServiceCollection AddFlvStreaming(this IServiceCollection services)
        {
            services.AddSingleton<RtpStreamingService>();
            return services;
        }

        public static IEndpointRouteBuilder MapFlvStreaming(this IEndpointRouteBuilder endpoints)
        {
            // GET /flv/{id}  - connect as consumer
            endpoints.MapGet("/flv/{id}", async (HttpContext ctx, string id) =>
            {
                var svc = ctx.RequestServices.GetRequiredService<RtpStreamingService>();
                ctx.Response.ContentType = "video/x-flv";
                ctx.Response.StatusCode = 200;

                // write standard FLV header (9 bytes) + PreviousTagSize0 (4 bytes) = 13 bytes
                var flvHeader = new byte[13];
                flvHeader[0] = (byte)'F';
                flvHeader[1] = (byte)'L';
                flvHeader[2] = (byte)'V';
                flvHeader[3] = 0x01; // version
                flvHeader[4] = 0x00; // flags (no audio/video flags by default)
                // DataOffset = 9 (big-endian)
                flvHeader[5] = 0x00;
                flvHeader[6] = 0x00;
                flvHeader[7] = 0x00;
                flvHeader[8] = 0x09;
                // PreviousTagSize0 = 0 (4 bytes)
                flvHeader[9] = 0x00;
                flvHeader[10] = 0x00;
                flvHeader[11] = 0x00;
                flvHeader[12] = 0x00;

                await ctx.Response.Body.WriteAsync(flvHeader, 0, flvHeader.Length, ctx.RequestAborted);
                await ctx.Response.Body.FlushAsync(ctx.RequestAborted);

                try
                {
                    await foreach (var chunk in svc.ConnectAsync(id))
                    {
                        // Write without throwing if the client disconnects; if request is aborted, break out.
                        try
                        {
                            await ctx.Response.Body.WriteAsync(chunk, 0, chunk.Length, ctx.RequestAborted);
                            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // client disconnected while enumerating; just end the request
                }
            });

            // POST /publish/{id} - publish payload
            endpoints.MapPost("/publish/{id}", async (HttpContext ctx, string id) =>
            {
                var svc = ctx.RequestServices.GetRequiredService<RtpStreamingService>();
                using var ms = new System.IO.MemoryStream();
                await ctx.Request.Body.CopyToAsync(ms);
                var data = ms.ToArray();
                // optional query ?audio=1
                var isAudio = ctx.Request.Query.TryGetValue("audio", out var a) && a == "1";
                await svc.PublishAsync(id, data, isAudio);
                ctx.Response.StatusCode = 204;
            });

            return endpoints;
        }
    }
}
