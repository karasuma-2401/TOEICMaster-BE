using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class FlashcardDeckConfiguration : IEntityTypeConfiguration<FlashcardDeck>
{
    public void Configure(EntityTypeBuilder<FlashcardDeck> builder)
    {
        builder.ToTable("flashcard_decks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Slug)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.TargetScore)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.EstimatedMinutes)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.Ignore(x => x.IsCustom);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Cards)
            .WithOne()
            .HasForeignKey(x => x.DeckId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Slug)
            .IsUnique();

        builder.HasIndex(x => new { x.OwnerUserId, x.Category });
    }
}

public sealed class FlashcardCardConfiguration : IEntityTypeConfiguration<FlashcardCard>
{
    public void Configure(EntityTypeBuilder<FlashcardCard> builder)
    {
        builder.ToTable("flashcard_cards");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Ordinal)
            .IsRequired();

        builder.Property(x => x.Term)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.Phonetic)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.PartOfSpeech)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.MeaningVi)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.ExampleEn)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.ExampleVi)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.CollocationsSerialized)
            .HasColumnName("collocations")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.ToeicPart)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Level)
            .HasMaxLength(8)
            .IsRequired();

        builder.HasIndex(x => new { x.DeckId, x.Ordinal })
            .IsUnique();
    }
}

public sealed class UserFlashcardProgressConfiguration : IEntityTypeConfiguration<UserFlashcardProgress>
{
    public void Configure(EntityTypeBuilder<UserFlashcardProgress> builder)
    {
        builder.ToTable("user_flashcard_progress");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Status)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.Starred)
            .IsRequired();

        builder.Property(x => x.ReviewCount)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<FlashcardCard>()
            .WithMany()
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.CardId })
            .IsUnique();
    }
}

public sealed class UserFlashcardMatchRecordConfiguration : IEntityTypeConfiguration<UserFlashcardMatchRecord>
{
    public void Configure(EntityTypeBuilder<UserFlashcardMatchRecord> builder)
    {
        builder.ToTable("user_flashcard_match_records");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.BestTimeMs)
            .IsRequired();

        builder.Property(x => x.BestCombo)
            .IsRequired();

        builder.Property(x => x.PlayCount)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<FlashcardDeck>()
            .WithMany()
            .HasForeignKey(x => x.DeckId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.DeckId })
            .IsUnique();
    }
}
