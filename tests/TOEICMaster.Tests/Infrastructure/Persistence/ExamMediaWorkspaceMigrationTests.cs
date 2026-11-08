using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using TOEICMaster.Infrastructure.Persistence.Migrations;

namespace TOEICMaster.Tests.Infrastructure.Persistence;

public sealed class ExamMediaWorkspaceMigrationTests
{
    [Fact]
    public void Workspace_audio_foreign_key_targets_the_existing_exam_media_asset_primary_key()
    {
        var operations = new AddAdminLrMediaWorkspaceProbe().GetUpOperations();

        var foreignKey = Assert.Single(operations.OfType<AddForeignKeyOperation>(), operation =>
            operation.Name == "fk_exam_blocks_exam_media_assets_audio_asset_id");

        Assert.Equal("exam_media_assets", foreignKey.PrincipalTable);
        Assert.Equal(["Id"], Assert.IsType<string[]>(foreignKey.PrincipalColumns));
    }

    [Fact]
    public void Workspace_schema_uses_the_existing_exam_model_column_names()
    {
        var operations = new AddAdminLrMediaWorkspaceProbe().GetUpOperations();

        var addedColumns = operations.OfType<AddColumnOperation>().ToArray();
        var expectedAddedColumns = new (string Table, string Name)[]
        {
            ("exam_revisions", "UsesMediaWorkspace"),
            ("exam_blocks", "AudioAssetId"),
            ("exam_questions", "ImageRequired"),
            ("exam_context_blocks", "ImageRequired"),
            ("exam_media_assets", "Kind"),
            ("exam_media_assets", "ByteSize")
        };

        Assert.Equal(expectedAddedColumns.Length, addedColumns.Length);
        foreach (var (table, name) in expectedAddedColumns)
            Assert.Contains(addedColumns, operation => operation.Table == table && operation.Name == name);

        var uploadIntents = Assert.Single(operations.OfType<CreateTableOperation>(), operation =>
            operation.Name == "exam_media_upload_intents");
        Assert.Equal(
        [
            "Id", "ExamId", "RevisionId", "OwnerKind", "OwnerKey", "ObjectKey", "ContentType",
            "ExpectedByteSize", "ExpiresAtUtc", "CreatedAtUtc", "Status"
        ], uploadIntents.Columns.Select(column => column.Name));

        Assert.Contains(operations.OfType<CreateIndexOperation>(), operation =>
            operation.Table == "exam_blocks" && operation.Columns.SequenceEqual(["AudioAssetId"]));
        Assert.Contains(operations.OfType<CreateIndexOperation>(), operation =>
            operation.Table == "exam_media_upload_intents" && operation.Columns.SequenceEqual(["ObjectKey"]));
        Assert.Contains(operations.OfType<CreateIndexOperation>(), operation =>
            operation.Table == "exam_media_upload_intents" &&
            operation.Columns.SequenceEqual(["RevisionId", "Status", "ExpiresAtUtc"]));
    }

    private sealed class AddAdminLrMediaWorkspaceProbe : AddAdminLrMediaWorkspace
    {
        public IReadOnlyList<MigrationOperation> GetUpOperations()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Up(builder);
            return builder.Operations;
        }
    }
}
