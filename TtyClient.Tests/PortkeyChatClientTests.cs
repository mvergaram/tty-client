using System.ClientModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TtyClient.AI;

namespace TtyClient.Tests;

public class PortkeyChatClientTests
{
    private const string SuccessBodyTemplate =
        "{{\"choices\":[{{\"index\":0,\"message\":{{\"role\":\"assistant\",\"content\":\"{0}\"}},\"finish_reason\":\"stop\"}}]}}";

    private static int GetFreeLoopbackPort()
    {
        var tcpListener = new TcpListener(IPAddress.Loopback, 0);
        tcpListener.Start();
        var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
        tcpListener.Stop();
        return port;
    }

    private static HttpListener StartListener(out string endpointUrl)
    {
        var port = GetFreeLoopbackPort();
        endpointUrl = $"http://127.0.0.1:{port}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(endpointUrl);
        listener.Start();
        return listener;
    }

    private static void RespondWith(HttpListenerContext context, int statusCode, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.OutputStream.Close();
    }

    [Theory]
    [InlineData("dsvertex", "@dsvertex")]
    [InlineData("@dsvertex", "@dsvertex")]
    public async Task Constructor_ProviderHeader_NormalizesLeadingAtSign(string provider, string expectedHeader)
    {
        var listener = StartListener(out var endpointUrl);
        try
        {
            string? receivedProviderHeader = null;
            string? receivedApiKeyHeader = null;
            var serverTask = Task.Run(() =>
            {
                var context = listener.GetContext();
                receivedProviderHeader = context.Request.Headers["x-portkey-provider"];
                receivedApiKeyHeader = context.Request.Headers["x-portkey-api-key"];
                RespondWith(context, 200, string.Format(SuccessBodyTemplate, "ok"));
            });

            var client = new PortkeyChatClient(endpointUrl, "secret-key", provider, "test-model", "system prompt", AiDefaults.MaxOutputTokens);
            await client.ImproveTextAsync("hello", CancellationToken.None);
            await serverTask;

            Assert.Equal(expectedHeader, receivedProviderHeader);
            Assert.Equal("secret-key", receivedApiKeyHeader);
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }

    [Fact]
    public async Task ImproveTextAsync_SuccessResponse_ReturnsTrimmedContent()
    {
        var listener = StartListener(out var endpointUrl);
        try
        {
            var serverTask = Task.Run(() =>
            {
                var context = listener.GetContext();
                RespondWith(context, 200, string.Format(SuccessBodyTemplate, "  Texto mejorado  "));
            });

            var client = new PortkeyChatClient(endpointUrl, "key", "dsvertex", "test-model", "system prompt", AiDefaults.MaxOutputTokens);
            var result = await client.ImproveTextAsync("hello", CancellationToken.None);
            await serverTask;

            Assert.Equal("Texto mejorado", result);
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }

    [Fact]
    public async Task ImproveTextAsync_NonSuccessStatus_ThrowsClientResultExceptionWithStatus()
    {
        var listener = StartListener(out var endpointUrl);
        try
        {
            var serverTask = Task.Run(() =>
            {
                var context = listener.GetContext();
                RespondWith(context, 401, "{\"error\":{\"message\":\"unauthorized\"}}");
            });

            var client = new PortkeyChatClient(endpointUrl, "key", "dsvertex", "test-model", "system prompt", AiDefaults.MaxOutputTokens);

            var exception = await Assert.ThrowsAsync<ClientResultException>(
                () => client.ImproveTextAsync("hello", CancellationToken.None));
            await serverTask;

            Assert.Equal(401, exception.Status);
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }

    [Fact]
    public async Task ImproveTextAsync_UnrecognizedFinishReason_ThrowsFriendlyInvalidOperationException()
    {
        var listener = StartListener(out var endpointUrl);
        try
        {
            const string body = "{\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"\"},\"finish_reason\":\"refusal\"}]}";
            var serverTask = Task.Run(() =>
            {
                var context = listener.GetContext();
                RespondWith(context, 200, body);
            });

            var client = new PortkeyChatClient(endpointUrl, "key", "dsvertex", "test-model", "system prompt", AiDefaults.MaxOutputTokens);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.ImproveTextAsync("hello", CancellationToken.None));
            await serverTask;

            Assert.Contains("refusal", exception.Message);
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }

    [Fact]
    public async Task ImproveTextAsync_AlreadyCancelledToken_ThrowsOperationCanceledException()
    {
        var listener = StartListener(out var endpointUrl);
        try
        {
            var client = new PortkeyChatClient(endpointUrl, "key", "dsvertex", "test-model", "system prompt", AiDefaults.MaxOutputTokens);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => client.ImproveTextAsync("hello", cts.Token));
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }

    [Fact]
    public void Constructor_InvalidEndpointUrl_ThrowsUriFormatException()
    {
        Assert.Throws<UriFormatException>(
            () => new PortkeyChatClient("not a url", "key", "dsvertex", "test-model", "system prompt", AiDefaults.MaxOutputTokens));
    }

    [Fact]
    public async Task ImproveTextAsync_DoesNotBlockCallingThread_WhileHttpRequestIsInFlight()
    {
        // Regression test for the UI freeze bug: HeaderPolicy.ProcessAsync used to delegate to
        // the synchronous pipeline chain (Process), which forces the real HTTP transport to send
        // the request synchronously and blocks whichever thread called ImproveTextAsync until the
        // full round-trip completes. On the fixed async chain, the call returns control to the
        // caller almost immediately and only the awaited Task completes once the response arrives.
        var listener = StartListener(out var endpointUrl);
        try
        {
            using var releaseResponse = new ManualResetEventSlim(false);
            var serverTask = Task.Run(() =>
            {
                var context = listener.GetContext();
                releaseResponse.Wait(TimeSpan.FromSeconds(5)); // deliberately slow server
                RespondWith(context, 200, string.Format(SuccessBodyTemplate, "ok"));
            });

            var client = new PortkeyChatClient(endpointUrl, "key", "dsvertex", "test-model", "system prompt", AiDefaults.MaxOutputTokens);

            var stopwatch = Stopwatch.StartNew();
            var improveTask = client.ImproveTextAsync("hello", CancellationToken.None);
            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1),
                $"ImproveTextAsync blocked its caller for {stopwatch.Elapsed} instead of returning immediately");

            releaseResponse.Set();
            var result = await improveTask;
            await serverTask;

            Assert.Equal("ok", result);
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }
}
