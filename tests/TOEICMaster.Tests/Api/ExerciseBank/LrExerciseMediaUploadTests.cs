using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Api.Controllers;
using TOEICMaster.Application.Exams.Commands.UploadExamImage;
using TOEICMaster.Tests.Api.Common;

namespace TOEICMaster.Tests.Api.ExerciseBank;

public sealed class LrExerciseMediaUploadTests
{
    [Fact]
    public async Task Oversized_image_is_rejected_before_file_is_read()
    {
        using var factory = new OpenApiSnapshotFactory();
        using var scope = factory.Services.CreateScope();
        var controller = new AdminLrExerciseBankController(null!, null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider }
            },
            ProblemDetailsFactory = scope.ServiceProvider.GetRequiredService<ProblemDetailsFactory>()
        };

        var response = await controller.UploadMedia("image", new OversizedImageFile(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(response.Result).StatusCode);
    }

    private sealed class OversizedImageFile : IFormFile
    {
        public string ContentType => "image/png";
        public string ContentDisposition => "";
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public long Length => UploadExamImageCommandHandler.MaxBytes + 1;
        public string Name => "file";
        public string FileName => "large.png";
        public Stream OpenReadStream() => throw new InvalidOperationException("Image was read before validation.");
        public void CopyTo(Stream target) => throw new InvalidOperationException("Image was read before validation.");
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Image was read before validation.");
    }
}
