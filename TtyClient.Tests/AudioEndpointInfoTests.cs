using NAudio.CoreAudioApi;
using TtyClient.Audio;

namespace TtyClient.Tests;

public class AudioEndpointInfoTests
{
    [Fact]
    public void IsLoopback_RenderFlow_ReturnsTrue()
    {
        var endpoint = new AudioEndpointInfo("id-1", "Speakers", DataFlow.Render);

        Assert.True(endpoint.IsLoopback);
    }

    [Fact]
    public void IsLoopback_CaptureFlow_ReturnsFalse()
    {
        var endpoint = new AudioEndpointInfo("id-2", "Microphone", DataFlow.Capture);

        Assert.False(endpoint.IsLoopback);
    }

    [Fact]
    public void DisplayName_RenderFlow_AppendsOutputLoopbackSuffix()
    {
        var endpoint = new AudioEndpointInfo("id-1", "Speakers", DataFlow.Render);

        Assert.Equal("Speakers  —  Output (loopback)", endpoint.DisplayName);
    }

    [Fact]
    public void DisplayName_CaptureFlow_AppendsMicrophoneSuffix()
    {
        var endpoint = new AudioEndpointInfo("id-2", "Microphone", DataFlow.Capture);

        Assert.Equal("Microphone  —  Microphone", endpoint.DisplayName);
    }
}
