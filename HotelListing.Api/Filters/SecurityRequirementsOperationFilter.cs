using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HotelListing.Api.Filters;

public class SecurityRequirementsOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var authAttributes = context.MethodInfo.DeclaringType?
            .GetCustomAttributes(true)
            .Union(context.MethodInfo.GetCustomAttributes(true))
            .OfType<AuthorizeAttribute>();

        if (authAttributes?.Any() != true)
            return;

        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });

        var securityRequirements = new List<OpenApiSecurityRequirement>();

        // API Key
        if (context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
            .Any(attr => attr.GetType().Name.Contains("ApiKey")) == true)
        {
            securityRequirements.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("ApiKey")] = new List<string>()
            });
        }

        // Basic
        if (context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
            .Any(attr => attr.GetType().Name.Contains("Basic")) == true)
        {
            securityRequirements.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Basic")] = new List<string>()
            });
        }

        // Default to Bearer
        if (securityRequirements.Count == 0)
        {
            securityRequirements.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer")] = new List<string>()
            });
        }

        operation.Security = securityRequirements;
    }
}
