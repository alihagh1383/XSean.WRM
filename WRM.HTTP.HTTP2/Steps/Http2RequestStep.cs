using WRM.HTTP.HTTP2.Connection;
using WRM.HTTP.HTTP2.Streams;
using WRM.Interface;

namespace WRM.HTTP.HTTP2.Steps;

public class Http2RequestStep : IPipelineStep
{
    public async Task InvokeAsync(NetworkContext ctx, Func<NetworkContext, Task> next)
    {
        // بررسی که آیا HTTP/2 connection داریم
        if (!ctx.Items.TryGetValue("http2", out var connObj) || connObj is not Http2Connection connection)
        {
            await next(ctx);
            return;
        }

        foreach (var streamKvp in connection.Streams)
        {
            var stream = streamKvp.Value;

            if (!stream.HeadersReceived
                || stream is { EndStreamReceived: false, Request.Body: not null }
                || stream.ResponseSent) continue;
            ctx.Items["HTTP_CONTEXT"] = stream;
            await ProcessStreamAsync(connection, stream, ctx);
            await next(ctx);
            stream.ResponseSent = true;
        }
    }

    private async Task ProcessStreamAsync(Http2Connection connection, Http2Stream stream, NetworkContext ctx)
    {
        try
        {
            Console.WriteLine($"Processing stream {stream.Id}: {stream.Request?.Method} {stream.Request?.Path}");

            // ساخت response headers
            var responseHeaders = new System.Collections.Generic.List<(string, string)>
            {
                (":status", "200"),
                ("content-type", "text/plain; charset=utf-8"),
                ("server", "WRM-HTTP2/1.0")
            };

            // ساخت response body
            string responseBody = $"Hello from HTTP/2!\n\n" +
                                  $"Method: {stream.Request?.Method}\n" +
                                  $"Path: {stream.Request?.Path}\n" +
                                  $"Stream ID: {stream.Id}\n\n" +
                                  $"Headers:\n";

            if (stream.Request?.Headers != null)
            {
                foreach (var header in stream.Request.Headers)
                {
                    responseBody += $"  {header.Key}: {header.Value}\n";
                }
            }

            byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(responseBody);
            responseHeaders.Add(("content-length", bodyBytes.Length.ToString()));

            // ارسال HEADERS frame
            var headersFrame = new Frames.HeadersFrame
            {
                Headers = responseHeaders,
                EndHeaders = true,
                EndStream = false // چون بعدش DATA می‌فرستیم
            };

            await connection.Writer!.WriteFrameAsync(
                headersFrame.ToFrame(stream.Id, connection.Encoder)
            );

            // ارسال DATA frame
            var dataFrame = new Frames.DataFrame
            {
                Data = bodyBytes,
                EndStream = true
            };

            await connection.Writer.WriteFrameAsync(
                dataFrame.ToFrame(stream.Id)
            );

            // آپدیت stream state
            stream.State = Http2StreamState.Closed;

            Console.WriteLine($"Successfully sent response for stream {stream.Id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing stream {stream.Id}: {ex.Message}");

            // ارسال error response
            try
            {
                var errorHeaders = new System.Collections.Generic.List<(string, string)>
                {
                    (":status", "500"),
                    ("content-type", "text/plain")
                };

                var headersFrame = new Frames.HeadersFrame
                {
                    Headers = errorHeaders,
                    EndHeaders = true,
                    EndStream = true
                };

                await connection.Writer!.WriteFrameAsync(
                    headersFrame.ToFrame(stream.Id, connection.Encoder)
                );
            }
            catch
            {
                // اگه ارسال error response هم fail شد، RST_STREAM بفرستیم
                var rstFrame = new Frames.RstStreamFrame
                {
                    ErrorCode = Frames.RstStreamFrame.INTERNAL_ERROR
                };

                await connection.Writer!.WriteFrameAsync(rstFrame.ToFrame(stream.Id));
            }
        }
    }
}