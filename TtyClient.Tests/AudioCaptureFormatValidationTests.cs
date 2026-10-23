using NAudio.Wave;
using TtyClient.Audio;

namespace TtyClient.Tests;

public class AudioCaptureFormatValidationTests
{
    [Fact]
    public void EnsureSupportedFormat_IeeeFloat32_DoesNotThrow()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

        var exception = Record.Exception(() => AudioCapture.EnsureSupportedFormat(format, "Test Device"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureSupportedFormat_Extensible32Bit_DoesNotThrow()
    {
        var format = new WaveFormatExtensible(44100, 32, 2);

        var exception = Record.Exception(() => AudioCapture.EnsureSupportedFormat(format, "Test Device"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureSupportedFormat_Pcm16Bit_ThrowsNotSupportedExceptionNamingDevice()
    {
        var format = new WaveFormat(44100, 16, 2);

        var exception = Assert.Throws<NotSupportedException>(
            () => AudioCapture.EnsureSupportedFormat(format, "Realtek Microphone"));

        Assert.Contains("Realtek Microphone", exception.Message);
        Assert.Contains("32-bit floating point", exception.Message);
    }
}
