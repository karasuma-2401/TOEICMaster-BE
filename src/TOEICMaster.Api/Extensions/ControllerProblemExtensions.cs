using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TOEICMaster.Api.ExceptionHandling;

namespace TOEICMaster.Api.Extensions;

public static class ControllerProblemExtensions
{
    public static ObjectResult BadRequestProblem(this ControllerBase controller, string title, string field = "file") =>
        controller.ValidationProblemResponse(new Dictionary<string, string[]> { [field] = [title] }, title);

    public static ObjectResult ValidationProblemResponse(this ControllerBase controller,
        IDictionary<string, string[]> errors, string title = "One or more validation errors occurred.") =>
        ProblemDetailsResponseWriter.CreateResult(controller.HttpContext,
            CreateValidationProblem(controller, errors, title, title));

    public static ObjectResult ProblemResponse(this ControllerBase controller, int status, string title,
        string? detail = null, string? code = null)
    {
        ProblemDetails problem = status == StatusCodes.Status400BadRequest
            ? CreateValidationProblem(controller,
                new Dictionary<string, string[]> { [string.Empty] = [title] },
                title, detail ?? title)
            : controller.ProblemDetailsFactory.CreateProblemDetails(controller.HttpContext,
                statusCode: status, title: title, detail: detail ?? title);
        if (code is not null)
            problem.Extensions["code"] = code;
        return ProblemDetailsResponseWriter.CreateResult(controller.HttpContext, problem);
    }

    private static ValidationProblemDetails CreateValidationProblem(ControllerBase controller,
        IDictionary<string, string[]> errors, string title, string detail)
    {
        var modelState = new ModelStateDictionary();
        foreach (var (field, messages) in errors)
        {
            foreach (var message in messages)
                modelState.AddModelError(field, message);
        }
        return controller.ProblemDetailsFactory.CreateValidationProblemDetails(controller.HttpContext, modelState,
            statusCode: StatusCodes.Status400BadRequest, title: title, detail: detail);
    }
}
