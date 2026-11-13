using FluentValidation;
using MediatR;

namespace TOEICMaster.Application.Writing.Admin;

public sealed record PreviewWritingTestImportQuery(byte[] Content) : IRequest<WritingTestImportPreviewDto>;
public sealed record GetWritingTestImportTemplateQuery : IRequest<byte[]>;

public sealed class PreviewWritingTestImportQueryValidator : AbstractValidator<PreviewWritingTestImportQuery>
{
    public PreviewWritingTestImportQueryValidator() => RuleFor(query => query.Content).NotEmpty()
        .Must(content => content.Length <= 10 * 1024 * 1024);
}

public sealed class PreviewWritingTestImportQueryHandler(IWritingTestImportParser parser,
    IWritingAdminRepository repository) : IRequestHandler<PreviewWritingTestImportQuery, WritingTestImportPreviewDto>
{
    public async Task<WritingTestImportPreviewDto> Handle(PreviewWritingTestImportQuery request,
        CancellationToken cancellationToken)
    {
        var result = parser.Preview(request.Content);
        if (result.Test is null) return result;
        var errors = result.Errors.ToList();
        if (await repository.TestCodeExistsAsync(result.Test.Code, null, cancellationToken))
            errors.Add("Mã đề đã tồn tại.");
        foreach (var question in result.Test.Questions)
            if (question.Content?.ImageAssetId is Guid image &&
                !await repository.MediaAssetExistsAsync(image, cancellationToken))
                errors.Add($"Part {question.Part}, câu {question.SequenceNumber}: không tìm thấy ảnh {image}.");
        return result with { Errors = errors };
    }
}

public sealed class GetWritingTestImportTemplateQueryHandler(IWritingTestImportParser parser)
    : IRequestHandler<GetWritingTestImportTemplateQuery, byte[]>
{
    public Task<byte[]> Handle(GetWritingTestImportTemplateQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(parser.CreateTemplate());
    }
}
