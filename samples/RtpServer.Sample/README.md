# RtpServer.Sample

Minimal ASP.NET Core sample that listens for RTP on UDP and exposes an FLV pull endpoint.

Features:

- Listens for RTP on port 5004 by default (configurable with RTP_PORT environment variable).
- Endpoint: `GET /flv` — returns `Content-Type: video/x-flv` and streams incoming RTP payloads as minimal FLV tags.

Usage

1. Build:

   dotnet build samples/RtpServer.Sample/RtpServer.Sample.csproj

2. Run:

   dotnet run --project samples/RtpServer.Sample/RtpServer.Sample.csproj

3. Test:

- Send RTP packets (UDP) to port 5004; the sample will parse RTP packets and stream payloads.
- Open `http://localhost:5000/flv` in a player that supports FLV over HTTP (or use ffplay):

  ffplay -i http://localhost:5000/flv

Notes

- This sample uses `RtpServer.Flv.RtpToFlvConverter` for building the initial FLV header + tag, then appends minimal tags for subsequent payloads.
- The converter in the library is intentionally minimal and intended for testing and plumbing, not production-grade muxing.
