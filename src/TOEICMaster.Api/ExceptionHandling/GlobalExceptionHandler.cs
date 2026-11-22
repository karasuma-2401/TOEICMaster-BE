using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Minio.Exceptions;
using Npgsql;
using TOEICMaster.Application.Accounts;
using TOEICMaster.Application.Common.Exceptions;
using TOEICMaster.Application.ExamAttempts;
using TOEICMaster.Application.Exams;
using TOEICMaster.Application.Exams.Importing;
using TOEICMaster.Application.ExerciseBank;
using TOEICMaster.Application.Knowledge;
using TOEICMaster.Application.Profile;
using TOEICMaster.Application.Speaking;
using TOEICMaster.Application.Writing.Admin;
using TOEICMaster.Application.Writing.Attempts;

namespace TOEICMaster.Api.ExceptionHandling;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string UnauthorizedType = "https://tools.ietf.org/html/rfc9110#section-15.5.2";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string ConflictType = "https://tools.ietf.org/html/rfc9110#section-15.5.10";
    private const string PayloadTooLargeType = "https://tools.ietf.org/html/rfc9110#section-15.5.14";
    private const string UnprocessableEntityType = "https://tools.ietf.org/html/rfc9110#section-15.5.21";
    private const string TooManyRequestsType = "https://tools.ietf.org/html/rfc6585#section-4";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = MapProblem(exception);
        if (problem is null)
            return false;

        if (exception is DbUpdateException or PostgresException or ObjectNotFoundException or BucketNotFoundException)
            logger.LogWarning(exception, "Handled infrastructure exception mapped to HTTP {StatusCode}.", problem.Status);

        await ProblemDetailsResponseWriter.WriteAsync(problemDetailsService, httpContext, exception, problem,
            cancellationToken);
        return true;
    }

    private static ProblemDetails? MapProblem(Exception exception) => exception switch
    {
        // Authentication & Profile
        InvalidProfilePasswordException => Simple(StatusCodes.Status400BadRequest,
            "Mật khẩu hiện tại không đúng hoặc tài khoản không có mật khẩu.", BadRequestType),
        InvalidAvatarException => Simple(StatusCodes.Status400BadRequest,
            "Ảnh đại diện phải là PNG hoặc JPEG hợp lệ, tối đa 2 MiB.", BadRequestType),
        UnauthorizedAccessException => Simple(StatusCodes.Status401Unauthorized,
            "Phiên đăng nhập không hợp lệ.", UnauthorizedType),
        InvalidCredentialsException => Simple(StatusCodes.Status401Unauthorized,
            "Email hoặc mật khẩu không chính xác.", UnauthorizedType),
        InvalidSessionException => Simple(StatusCodes.Status401Unauthorized,
            "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.", UnauthorizedType),
        InvalidGoogleCredentialException => Simple(StatusCodes.Status401Unauthorized,
            "Google credential không hợp lệ hoặc đã hết hạn.", UnauthorizedType),
        ExternalAccountLinkRequiredException => Simple(StatusCodes.Status409Conflict,
            "Email này đã thuộc một tài khoản khác. Hãy xác minh email, đăng nhập bằng mật khẩu rồi liên kết Google.",
            ConflictType),
        InvalidEmailVerificationException => Simple(StatusCodes.Status400BadRequest,
            "Mã xác minh không chính xác, đã hết hạn hoặc đã vượt quá số lần thử.", BadRequestType),
        InvalidPasswordResetCodeException => Simple(StatusCodes.Status400BadRequest,
            "Mã đặt lại mật khẩu không chính xác, đã hết hạn hoặc đã vượt quá số lần thử.", BadRequestType),
        InvalidPasswordResetTokenException => Simple(StatusCodes.Status400BadRequest,
            "Yêu cầu đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.", BadRequestType),
        DuplicateEmailException => Simple(StatusCodes.Status409Conflict,
            "Email này đã được sử dụng.", ConflictType),

        // AdminUsers
        AdminUserNotFoundException notFound => Simple(StatusCodes.Status404NotFound, notFound.Message, NotFoundType),
        AdminUserOperationConflictException conflict => WithCode(StatusCodes.Status409Conflict, conflict.Message,
            ConflictType, conflict.Code),
        AdminUserRateLimitException rateLimit => WithCode(StatusCodes.Status429TooManyRequests, rateLimit.Message,
            TooManyRequestsType, rateLimit.Code),

        // ExamAttempts
        ExamAttemptNotFoundException notFound => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Không tìm thấy dữ liệu yêu cầu",
            Detail = notFound.Message,
            Type = NotFoundType
        },
        ExamAttemptConflictException conflict => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Xung đột trạng thái bài thi",
            Detail = conflict.Message,
            Type = ConflictType
        },
        ExamAttemptValidationException validation => new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Yêu cầu không hợp lệ",
            Detail = validation.Message,
            Type = BadRequestType
        },

        // Exams
        ExamNotFoundException => Simple(StatusCodes.Status404NotFound,
            "Không tìm thấy đề thi, revision hoặc barem yêu cầu.", NotFoundType),
        ExamConflictException => Simple(StatusCodes.Status409Conflict,
            "Mã đề thi đã tồn tại.", ConflictType),
        ExamPublicationException publication => Validation(StatusCodes.Status400BadRequest,
            "Đề thi chưa đạt điều kiện xuất bản.", BadRequestType,
            new Dictionary<string, string[]> { ["exam"] = publication.Errors.ToArray() }),
        ExamWorkbookFormatException workbook => Validation(StatusCodes.Status400BadRequest,
            "Workbook import không hợp lệ.", BadRequestType,
            new Dictionary<string, string[]> { ["workbook"] = workbook.Errors.ToArray() }),
        InvalidExamMediaException => Simple(StatusCodes.Status400BadRequest,
            "Tệp media không hợp lệ hoặc vượt quá giới hạn cho phép.", BadRequestType),
        ExamMediaUploadIntentConflictException conflict => Simple(StatusCodes.Status409Conflict,
            conflict.Message, ConflictType),

        // Knowledge
        KnowledgeNotFoundException notFound => Simple(StatusCodes.Status404NotFound, notFound.Message, NotFoundType),
        KnowledgeConflictException conflict => Simple(StatusCodes.Status409Conflict, conflict.Message, ConflictType),
        KnowledgeMediaTooLargeException tooLarge => Simple(StatusCodes.Status413PayloadTooLarge, tooLarge.Message,
            PayloadTooLargeType),
        InvalidKnowledgeMediaException invalidMedia => Validation(StatusCodes.Status400BadRequest,
            invalidMedia.Message, BadRequestType,
            new Dictionary<string, string[]> { ["file"] = [invalidMedia.Message] }),
        InvalidKnowledgeContentException invalidContent => Validation(StatusCodes.Status400BadRequest,
            invalidContent.Message, BadRequestType,
            new Dictionary<string, string[]> { [invalidContent.Field] = [invalidContent.Message] }),

        // LR Exercise Bank & Practice
        LrExerciseBankConflictException conflict => Simple(StatusCodes.Status409Conflict, conflict.Message, ConflictType),
        LrExerciseBankImportException import => Simple(StatusCodes.Status400BadRequest, import.Message, BadRequestType),
        InvalidLrExerciseBankItemException invalid => Simple(StatusCodes.Status400BadRequest, invalid.Message,
            BadRequestType),
        LrPracticeItemNotFoundException notFound => Simple(StatusCodes.Status404NotFound, notFound.Message,
            NotFoundType),
        InvalidLrPracticeSubmissionException submission => Simple(StatusCodes.Status400BadRequest, submission.Message,
            BadRequestType),
        InvalidLrPracticeRequestException request => Simple(StatusCodes.Status400BadRequest, request.Message,
            BadRequestType),

        // Speaking
        SpeakingNotFoundException => Simple(StatusCodes.Status404NotFound, exception.Message, NotFoundType),
        SpeakingConflictException conflict => WithCode(StatusCodes.Status409Conflict, conflict.Message, ConflictType,
            conflict.Code),
        SpeakingConcurrencyException => WithCode(StatusCodes.Status409Conflict, exception.Message, ConflictType,
            "VERSION_CONFLICT"),
        SpeakingPublishRequirementsException requirements => Validation(StatusCodes.Status422UnprocessableEntity,
            requirements.Message, UnprocessableEntityType,
            requirements.Errors.ToDictionary(pair => pair.Key, pair => pair.Value)),
        SpeakingMediaTooLargeException => Simple(StatusCodes.Status413PayloadTooLarge, exception.Message,
            PayloadTooLargeType),
        InvalidSpeakingMediaException => Simple(StatusCodes.Status400BadRequest, exception.Message, BadRequestType),

        // Writing
        WritingResourceNotFoundException => Simple(StatusCodes.Status404NotFound,
            "Không tìm thấy tài nguyên Writing.", NotFoundType),
        WritingConflictException conflict => Simple(StatusCodes.Status409Conflict, conflict.Message, ConflictType),
        InvalidWritingImageException => Simple(StatusCodes.Status400BadRequest,
            "Ảnh đề bài phải là PNG hoặc JPEG hợp lệ, tối đa 5 MiB.", BadRequestType),
        InvalidWritingImportException import => Simple(StatusCodes.Status400BadRequest, import.Message, BadRequestType),
        WritingAttemptNotFoundException => Simple(StatusCodes.Status404NotFound,
            "Không tìm thấy lượt làm Writing.", NotFoundType),
        WritingAttemptConflictException conflict => Simple(StatusCodes.Status409Conflict, conflict.Message,
            ConflictType),

        // Storage & Database Infrastructure Faults
        ObjectNotFoundException or BucketNotFoundException or FileNotFoundException => Simple(
            StatusCodes.Status404NotFound, "Không tìm thấy tài nguyên yêu cầu.", NotFoundType),
        DbUpdateConcurrencyException => Simple(StatusCodes.Status409Conflict,
            "Dữ liệu đã được cập nhật bởi một thao tác khác.", ConflictType),
        DbUpdateException { InnerException: PostgresException pg } => MapPostgresException(pg),
        PostgresException pg => MapPostgresException(pg),

        _ => null
    };

    private static ProblemDetails? MapPostgresException(PostgresException pg) => pg.SqlState switch
    {
        PostgresErrorCodes.UniqueViolation => Simple(StatusCodes.Status409Conflict,
            "Dữ liệu bị trùng lặp với bản ghi hiện có.", ConflictType),
        PostgresErrorCodes.StringDataRightTruncation => Simple(StatusCodes.Status400BadRequest,
            "Giá trị nhập vượt quá độ dài cho phép.", BadRequestType),
        PostgresErrorCodes.ForeignKeyViolation => Simple(StatusCodes.Status409Conflict,
            "Dữ liệu đang được tham chiếu hoặc liên kết không hợp lệ.", ConflictType),
        _ => null
    };

    private static ProblemDetails Simple(int status, string title, string type) => new()
    {
        Status = status,
        Title = title,
        Type = type
    };

    private static ProblemDetails WithCode(int status, string title, string type, string code)
    {
        var problem = Simple(status, title, type);
        problem.Extensions["code"] = code;
        return problem;
    }

    private static ValidationProblemDetails Validation(int status, string title, string type,
        IDictionary<string, string[]> errors) => new(errors)
        {
            Status = status,
            Title = title,
            Type = type
        };
}

internal static class ProblemDetailsResponseWriter
{
    public static async Task WriteAsync(
        IProblemDetailsService problemDetailsService,
        HttpContext httpContext,
        Exception exception,
        ProblemDetails problem,
        CancellationToken cancellationToken)
    {
        var status = problem.Status ?? StatusCodes.Status500InternalServerError;
        problem.Status = status;
        if (!problem.Extensions.TryGetValue("traceId", out var traceId) || traceId is null)
            problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = status;
        if (!await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        }))
        {
            await httpContext.Response.WriteAsJsonAsync(
                problem,
                problem.GetType(),
                options: null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);
        }
    }
}
