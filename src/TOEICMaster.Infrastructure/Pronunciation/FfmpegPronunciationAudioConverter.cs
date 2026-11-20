using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using TOEICMaster.Application.Pronunciation;

namespace TOEICMaster.Infrastructure.Pronunciation;

/// <summary>16 kHz mono 16-bit PCM WAV and its duration in seconds.</summary>
public sealed record PronunciationWav(byte[] Wav, double Seconds);

public interface IPronunciationAudioConverter
{
    /// <summary>Converts a short browser recording to 16 kHz mono PCM WAV for Azure Speech.</summary>
    Task<PronunciationWav> ToWavAsync(byte[] audio, string contentType, CancellationToken cancellationToken);
}

public sealed class FfmpegPronunciationAudioConverter(IOptions<PronunciationSpeechOptions> options)
    : IPronunciationAudioConverter
{
    private const int WavHeaderBytes = 44;
    private const int BytesPerSecond = 16000 * 2;
    private static readonly TimeSpan ConversionTimeout = TimeSpan.FromSeconds(20);

    public async Task<PronunciationWav> ToWavAsync(byte[] audio, string contentType,
        CancellationToken cancellationToken)
    {
        // The frontend already sends 16 kHz mono PCM WAV: use it as is, no ffmpeg needed.
        if (contentType == "audio/wav" && TryReadPcm16Mono16k(audio, out var pcm))
            return new PronunciationWav(Wav(pcm), Math.Round(pcm.Length / (double)BytesPerSecond, 2));

        var directory = Directory.CreateTempSubdirectory("toeicmaster-pronunciation-");
        try
        {
            var input = Path.Combine(directory.FullName, "input." + Extension(contentType));
            var output = Path.Combine(directory.FullName, "audio.wav");
            await File.WriteAllBytesAsync(input, audio, cancellationToken);

            var startInfo = new ProcessStartInfo(options.Value.FfmpegPath)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            // -t caps decoding just above the limit, so an oversized file cannot keep ffmpeg busy.
            foreach (var argument in new[]
                     {
                         "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-i", input, "-t",
                         (PronunciationRules.MaxAudioSeconds + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                         "-vn", "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le", "-f", "wav", output
                     })
                startInfo.ArgumentList.Add(argument);

            Process process;
            try
            {
                process = Process.Start(startInfo) ?? throw PronunciationAssessmentException.NotConfigured();
            }
            catch (Win32Exception exception)
            {
                // ffmpeg is not installed on this host.
                throw new PronunciationAssessmentException(PronunciationFailureKind.NotConfigured, "NOT_CONFIGURED",
                    "Chưa chấm được phát âm lúc này.", exception);
            }

            using (process)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(ConversionTimeout);
                var standardError = process.StandardError.ReadToEndAsync(timeout.Token);
                var standardOutput = process.StandardOutput.ReadToEndAsync(timeout.Token);
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                    await Task.WhenAll(standardError, standardOutput);
                }
                catch (OperationCanceledException exception)
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) { /* already exited */ }
                    if (cancellationToken.IsCancellationRequested)
                        throw;
                    throw PronunciationAssessmentException.Timeout(exception);
                }

                if (process.ExitCode != 0 || !File.Exists(output))
                    throw PronunciationAssessmentException.InvalidAudio("Không đọc được bản thu. Hãy thu lại.");
            }

            var wav = await File.ReadAllBytesAsync(output, cancellationToken);
            var seconds = Math.Max(0, wav.Length - WavHeaderBytes) / (double)BytesPerSecond;
            return new PronunciationWav(wav, Math.Round(seconds, 2));
        }
        finally
        {
            try { directory.Delete(recursive: true); }
            catch (IOException) { /* temp cleanup is best effort */ }
            catch (UnauthorizedAccessException) { /* temp cleanup is best effort */ }
        }
    }

    /// <summary>
    /// Reads the samples of a RIFF/WAVE file that is already 16-bit PCM, mono, 16 kHz. Any other WAV layout
    /// returns false and goes through ffmpeg.
    /// </summary>
    internal static bool TryReadPcm16Mono16k(byte[] wav, out byte[] pcm)
    {
        pcm = [];
        var bytes = wav.AsSpan();
        if (bytes.Length < 12 || !bytes.StartsWith("RIFF"u8) || !bytes.Slice(8, 4).SequenceEqual("WAVE"u8))
            return false;
        var formatOk = false;
        var offset = 12;
        while (offset + 8 <= bytes.Length)
        {
            var id = bytes.Slice(offset, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + 4, 4));
            var body = offset + 8;
            if (size > (uint)(bytes.Length - body))
                return false;
            if (id.SequenceEqual("fmt "u8))
            {
                if (size < 16)
                    return false;
                var format = bytes.Slice(body, 16);
                formatOk = BinaryPrimitives.ReadUInt16LittleEndian(format) == 1 &&
                           BinaryPrimitives.ReadUInt16LittleEndian(format[2..]) == 1 &&
                           BinaryPrimitives.ReadUInt32LittleEndian(format[4..]) == 16000 &&
                           BinaryPrimitives.ReadUInt16LittleEndian(format[14..]) == 16;
                if (!formatOk)
                    return false;
            }
            else if (id.SequenceEqual("data"u8))
            {
                if (!formatOk)
                    return false;
                pcm = bytes.Slice(body, (int)size & ~1).ToArray();
                return true;
            }
            offset = body + (int)size + (int)(size & 1);
        }
        return false;
    }

    /// <summary>A canonical 44-byte-header WAV around 16 kHz mono 16-bit samples.</summary>
    internal static byte[] Wav(byte[] pcm)
    {
        var wav = new byte[WavHeaderBytes + pcm.Length];
        var span = wav.AsSpan();
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], (uint)(36 + pcm.Length));
        "WAVEfmt "u8.CopyTo(span[8..]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteUInt16LittleEndian(span[20..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span[22..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(span[24..], 16000);
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..], BytesPerSecond);
        BinaryPrimitives.WriteUInt16LittleEndian(span[32..], 2);
        BinaryPrimitives.WriteUInt16LittleEndian(span[34..], 16);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[40..], (uint)pcm.Length);
        pcm.CopyTo(span[WavHeaderBytes..]);
        return wav;
    }

    private static string Extension(string contentType) => contentType switch
    {
        "audio/webm" => "webm",
        "audio/ogg" => "ogg",
        "audio/mp4" => "m4a",
        "audio/wav" => "wav",
        _ => "bin"
    };
}
