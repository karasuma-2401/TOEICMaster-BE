using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable("exams");
        builder.HasKey(exam => exam.Id);
        builder.Property(exam => exam.Code).HasMaxLength(64).IsRequired();
        builder.Property(exam => exam.Title).HasMaxLength(200).IsRequired();
        builder.Property(exam => exam.CreatedAtUtc).IsRequired();
        builder.HasIndex(exam => exam.Code).IsUnique();
        builder.HasMany(exam => exam.Revisions).WithOne().HasForeignKey(revision => revision.ExamId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(exam => exam.Revisions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ExamRevisionConfiguration : IEntityTypeConfiguration<ExamRevision>
{
    public void Configure(EntityTypeBuilder<ExamRevision> builder)
    {
        builder.ToTable("exam_revisions");
        builder.HasKey(revision => revision.Id);
        builder.Property(revision => revision.VersionNumber).IsRequired();
        builder.Property(revision => revision.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(revision => revision.MasterAudioObjectKey).HasMaxLength(256);
        builder.Property(revision => revision.CreatedAtUtc).IsRequired();
        builder.HasIndex(revision => new { revision.ExamId, revision.VersionNumber }).IsUnique();
        builder.HasIndex(revision => new { revision.ExamId, revision.Status });
        builder.HasOne<ScoreProfile>().WithMany().HasForeignKey(revision => revision.ScoreProfileId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(revision => revision.Blocks).WithOne().HasForeignKey(block => block.RevisionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(revision => revision.Blocks).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ExamBlockConfiguration : IEntityTypeConfiguration<ExamBlock>
{
    public void Configure(EntityTypeBuilder<ExamBlock> builder)
    {
        builder.ToTable("exam_blocks");
        builder.HasKey(block => block.Id);
        builder.Property(block => block.Key).HasMaxLength(32).IsRequired();
        builder.Property(block => block.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(block => new { block.RevisionId, block.Key }).IsUnique();
        builder.HasIndex(block => new { block.RevisionId, block.FirstQuestionNumber }).IsUnique();
        builder.HasMany(block => block.Questions).WithOne().HasForeignKey(question => question.BlockId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(block => block.Questions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(block => block.ContextBlocks).WithOne().HasForeignKey(context => context.BlockId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(block => block.ContextBlocks).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ExamQuestionConfiguration : IEntityTypeConfiguration<ExamQuestion>
{
    public void Configure(EntityTypeBuilder<ExamQuestion> builder)
    {
        builder.ToTable("exam_questions");
        builder.HasKey(question => question.Id);
        builder.Property(question => question.Code).HasMaxLength(80);
        builder.Property(question => question.Stem).HasColumnType("text");
        builder.Property(question => question.OptionA).HasColumnType("text");
        builder.Property(question => question.OptionB).HasColumnType("text");
        builder.Property(question => question.OptionC).HasColumnType("text");
        builder.Property(question => question.OptionD).HasColumnType("text");
        builder.Property(question => question.CorrectKey).HasMaxLength(1);
        builder.Property(question => question.Translation).HasColumnType("text");
        builder.Property(question => question.Explanation).HasColumnType("text");
        builder.HasIndex(question => new { question.BlockId, question.Number }).IsUnique();
        builder.HasOne<ExamMediaAsset>().WithMany().HasForeignKey(question => question.ImageAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExamContextBlockConfiguration : IEntityTypeConfiguration<ExamContextBlock>
{
    public void Configure(EntityTypeBuilder<ExamContextBlock> builder)
    {
        builder.ToTable("exam_context_blocks");
        builder.HasKey(context => context.Id);
        builder.Property(context => context.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(context => context.Text).HasColumnType("text");
        builder.Property(context => context.AltText).HasMaxLength(500);
        builder.HasIndex(context => new { context.BlockId, context.Ordinal }).IsUnique();
        builder.HasOne<ExamMediaAsset>().WithMany().HasForeignKey(context => context.ImageAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExamMediaAssetConfiguration : IEntityTypeConfiguration<ExamMediaAsset>
{
    public void Configure(EntityTypeBuilder<ExamMediaAsset> builder)
    {
        builder.ToTable("exam_media_assets");
        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.ObjectKey).HasMaxLength(256).IsRequired();
        builder.Property(asset => asset.ContentType).HasMaxLength(64).IsRequired();
        builder.Property(asset => asset.CreatedAtUtc).IsRequired();
        builder.HasIndex(asset => asset.ObjectKey).IsUnique();
    }
}
