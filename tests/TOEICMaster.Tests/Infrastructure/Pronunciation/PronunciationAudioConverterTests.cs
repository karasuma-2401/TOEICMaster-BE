using System.Buffers.Binary;
using Microsoft.Extensions.Options;
using TOEICMaster.Infrastructure.Pronunciation;

namespace TOEICMaster.Tests.Infrastructure.Pronunciation;

public sealed class PronunciationAudioConverterTests
{
    private static byte[] Wav(int sampleRate, ushort channels, ushort bits, byte[] samples, bool extraChunk = false)
    {
        var chunks = new List<byte>();
        chunks.AddRange("WAVE"u8.ToArray());
        if (extraChunk)
        {
            chunks.AddRange("LIST"u8.ToArray());
            chunks.AddRange(BitConverter.GetBytes(3u));
            chunks.AddRange(new byte[] { 1, 2, 3, 0 }); // odd size plus pad byte
        }
        var format = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(format, 1);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(2), channels);
        BinaryPrimitives.WriteUInt32LittleEndian(format.AsSpan(4), (uint)sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(format.AsSpan(8), (uint)(sampleRate * channels * bits / 8));
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(12), (ushort)(channels * bits / 8));
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(14), bits);
        chunks.AddRange("fmt "u8.ToArray());
        chunks.AddRange(BitConverter.GetBytes(16u));
        chunks.AddRange(format);
        chunks.AddRange("data"u8.ToArray());
        chunks.AddRange(BitConverter.GetBytes((uint)samples.Length));
        chunks.AddRange(samples);
        return [.. "RIFF"u8.ToArray(), .. BitConverter.GetBytes((uint)chunks.Count), .. chunks];
    }

    [Fact]
    public void Reads_samples_of_16k_mono_pcm_even_after_other_chunks()
    {
        var samples = Enumerable.Range(0, 64).Select(index => (byte)index).ToArray();

        Assert.True(FfmpegPronunciationAudioConverter.TryReadPcm16Mono16k(Wav(16000, 1, 16, samples, extraChunk: true),
            out var pcm));
        Assert.Equal(samples, pcm);
    }

    [Theory]
    [InlineData(44100, 1, 16)]
    [InlineData(16000, 2, 16)]
    [InlineData(16000, 1, 8)]
    public void Other_wav_layouts_are_left_to_ffmpeg(int sampleRate, ushort channels, ushort bits) =>
        Assert.False(FfmpegPronunciationAudioConverter.TryReadPcm16Mono16k(Wav(sampleRate, channels, bits, [1, 2]),
            out _));

    [Fact]
    public void Truncated_or_non_wav_input_is_rejected()
    {
        var wav = Wav(16000, 1, 16, new byte[100]);
        Assert.False(FfmpegPronunciationAudioConverter.TryReadPcm16Mono16k(wav[..60], out _));
        Assert.False(FfmpegPronunciationAudioConverter.TryReadPcm16Mono16k([0x1a, 0x45, 0xdf, 0xa3], out _));
    }

    [Fact]
    public async Task Ready_wav_passes_through_without_ffmpeg_with_its_duration()
    {
        // A missing ffmpeg binary proves the passthrough path never starts a process.
        var converter = new FfmpegPronunciationAudioConverter(Options.Create(new PronunciationSpeechOptions
        {
            FfmpegPath = "/nonexistent/ffmpeg"
        }));
        var samples = new byte[32000]; // one second

        var result = await converter.ToWavAsync(Wav(16000, 1, 16, samples, extraChunk: true), "audio/wav",
            CancellationToken.None);

        Assert.Equal(1, result.Seconds);
        Assert.Equal(44 + samples.Length, result.Wav.Length);
        Assert.True(FfmpegPronunciationAudioConverter.TryReadPcm16Mono16k(result.Wav, out var pcm));
        Assert.Equal(samples.Length, pcm.Length);
    }
}
