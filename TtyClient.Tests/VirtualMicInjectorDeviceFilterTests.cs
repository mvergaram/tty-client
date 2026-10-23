using TtyClient.Audio;

namespace TtyClient.Tests;

public class VirtualMicInjectorDeviceFilterTests
{
    [Theory]
    [InlineData("CABLE Input (VB-Audio Virtual Cable)")]
    [InlineData("CABLE Output (VB-Audio Virtual Cable)")]
    [InlineData("cable input")]
    [InlineData("Virtual Audio Device")]
    [InlineData("VB-Audio Point")]
    public void IsVirtualAudioDeviceName_VirtualDeviceNames_ReturnsTrue(string name)
    {
        Assert.True(VirtualMicInjector.IsVirtualAudioDeviceName(name));
    }

    [Theory]
    [InlineData("Realtek High Definition Audio")]
    [InlineData("Speakers (Realtek)")]
    [InlineData("Headset Microphone")]
    public void IsVirtualAudioDeviceName_RealHardwareDeviceNames_ReturnsFalse(string name)
    {
        Assert.False(VirtualMicInjector.IsVirtualAudioDeviceName(name));
    }
}
