using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TOEICMaster.Api.Configuration;

public sealed class MultipartFormFileOpenApiTransformer : IOpenApiOperationTransformer
{
    private static readonly HashSet<string> ExplodedFormFilePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(IFormFile.ContentType),
        nameof(IFormFile.ContentDisposition),
        nameof(IFormFile.Headers),
        nameof(IFormFile.Length),
        nameof(IFormFile.Name),
        nameof(IFormFile.FileName)
    };

    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var actionParameters = context.Description.ActionDescriptor.Parameters;
        var fileParameters = actionParameters
            .Where(parameter => typeof(IFormFile).IsAssignableFrom(parameter.ParameterType))
            .ToArray();
        if (fileParameters.Length == 0)
            return;

        var properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        var required = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fileParameter in fileParameters)
        {
            var fieldName = GetFormFieldName(fileParameter);
            properties[fieldName] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Format = "binary"
            };
            if (!IsNullableParameter(fileParameter))
                required.Add(fieldName);
        }

        foreach (var parameter in actionParameters)
        {
            if (typeof(IFormFile).IsAssignableFrom(parameter.ParameterType) ||
                parameter.ParameterType == typeof(CancellationToken) ||
                !IsFromFormParameter(parameter))
                continue;

            var fieldName = GetFormFieldName(parameter);
            var schema = await context.GetOrCreateSchemaAsync(parameter.ParameterType, null, cancellationToken);
            properties[fieldName] = schema;
            if (!IsNullableParameter(parameter))
                required.Add(fieldName);
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
            {
                ["multipart/form-data"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = properties,
                        Required = required.Count > 0 ? required : null
                    }
                }
            }
        };

        if (operation.Parameters is { Count: > 0 })
        {
            var toRemove = operation.Parameters
                .Where(parameter => parameter.Name is not null && ExplodedFormFilePropertyNames.Contains(parameter.Name))
                .ToArray();
            foreach (var item in toRemove)
                operation.Parameters.Remove(item);
        }
    }

    private static bool IsFromFormParameter(ParameterDescriptor parameter) =>
        parameter is ControllerParameterDescriptor controllerParameter &&
        controllerParameter.ParameterInfo.GetCustomAttribute<FromFormAttribute>() is not null;

    private static string GetFormFieldName(ParameterDescriptor parameter)
    {
        if (parameter is ControllerParameterDescriptor controllerParameter)
        {
            var fromForm = controllerParameter.ParameterInfo.GetCustomAttribute<FromFormAttribute>();
            if (!string.IsNullOrWhiteSpace(fromForm?.Name))
                return fromForm.Name;
        }

        return parameter.Name;
    }

    private static bool IsNullableParameter(ParameterDescriptor parameter)
    {
        if (Nullable.GetUnderlyingType(parameter.ParameterType) is not null)
            return true;

        if (parameter is not ControllerParameterDescriptor controllerParameter)
            return false;

        var nullability = new NullabilityInfoContext().Create(controllerParameter.ParameterInfo);
        return nullability.ReadState == NullabilityState.Nullable;
    }
}
