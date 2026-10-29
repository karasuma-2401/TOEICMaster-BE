using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Application.Exams;
using TOEICMaster.Application.Exams.Commands.CreateExam;
using TOEICMaster.Application.Exams.Commands.ForkExamRevision;
using TOEICMaster.Application.Exams.Commands.ImportExam;
using TOEICMaster.Application.Exams.Commands.PublishExamRevision;
using TOEICMaster.Application.Exams.Commands.ReplaceExamContextBlocks;
using TOEICMaster.Application.Exams.Commands.UpdateExamQuestion;
using TOEICMaster.Application.Exams.Commands.UpdateExamScoreProfile;
using TOEICMaster.Application.Exams.Commands.UploadMasterAudio;
using TOEICMaster.Application.Exams.Importing;
using TOEICMaster.Application.Exams.Queries.GetExamRevision;
using TOEICMaster.Application.Exams.Queries.GetExams;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Route("api/v1/admin/exams")]
[Authorize(Policy = "AccountAdministration")]
public sealed class AdminExamsController(ISender sender, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("import-template")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public IActionResult DownloadImportTemplate()
    {
        const string filename = "toeic-lr-complete-exam-import-template.xlsx";
        var path = Path.Combine(environment.ContentRootPath, "Resources", "ExamTemplates", filename);
        if (!System.IO.File.Exists(path))
            return NotFound();
        return PhysicalFile(path, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename,
            enableRangeProcessing: false);
    }

    [HttpGet]
    [ProducesResponseType<ExamPageDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExamPageDto>> GetExams([FromQuery] ExamRevisionStatus? status,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetExamsQuery(status, pageNumber, pageSize), cancellationToken));

    [HttpGet("{examId:guid}/revisions/{revisionId:guid}")]
    [ProducesResponseType<ExamRevisionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExamRevisionDetailDto>> GetRevision(Guid examId, Guid revisionId,
        CancellationToken cancellationToken) => Ok(await sender.Send(new GetExamRevisionQuery(examId, revisionId),
        cancellationToken));

    [HttpPost]
    [ProducesResponseType<ExamDraftResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ExamDraftResponse>> Create(CreateExamRequest request,
        CancellationToken cancellationToken)
    {
        var draft = await sender.Send(new CreateExamCommand(request.Code, request.Title, request.ScoreProfileId),
            cancellationToken);
        var response = ExamDraftResponse.From(draft);
        return Created($"/api/v1/admin/exams/{draft.ExamId}/revisions/{draft.RevisionId}", response);
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(XlsxExamWorkbookReader.MaxBytes + UploadMasterAudioCommandHandler.MaxBytes + 128 * 1024)]
    [ProducesResponseType<ExamDraftResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExamDraftResponse>> Import([FromForm] IFormFile workbook,
        [FromForm] IFormFile masterAudio, CancellationToken cancellationToken)
    {
        if (workbook is null || workbook.Length == 0 || workbook.Length > XlsxExamWorkbookReader.MaxBytes)
            throw new ExamWorkbookFormatException(["Workbook phải là tệp XLSX không rỗng, tối đa 10 MiB."]);
        if (masterAudio is null || masterAudio.Length == 0 ||
            masterAudio.Length > UploadMasterAudioCommandHandler.MaxBytes)
            throw new InvalidExamMediaException();

        await using var workbookStream = new MemoryStream();
        await workbook.CopyToAsync(workbookStream, cancellationToken);
        await using var audioStream = new MemoryStream();
        await masterAudio.CopyToAsync(audioStream, cancellationToken);
        var draft = await sender.Send(new ImportExamCommand(workbookStream.ToArray(), audioStream.ToArray(),
            masterAudio.ContentType), cancellationToken);
        return Created($"/api/v1/admin/exams/{draft.ExamId}/revisions/{draft.RevisionId}", ExamDraftResponse.From(draft));
    }

    [HttpPut("{examId:guid}/revisions/{revisionId:guid}/questions/{questionNumber:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateQuestion(Guid examId, Guid revisionId, int questionNumber,
        UpdateExamQuestionRequest request, CancellationToken cancellationToken)
    {
        var content = new ExamQuestionContent(request.Code, request.Stem, request.OptionA, request.OptionB,
            request.OptionC, request.OptionD, request.CorrectKey, request.Translation, request.Explanation,
            request.ImageAssetId);
        await sender.Send(new UpdateExamQuestionCommand(examId, revisionId, questionNumber, content), cancellationToken);
        return NoContent();
    }

    [HttpPut("{examId:guid}/revisions/{revisionId:guid}/score-profile")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateScoreProfile(Guid examId, Guid revisionId, UpdateExamScoreProfileRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UpdateExamScoreProfileCommand(examId, revisionId, request.ScoreProfileId),
            cancellationToken);
        return NoContent();
    }

    [HttpPut("{examId:guid}/revisions/{revisionId:guid}/blocks/{blockKey}/contexts")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReplaceContexts(Guid examId, Guid revisionId, string blockKey,
        ReplaceExamContextBlocksRequest request, CancellationToken cancellationToken)
    {
        var contexts = request.Contexts.Select(context => new ExamContextContent(context.Ordinal, context.Type,
            context.Text, context.ImageAssetId, context.AltText)).ToArray();
        await sender.Send(new ReplaceExamContextBlocksCommand(examId, revisionId, blockKey, contexts), cancellationToken);
        return NoContent();
    }

    [HttpPost("{examId:guid}/revisions")]
    [ProducesResponseType<ExamDraftResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ExamDraftResponse>> ForkRevision(Guid examId, ForkExamRevisionRequest request,
        CancellationToken cancellationToken)
    {
        var draft = await sender.Send(new ForkExamRevisionCommand(examId, request.SourceRevisionId), cancellationToken);
        var response = ExamDraftResponse.From(draft);
        return Created($"/api/v1/admin/exams/{draft.ExamId}/revisions/{draft.RevisionId}", response);
    }

    [HttpPost("{examId:guid}/revisions/{revisionId:guid}/publish")]
    [ProducesResponseType<ExamDraftResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExamDraftResponse>> Publish(Guid examId, Guid revisionId,
        CancellationToken cancellationToken) => Ok(ExamDraftResponse.From(await sender.Send(
        new PublishExamRevisionCommand(examId, revisionId), cancellationToken)));

    [HttpPut("{examId:guid}/revisions/{revisionId:guid}/master-audio")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadMasterAudioCommandHandler.MaxBytes + 64 * 1024)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UploadMasterAudio(Guid examId, Guid revisionId, [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0 || file.Length > UploadMasterAudioCommandHandler.MaxBytes)
            throw new InvalidExamMediaException();
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        await sender.Send(new UploadMasterAudioCommand(examId, revisionId, stream.ToArray(), file.ContentType),
            cancellationToken);
        return NoContent();
    }
}

public sealed record CreateExamRequest(string Code, string Title, Guid ScoreProfileId);

public sealed record ExamDraftResponse(Guid ExamId, Guid RevisionId, string Code, string Title, int VersionNumber,
    string Status, int QuestionCount)
{
    public static ExamDraftResponse From(ExamDraftDto draft) => new(draft.ExamId, draft.RevisionId, draft.Code,
        draft.Title, draft.VersionNumber, draft.Status.ToString(), draft.QuestionCount);
}

public sealed record UpdateExamQuestionRequest(string Code, string Stem, string OptionA, string OptionB, string OptionC,
    string? OptionD, string CorrectKey, string? Translation, string? Explanation, Guid? ImageAssetId);
public sealed record UpdateExamScoreProfileRequest(Guid ScoreProfileId);
public sealed record ReplaceExamContextBlocksRequest(IReadOnlyList<ExamContextBlockRequest> Contexts);
public sealed record ExamContextBlockRequest(int Ordinal,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ExamContextType>))] ExamContextType Type, string? Text,
    Guid? ImageAssetId, string? AltText);
public sealed record ForkExamRevisionRequest(Guid SourceRevisionId);
