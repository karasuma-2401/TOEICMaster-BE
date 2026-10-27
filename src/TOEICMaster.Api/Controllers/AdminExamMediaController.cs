using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Application.Exams;
using TOEICMaster.Application.Exams.Commands.UploadExamImage;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Route("api/v1/admin/exam-media")]
[Authorize(Policy = "AccountAdministration")]
public sealed class AdminExamMediaController(ISender sender) : ControllerBase
{
    [HttpPost("images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadExamImageCommandHandler.MaxBytes + 64 * 1024)]
    [ProducesResponseType<ExamMediaAssetDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ExamMediaAssetDto>> UploadImage([FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0 || file.Length > UploadExamImageCommandHandler.MaxBytes)
            throw new InvalidExamMediaException();
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        var asset = await sender.Send(new UploadExamImageCommand(stream.ToArray(), file.ContentType), cancellationToken);
        return Created($"/api/v1/admin/exam-media/images/{asset.Id}", asset);
    }
}
