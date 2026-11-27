using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Application.ExamAttempts;
using TOEICMaster.Application.Exams;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Infrastructure.Persistence;
using TOEICMaster.Tests.Api.Common;

namespace TOEICMaster.Tests.Api.Exams;

public sealed class LrAudioEfRepositoryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Ef_repositories_persist_and_query_durations_playlist_and_media_access_in_postgres()
    {
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var p1Audio = ExamMediaAsset.Create($"exam-media/{suffix}/p1.mp3", "audio/mpeg", now, ExamMediaKind.Audio,
            2048, 19_200);
        var p2AudioNoDuration = ExamMediaAsset.Create($"exam-media/{suffix}/p2.mp3", "audio/mpeg", now,
            ExamMediaKind.Audio, 1024, null);
        var p3Audio = ExamMediaAsset.Create($"exam-media/{suffix}/p3.mp3", "audio/mpeg", now, ExamMediaKind.Audio,
            1024, 32_400);
        var p4Audio = ExamMediaAsset.Create($"exam-media/{suffix}/p4.mp3", "audio/mpeg", now, ExamMediaKind.Audio,
            1024, 45_600);
        var q1Image = ExamMediaAsset.Create($"exam-media/{suffix}/q1.png", "image/png", now, ExamMediaKind.Image,
            4096);
        var p3ContextImage = ExamMediaAsset.Create($"exam-media/{suffix}/p3-ctx.png", "image/png", now,
            ExamMediaKind.Image, 5120);
        var unlinkedAudio = ExamMediaAsset.Create($"exam-media/{suffix}/unlinked.mp3", "audio/mpeg", now,
            ExamMediaKind.Audio, 1024, 5_000);

        var profile = ScoreProfile.Create($"EF Profile {suffix}", now);
        foreach (var skill in new[] { ExamSkill.Listening, ExamSkill.Reading })
        {
            for (var raw = 0; raw <= 100; raw++)
                profile.SetScore(skill, raw, Math.Min(495, 5 + (raw * 5)));
        }

        var exam = Exam.Create($"LR-EF-{suffix}", "EF Audio Verification Exam", profile.Id, now);
        var revision = exam.Revisions.Single();
        revision.UpdateQuestion(1, new ExamQuestionContent($"EF-{suffix}-001", "Stem 1", "A", "B", "C", "D", "A",
            null, null, q1Image.Id));
        revision.UpdateQuestion(7, new ExamQuestionContent($"EF-{suffix}-007", "Stem 7", "A", "B", "C", null, "B",
            null, null, null));
        revision.ReplaceContextBlocks("P3-G32", [
            new ExamContextContent(1, ExamContextType.Image, null, p3ContextImage.Id, "Context diagram", false)
        ]);
        revision.AttachAudioAsset("P4-G71", p4Audio.Id);
        revision.AttachAudioAsset("P2-Q7", p2AudioNoDuration.Id);
        revision.AttachAudioAsset("P1-Q1", p1Audio.Id);
        revision.AttachAudioAsset("P3-G32", p3Audio.Id);

        var listeningAttempt = new ExamAttempt(Guid.NewGuid(), ownerId, revision.Id, profile.Id, now);
        var readingLockedAttempt = new ExamAttempt(Guid.NewGuid(), ownerId, revision.Id, profile.Id, now);
        readingLockedAttempt.StartReading(now.AddMinutes(45));
        var completedAttempt = new ExamAttempt(Guid.NewGuid(), ownerId, revision.Id, profile.Id, now);
        completedAttempt.GradeAndComplete(profile, new Dictionary<Guid, string?>(), now.AddMinutes(60));

        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<ToeicDbContext>();
            db.ScoreProfiles.Add(profile);
            db.ExamMediaAssets.AddRange(p1Audio, p2AudioNoDuration, p3Audio, p4Audio, q1Image, p3ContextImage,
                unlinkedAudio);
            db.Exams.Add(exam);
            db.ExamAttempts.AddRange(listeningAttempt, readingLockedAttempt, completedAttempt);
            await db.SaveChangesAsync();
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var examRepository = verifyScope.ServiceProvider.GetRequiredService<IExamRepository>();
        var attemptRepository = verifyScope.ServiceProvider.GetRequiredService<IExamAttemptRepository>();

        var examDurations = await examRepository.GetMediaDurationsAsync(
            [p1Audio.Id, p2AudioNoDuration.Id, Guid.NewGuid()], CancellationToken.None);
        Assert.Equal(19_200, examDurations[p1Audio.Id]);
        Assert.Null(examDurations[p2AudioNoDuration.Id]);

        var emptyExamDurations = await examRepository.GetMediaDurationsAsync([], CancellationToken.None);
        Assert.Empty(emptyExamDurations);

        var loadedExam = await examRepository.FindExamAsync(exam.Id, CancellationToken.None);
        Assert.NotNull(loadedExam);
        var loadedRevision = Assert.Single(loadedExam.Revisions);
        Assert.Equal(200, loadedRevision.Blocks.Sum(block => block.Questions.Count));
        Assert.Equal(p3ContextImage.Id,
            Assert.Single(loadedRevision.Blocks.Single(block => block.Key == "P3-G32").ContextBlocks).ImageAssetId);

        var attemptDurations = await attemptRepository.GetMediaDurationsAsync(
            [p1Audio.Id, p2AudioNoDuration.Id], CancellationToken.None);
        Assert.Equal(19_200, attemptDurations[p1Audio.Id]);
        Assert.Null(attemptDurations[p2AudioNoDuration.Id]);

        var playlistMissingAttempt = await attemptRepository.GetListeningPlaylistSourceAsync(
            Guid.NewGuid(), ownerId, CancellationToken.None);
        Assert.Null(playlistMissingAttempt);

        var playlistWrongUser = await attemptRepository.GetListeningPlaylistSourceAsync(
            listeningAttempt.Id, otherUserId, CancellationToken.None);
        Assert.Null(playlistWrongUser);

        var listeningPlaylist = await attemptRepository.GetListeningPlaylistSourceAsync(
            listeningAttempt.Id, ownerId, CancellationToken.None);
        Assert.NotNull(listeningPlaylist);
        Assert.Equal(ExamAttemptStatus.InProgress, listeningPlaylist.Status);
        Assert.False(listeningPlaylist.IsListeningLocked);
        Assert.Equal(4, listeningPlaylist.Blocks.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, listeningPlaylist.Blocks.Select(block => block.Part));
        Assert.Equal(new[] { 1, 7, 32, 71 }, listeningPlaylist.Blocks.Select(block => block.FirstQuestionNumber));
        Assert.Equal(1, listeningPlaylist.Blocks[0].Part);
        Assert.Equal(1, listeningPlaylist.Blocks[0].FirstQuestionNumber);
        Assert.Equal(p1Audio.Id, listeningPlaylist.Blocks[0].AudioAssetId);
        Assert.Equal(p1Audio.ObjectKey, listeningPlaylist.Blocks[0].ObjectKey);
        Assert.Equal(19_200, listeningPlaylist.Blocks[0].DurationMs);
        Assert.Equal(2, listeningPlaylist.Blocks[1].Part);
        Assert.Equal(7, listeningPlaylist.Blocks[1].FirstQuestionNumber);
        Assert.Equal(p2AudioNoDuration.Id, listeningPlaylist.Blocks[1].AudioAssetId);
        Assert.Null(listeningPlaylist.Blocks[1].DurationMs);
        Assert.Equal(3, listeningPlaylist.Blocks[2].Part);
        Assert.Equal(p3Audio.Id, listeningPlaylist.Blocks[2].AudioAssetId);
        Assert.Equal(32_400, listeningPlaylist.Blocks[2].DurationMs);
        Assert.Equal(4, listeningPlaylist.Blocks[3].Part);
        Assert.Equal(p4Audio.Id, listeningPlaylist.Blocks[3].AudioAssetId);
        Assert.Equal(45_600, listeningPlaylist.Blocks[3].DurationMs);

        var lockedPlaylist = await attemptRepository.GetListeningPlaylistSourceAsync(
            readingLockedAttempt.Id, ownerId, CancellationToken.None);
        Assert.NotNull(lockedPlaylist);
        Assert.True(lockedPlaylist.IsListeningLocked);

        var listeningAudioAccess = await attemptRepository.GetAttemptMediaAccessAsync(
            listeningAttempt.Id, ownerId, p1Audio.Id, ExamMediaKind.Audio, CancellationToken.None);
        Assert.Equal(AttemptMediaLookupStatus.Found, listeningAudioAccess.Status);
        Assert.Equal(p1Audio.ObjectKey, listeningAudioAccess.ObjectKey);

        var lockedAudioAccess = await attemptRepository.GetAttemptMediaAccessAsync(
            readingLockedAttempt.Id, ownerId, p1Audio.Id, ExamMediaKind.Audio, CancellationToken.None);
        Assert.Equal(AttemptMediaLookupStatus.ListeningLocked, lockedAudioAccess.Status);
        Assert.Null(lockedAudioAccess.ObjectKey);

        var completedAudioAccess = await attemptRepository.GetAttemptMediaAccessAsync(
            completedAttempt.Id, ownerId, p1Audio.Id, ExamMediaKind.Audio, CancellationToken.None);
        Assert.Equal(AttemptMediaLookupStatus.Found, completedAudioAccess.Status);
        Assert.Equal(p1Audio.ObjectKey, completedAudioAccess.ObjectKey);

        var lockedQuestionImageAccess = await attemptRepository.GetAttemptMediaAccessAsync(
            readingLockedAttempt.Id, ownerId, q1Image.Id, ExamMediaKind.Image, CancellationToken.None);
        Assert.Equal(AttemptMediaLookupStatus.Found, lockedQuestionImageAccess.Status);
        Assert.Equal(q1Image.ObjectKey, lockedQuestionImageAccess.ObjectKey);

        var lockedContextImageAccess = await attemptRepository.GetAttemptMediaAccessAsync(
            readingLockedAttempt.Id, ownerId, p3ContextImage.Id, ExamMediaKind.Image, CancellationToken.None);
        Assert.Equal(AttemptMediaLookupStatus.Found, lockedContextImageAccess.Status);
        Assert.Equal(p3ContextImage.ObjectKey, lockedContextImageAccess.ObjectKey);

        var wrongUserAccess = await attemptRepository.GetAttemptMediaAccessAsync(
            listeningAttempt.Id, otherUserId, p1Audio.Id, ExamMediaKind.Audio, CancellationToken.None);
        Assert.Equal(AttemptMediaLookupStatus.NotFound, wrongUserAccess.Status);

        var unlinkedAssetAccess = await attemptRepository.GetAttemptMediaAccessAsync(
            listeningAttempt.Id, ownerId, unlinkedAudio.Id, ExamMediaKind.Audio, CancellationToken.None);
        Assert.Equal(AttemptMediaLookupStatus.NotFound, unlinkedAssetAccess.Status);

        var kindMismatchAccess = await attemptRepository.GetAttemptMediaAccessAsync(
            listeningAttempt.Id, ownerId, p1Audio.Id, ExamMediaKind.Image, CancellationToken.None);
        Assert.Equal(AttemptMediaLookupStatus.NotFound, kindMismatchAccess.Status);
    }
}
