using System.Text;
using TOEICMaster.Application.Exams.Commands.UploadExamImage;

namespace TOEICMaster.Tests.Application.Exams;

public sealed class ExamMediaInspectorAudioTests
{
    [Fact]
    public void Accepts_complete_pcm_wav_and_rejects_truncated_data()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(436);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(8000);
        writer.Write(16000);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(400);
        writer.Write(new byte[400]);
        var valid = stream.ToArray();

        Assert.Equal(("wav", "audio/wav"), ExamMediaInspector.DetectAudio(valid));
        Assert.Null(ExamMediaInspector.DetectAudio(valid[..^1]));
        Assert.Null(ExamMediaInspector.DetectAudio(Encoding.ASCII.GetBytes("RIFFfakeWAVE")));
    }

    [Fact]
    public void Rejects_an_id3_tag_without_an_mp3_frame()
    {
        Assert.Null(ExamMediaInspector.DetectAudio(Encoding.ASCII.GetBytes("ID3fakeaudio")));
    }
}
