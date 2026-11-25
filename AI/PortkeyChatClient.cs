using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Chat;

namespace TtyClient.AI;

/// <summary>
/// Chat client routed through Portkey's gateway, following Portkey's official C# SDK pattern:
/// the OpenAI client is pointed at Portkey's endpoint and a pipeline policy injects Portkey's
/// own headers instead of a real OpenAI API key (see
/// https://portkey.ai/docs/api-reference/sdk/c-sharp.md).
/// </summary>
public sealed class PortkeyChatClient
{
    private readonly ChatClient _chatClient;
    private readonly string _systemPrompt;
    private readonly int _maxOutputTokens;

    public PortkeyChatClient(string endpointUrl, string apiKey, string provider, string model, string systemPrompt, int maxOutputTokens)
    {
        var headers = new Dictionary<string, string>
        {
            ["x-portkey-api-key"] = apiKey,
            ["x-portkey-provider"] = "@" + provider.TrimStart('@'),
        };

        var options = new OpenAIClientOptions { Endpoint = new Uri(endpointUrl) };
        options.AddPolicy(new HeaderPolicy(headers), PipelinePosition.PerCall);

        _chatClient = new OpenAIClient(new ApiKeyCredential("dummy"), options).GetChatClient(model);
        _systemPrompt = systemPrompt;
        _maxOutputTokens = maxOutputTokens;
    }

    public async Task<string> ImproveTextAsync(string originalText, CancellationToken cancellationToken)
    {
        var options = new ChatCompletionOptions { MaxOutputTokenCount = _maxOutputTokens };

        ChatCompletion completion;
        try
        {
            completion = await _chatClient.CompleteChatAsync(
                [new SystemChatMessage(_systemPrompt), new UserChatMessage(originalText)],
                options,
                cancellationToken);
        }
        catch (ArgumentOutOfRangeException ex) when (ex.Message.StartsWith("Unknown ChatFinishReason value.", StringComparison.Ordinal))
        {
            // OpenAI.Chat.ChatFinishReason only knows OpenAI's own 5 values (stop, length,
            // tool_calls, content_filter, function_call). Some models behind Portkey (e.g.
            // Anthropic models, which can report stop_reason "refusal" when a safety
            // classifier halts generation) return a finish reason the SDK doesn't recognize,
            // and it throws while parsing the response instead of returning a ChatCompletion.
            // Surface that as a readable message instead of the raw SDK parsing exception.
            throw new InvalidOperationException($"the model declined to respond (reason: {ex.ActualValue})", ex);
        }

        return completion.Content[0].Text.Trim();
    }

    private sealed class HeaderPolicy(Dictionary<string, string> headers) : PipelinePolicy
    {
        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int index)
        {
            foreach (var header in headers)
                message.Request.Headers.Set(header.Key, header.Value);
            if (index < pipeline.Count)
                pipeline[index].Process(message, pipeline, index + 1);
        }

        public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int index)
        {
            // Must continue through the async chain (ProcessAsync), not the sync one
            // (Process): delegating to the sync chain here forces the real HTTP
            // transport to send the request synchronously, blocking whichever thread
            // called ImproveTextAsync (the WPF UI thread) for the whole round-trip.
            foreach (var header in headers)
                message.Request.Headers.Set(header.Key, header.Value);
            return index < pipeline.Count
                ? pipeline[index].ProcessAsync(message, pipeline, index + 1)
                : ValueTask.CompletedTask;
        }
    }
}
