using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Speaking.Evaluations;

namespace TOEICMaster.Infrastructure.Speaking.Evaluations;

/// <summary>16 kHz mono 16-bit PCM WAV audio and its duration.</summary>
public sealed record SpeakingWavChunk(byte[] Wav, double Seconds);

public interface ISpeakingAudioConverter
{
    /// <summary>
    /// Converts a browser recording (WebM/Ogg Opus or MP4 AAC) to WAV chunks short enough for one pronunciation
    /// assessment request each. Returns no chunks when the recording cannot be decoded or is silent-length.
    /// </summary>
    Task<IReadOnlyList<SpeakingWavChunk>> ToWavChunksAsync(byte[] audio, string contentType,
        CancellationToken cancellationToken);
}

public sealed class FfmpegSpeakingAudioConverter(IOptions<AzureSpeechOptions> options) : ISpeakingAudioConverter
{
    private const double MinimumChunkSeconds = 0.3;
    private static readonly TimeSpan ConversionTimeout = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<SpeakingWavChunk>> ToWavChunksAsync(byte[] audio, string contentType,
        CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        var directory = Directory.CreateTempSubdirectory("toeicmaster-speaking-");
        try
        {
            var input = Path.Combine(directory.FullName, "input." + SpeakingMediaRules.Extension(contentType));
            await File.WriteAllBytesAsync(input, audio, cancellationToken);
            var startInfo = new ProcessStartInfo(configuration.FfmpegPath)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            foreach (var argument in new[]
                     {
                         "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-i", input, "-vn", "-ac", "1",
                         "-ar", "16000", "-c:a", "pcm_s16le", "-f", "segment", "-segment_format", "wav",
                         "-segment_time", configuration.ChunkSeconds.ToString(CultureInfo.InvariantCulture),
                         "-reset_timestamps", "1", Path.Combine(directory.FullName, "chunk%03d.wav")
                     })
                startInfo.ArgumentList.Add(argument);

            using var process = StartProcess(startInfo);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ConversionTimeout);
            var standardError = process.StandardError.ReadToEndAsync(timeout.Token);
            var standardOutput = process.StandardOutput.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                await Task.WhenAll(standardError, standardOutput);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                if (cancellationToken.IsCancellationRequested)
                    throw;
                throw new SpeakingEvaluatorException("AUDIO_CONVERSION_TIMEOUT",
                    "Xử lý bản ghi mất nhiều thời gian hơn dự kiến. Hệ thống sẽ thử lại.", true);
            }
            if (process.ExitCode != 0)
                return [];

            var chunks = new List<SpeakingWavChunk>();
            foreach (var file in Directory.GetFiles(directory.FullName, "chunk*.wav").Order(StringComparer.Ordinal))
            {
                var wav = await File.ReadAllBytesAsync(file, cancellationToken);
                var seconds = WavSeconds(wav);
                if (seconds >= MinimumChunkSeconds)
                    chunks.Add(new SpeakingWavChunk(wav, seconds));
            }
            return chunks;
        }
        finally
        {
            try { directory.Delete(recursive: true); }
            catch (IOException) { /* The OS temp cleaner removes leftovers. */ }
            catch (UnauthorizedAccessException) { /* The OS temp cleaner removes leftovers. */ }
        }
    }

    private static Process StartProcess(ProcessStartInfo startInfo)
    {
        try
        {
            return Process.Start(startInfo)
                   ?? throw new SpeakingEvaluatorException("AUDIO_CONVERTER_UNAVAILABLE",
                       "Máy chủ chưa cài công cụ xử lý âm thanh cho chấm AI Speaking.", false);
        }
        catch (Win32Exception exception)
        {
            throw new SpeakingEvaluatorException("AUDIO_CONVERTER_UNAVAILABLE",
                "Máy chủ chưa cài công cụ xử lý âm thanh cho chấm AI Speaking.", false, innerException: exception);
        }
    }

    /// <summary>Duration from the RIFF "fmt " byte rate and "data" chunk size.</summary>
    public static double WavSeconds(ReadOnlySpan<byte> wav)
    {
        if (wav.Length < 12 || !wav[..4].SequenceEqual("RIFF"u8) || !wav.Slice(8, 4).SequenceEqual("WAVE"u8))
            return 0;
        var byteRate = 0;
        var offset = 12;
        while (offset + 8 <= wav.Length)
        {
            var id = wav.Slice(offset, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(wav.Slice(offset + 4, 4));
            var body = offset + 8;
            if (id.SequenceEqual("fmt "u8) && body + 12 <= wav.Length)
                byteRate = BinaryPrimitives.ReadInt32LittleEndian(wav.Slice(body + 8, 4));
            if (id.SequenceEqual("data"u8))
            {
                var dataBytes = Math.Min(size, (uint)(wav.Length - body));
                return byteRate > 0 ? dataBytes / (double)byteRate : 0;
            }
            offset = body + (int)Math.Min(size + (size & 1), (uint)(wav.Length - body));
        }
        return 0;
    }
}
