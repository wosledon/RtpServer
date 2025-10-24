using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace RtpServerApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            int port = 5004;
            if (args.Length > 0 && int.TryParse(args[0], out var p)) port = p;

            using var serviceProvider = new ServiceCollection()
                .AddLogging(builder => builder.AddSimpleConsole(options => { options.SingleLine = true; }))
                .BuildServiceProvider();

            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("RtpServerApp");

            using var server = new RtpServer.RtpServer(port, logger);
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

            logger.LogInformation("Starting RTP server on UDP port {Port}. Press Ctrl+C to stop.", port);
            await server.StartAsync(cts.Token);
            logger.LogInformation("Server stopped.");
        }
    }
}
