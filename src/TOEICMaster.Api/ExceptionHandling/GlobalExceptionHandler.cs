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
using TOEICMaster.Domain.Entities;

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
            "Current password is incorrect or the account does not have a password.", BadRequestType),
        InvalidAvatarException => Simple(StatusCodes.Status400BadRequest,
            "Avatar must be a valid PNG or JPEG image up to 2 MiB.", BadRequestType),
        UnauthorizedAccessException => Simple(StatusCodes.Status401Unauthorized,
            "Invalid login session.", UnauthorizedType),
        InvalidCredentialsException => Simple(StatusCodes.Status401Unauthorized,
            "Email or password is incorrect.", UnauthorizedType),
        InvalidSessionException => Simple(StatusCodes.Status401Unauthorized,
            "Invalid or expired login session.", UnauthorizedType),
        InvalidGoogleCredentialException => Simple(StatusCodes.Status401Unauthorized,
            "Google credential is invalid or has expired.", UnauthorizedType),
        ExternalAccountLinkRequiredException => Simple(StatusCodes.Status409Conflict,
            "This email already belongs to another account. Please verify your email, sign in with your password, and then link Google.",
            ConflictType),
        InvalidEmailVerificationException => Simple(StatusCodes.Status400BadRequest,
            "Verification code is incorrect, expired, or has exceeded the maximum number of attempts.", BadRequestType),
        InvalidPasswordResetCodeException => Simple(StatusCodes.Status400BadRequest,
            "Password reset code is incorrect, expired, or has exceeded the maximum number of attempts.", BadRequestType),
        InvalidPasswordResetTokenException => Simple(StatusCodes.Status400BadRequest,
            "Password reset request is invalid or has expired.", BadRequestType),
        DuplicateEmailException => Simple(StatusCodes.Status409Conflict,
            "This email is already in use.", ConflictType),

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
            Title = "Requested data not found",
            Detail = notFound.Message,
            Type = NotFoundType
        },
        ExamAttemptConflictException conflict => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Exam attempt state conflict",
            Detail = conflict.Message,
            Type = ConflictType
        },
        ExamAttemptValidationException validation => new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid request",
            Detail = validation.Message,
            Type = BadRequestType
        },

        // Exams
        ExamNotFoundException => Simple(StatusCodes.Status404NotFound,
            "Requested exam, revision, or scoring rubric not found.", NotFoundType),
        ExamConflictException => Simple(StatusCodes.Status409Conflict,
            "Exam code already exists.", ConflictType),
        ExamRevisionStateConflictException conflict => Simple(StatusCodes.Status409Conflict,
            conflict.Message, ConflictType),
        ExamPublicationException publication => Validation(StatusCodes.Status400BadRequest,
            "Exam does not meet publication requirements.", BadRequestType,
            new Dictionary<string, string[]> { ["exam"] = publication.Errors.ToArray() }),
        ExamWorkbookFormatException workbook => Validation(StatusCodes.Status400BadRequest,
            "Invalid import workbook.", BadRequestType,
            new Dictionary<string, string[]> { ["workbook"] = workbook.Errors.ToArray() }),
        InvalidExamMediaException => Simple(StatusCodes.Status400BadRequest,
            "Media file is invalid or exceeds the allowed limit.", BadRequestType),
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
            "Writing resource not found.", NotFoundType),
        WritingConflictException conflict => Simple(StatusCodes.Status409Conflict, conflict.Message, ConflictType),
        InvalidWritingImageException => Simple(StatusCodes.Status400BadRequest,
            "Prompt image must be a valid PNG or JPEG up to 5 MiB.", BadRequestType),
        InvalidWritingImportException import => Simple(StatusCodes.Status400BadRequest, import.Message, BadRequestType),
        WritingAttemptNotFoundException => Simple(StatusCodes.Status404NotFound,
            "Writing attempt not found.", NotFoundType),
        WritingAttemptConflictException conflict => Simple(StatusCodes.Status409Conflict, conflict.Message,
            ConflictType),

        // Storage & Database Infrastructure Faults
        ObjectNotFoundException or BucketNotFoundException or FileNotFoundException => Simple(
            StatusCodes.Status404NotFound, "Requested resource not found.", NotFoundType),
        DbUpdateConcurrencyException => Simple(StatusCodes.Status409Conflict,
            "The data has been updated by another operation.", ConflictType),
        DbUpdateException { InnerException: PostgresException pg } => MapPostgresException(pg),
        PostgresException pg => MapPostgresException(pg),

        _ => null
    };

    private static ProblemDetails? MapPostgresException(PostgresException pg) => pg.SqlState switch
    {
        PostgresErrorCodes.UniqueViolation => Simple(StatusCodes.Status409Conflict,
            "Data duplicates an existing record.", ConflictType),
        PostgresErrorCodes.StringDataRightTruncation => Simple(StatusCodes.Status400BadRequest,
            "Input value exceeds the allowed length.", BadRequestType),
        PostgresErrorCodes.ForeignKeyViolation => Simple(StatusCodes.Status409Conflict,
            "Data is being referenced or has an invalid association.", ConflictType),
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
