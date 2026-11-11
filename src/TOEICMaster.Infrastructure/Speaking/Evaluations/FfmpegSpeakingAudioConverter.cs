using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
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
    private const int CutSearchSeconds = 5;
    private const int WavHeaderBytes = 44;
    private const string UndecodableInput = "Invalid data found";
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
            var output = Path.Combine(directory.FullName, "audio.wav");
            var startInfo = new ProcessStartInfo(configuration.FfmpegPath)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            foreach (var argument in new[]
                     {
                         "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-i", input, "-vn", "-ac", "1",
                         "-ar", "16000", "-c:a", "pcm_s16le", "-f", "wav", output
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
            {
                // Only a recording ffmpeg reports as undecodable is final; anything else may succeed on a retry.
                if ((await standardError).Contains(UndecodableInput, StringComparison.OrdinalIgnoreCase))
                    return [];
                throw new SpeakingEvaluatorException("AUDIO_CONVERSION_FAILED",
                    "Chưa xử lý được bản ghi. Hệ thống sẽ thử lại.", true);
            }

            return File.Exists(output)
                ? SplitWav(await File.ReadAllBytesAsync(output, cancellationToken), configuration.ChunkSeconds)
                : [];
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

    /// <summary>
    /// Splits 16-bit mono PCM into chunks of at most <paramref name="maxChunkSeconds"/>. Each cut falls at the
    /// quietest moment of the last seconds of the chunk, so it lands between words rather than inside one.
    /// </summary>
    public static IReadOnlyList<SpeakingWavChunk> SplitWav(byte[] wav, int maxChunkSeconds)
    {
        if (!TryFindPcm(wav, out var byteRate, out var pcmOffset, out var pcmLength) || byteRate <= 0)
            return [];
        var pcm = wav.AsSpan(pcmOffset, pcmLength & ~1);
        var maxBytes = maxChunkSeconds * byteRate & ~1;
        var searchBytes = Math.Min(CutSearchSeconds * byteRate, maxBytes / 2) & ~1;
        var chunks = new List<SpeakingWavChunk>();
        var start = 0;
        while (start < pcm.Length)
        {
            var end = pcm.Length - start <= maxBytes
                ? pcm.Length
                : QuietestCut(pcm, start + maxBytes - searchBytes, start + maxBytes, byteRate);
            var seconds = (end - start) / (double)byteRate;
            if (seconds >= MinimumChunkSeconds)
                chunks.Add(new SpeakingWavChunk(ToWav(pcm[start..end], byteRate), seconds));
            start = end;
        }
        return chunks;
    }

    private static int QuietestCut(ReadOnlySpan<byte> pcm, int from, int to, int byteRate)
    {
        var windowBytes = byteRate / 20 & ~1;
        var cut = to;
        var lowest = long.MaxValue;
        for (var window = from; window + windowBytes <= to; window += windowBytes)
        {
            long energy = 0;
            for (var sample = window; sample < window + windowBytes; sample += 2)
                energy += Math.Abs((int)BinaryPrimitives.ReadInt16LittleEndian(pcm.Slice(sample, 2)));
            if (energy > lowest)
                continue;
            lowest = energy;
            cut = window + windowBytes / 2 & ~1;
        }
        return cut;
    }

    private static byte[] ToWav(ReadOnlySpan<byte> pcm, int byteRate)
    {
        var wav = new byte[WavHeaderBytes + pcm.Length];
        var header = wav.AsSpan();
        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], WavHeaderBytes - 8 + pcm.Length);
        "WAVEfmt "u8.CopyTo(header[8..]);
        BinaryPrimitives.WriteInt32LittleEndian(header[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(header[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(header[22..], 1);
        BinaryPrimitives.WriteInt32LittleEndian(header[24..], byteRate / 2);
        BinaryPrimitives.WriteInt32LittleEndian(header[28..], byteRate);
        BinaryPrimitives.WriteInt16LittleEndian(header[32..], 2);
        BinaryPrimitives.WriteInt16LittleEndian(header[34..], 16);
        "data"u8.CopyTo(header[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(header[40..], pcm.Length);
        pcm.CopyTo(header[WavHeaderBytes..]);
        return wav;
    }

    /// <summary>Duration from the RIFF "fmt " byte rate and "data" chunk size.</summary>
    public static double WavSeconds(ReadOnlySpan<byte> wav) =>
        TryFindPcm(wav, out var byteRate, out _, out var pcmLength) && byteRate > 0 ? pcmLength / (double)byteRate : 0;

    private static bool TryFindPcm(ReadOnlySpan<byte> wav, out int byteRate, out int pcmOffset, out int pcmLength)
    {
        byteRate = 0;
        pcmOffset = 0;
        pcmLength = 0;
        if (wav.Length < 12 || !wav[..4].SequenceEqual("RIFF"u8) || !wav.Slice(8, 4).SequenceEqual("WAVE"u8))
            return false;
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
                pcmOffset = body;
                pcmLength = (int)Math.Min(size, (uint)(wav.Length - body));
                return true;
            }
            offset = body + (int)Math.Min(size + (size & 1), (uint)(wav.Length - body));
        }
        return false;
    }
}
