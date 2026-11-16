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

    public PortkeyChatClient(string endpointUrl, string apiKey, string provider, string model, string systemPrompt)
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
    }

    public async Task<string> ImproveTextAsync(string originalText, CancellationToken cancellationToken)
    {
        ChatCompletion completion = await _chatClient.CompleteChatAsync(
            [new SystemChatMessage(_systemPrompt), new UserChatMessage(originalText)],
            cancellationToken: cancellationToken);

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
            Process(message, pipeline, index);
            return ValueTask.CompletedTask;
        }
    }
}
