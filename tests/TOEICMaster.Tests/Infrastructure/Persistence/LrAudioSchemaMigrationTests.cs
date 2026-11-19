using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using TOEICMaster.Infrastructure.Persistence.Migrations;

namespace TOEICMaster.Tests.Infrastructure.Persistence;

public sealed class LrAudioSchemaMigrationTests
{
    [Fact]
    public void Refactor_lr_audio_schema_drops_legacy_revision_columns_and_adds_nullable_duration_columns()
    {
        var operations = new RefactorLrAudioSchemaProbe().GetUpOperations();

        var droppedColumns = operations.OfType<DropColumnOperation>().ToArray();
        var expectedDroppedColumns = new (string Table, string Name)[]
        {
            ("exam_revisions", "MasterAudioObjectKey"),
            ("exam_revisions", "UsesMediaWorkspace")
        };

        Assert.Equal(expectedDroppedColumns.Length, droppedColumns.Length);
        foreach (var (table, name) in expectedDroppedColumns)
            Assert.Contains(droppedColumns, operation => operation.Table == table && operation.Name == name);

        var addedColumns = operations.OfType<AddColumnOperation>().ToArray();
        var expectedAddedColumns = new (string Table, string Name)[]
        {
            ("exam_media_assets", "DurationMs"),
            ("exam_media_upload_intents", "ExpectedDurationMs")
        };

        Assert.Equal(expectedAddedColumns.Length, addedColumns.Length);
        foreach (var (table, name) in expectedAddedColumns)
        {
            var column = Assert.Single(addedColumns, operation => operation.Table == table && operation.Name == name);
            Assert.Equal(typeof(int), column.ClrType);
            Assert.Equal("integer", column.ColumnType);
            Assert.True(column.IsNullable);
        }

        Assert.Equal(4, operations.Count);
    }

    private sealed class RefactorLrAudioSchemaProbe : RefactorLrAudioSchema
    {
        public IReadOnlyList<MigrationOperation> GetUpOperations()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Up(builder);
            return builder.Operations;
        }
    }
}
