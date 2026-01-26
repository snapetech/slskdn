// <copyright file="ValidateCsrfForCookiesOnlyAttribute.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Core.Security;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;

/// <summary>
/// Validates CSRF tokens for cookie-based authentication ONLY.
/// Automatically exempts:
/// - Requests with Authorization header (JWT/Bearer tokens)
/// - Requests with API key authentication
/// - Safe HTTP methods (GET, HEAD, OPTIONS, TRACE)
/// - Requests without cookies (pure API clients)
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class ValidateCsrfForCookiesOnlyAttribute : Attribute, IAsyncAuthorizationFilter
{
    private static readonly ILogger Log = Serilog.Log.ForContext<ValidateCsrfForCookiesOnlyAttribute>();
    
    /// <summary>
    /// Filter order - set to run early in the authorization pipeline.
    /// </summary>
    public int Order => -1000;

    /// <summary>
    /// Safe HTTP methods that don't need CSRF protection.
    /// </summary>
    private static readonly string[] SafeMethods = { "GET", "HEAD", "OPTIONS", "TRACE" };

    /// <summary>
    /// Executes the CSRF validation filter.
    /// </summary>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        
        // 2. Exempt safe HTTP methods (GET, HEAD, OPTIONS, TRACE) - CHECK FIRST
        // NOTE: This check must happen BEFORE any other checks to ensure GET requests are never validated
        if (SafeMethods.Contains(request.Method, StringComparer.OrdinalIgnoreCase))
        {
            // Verbose level - safe methods are very common and don't need logging
            Log.Verbose("[CSRF] Skipping validation for safe method: {Method} {Path}", request.Method, request.Path);
            return; // Safe method - no CSRF needed
        }
        
        Log.Verbose("[CSRF] Processing non-safe method: {Method} {Path}", request.Method, request.Path);
        
        // 1. Exempt endpoints with [AllowAnonymous] attribute (like login)
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() != null)
        {
            Log.Verbose("[CSRF] Skipping validation for anonymous endpoint: {Path}", request.Path);
            return; // Anonymous endpoint - no CSRF needed (e.g., login)
        }

        // 3. Exempt requests with Authorization header (JWT/Bearer tokens)
        if (request.Headers.ContainsKey("Authorization"))
        {
            var authHeader = request.Headers["Authorization"].ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                Log.Verbose("[CSRF] Skipping validation for JWT Bearer token");
                return; // JWT auth - no CSRF needed
            }
        }

        // 4. Exempt requests with API key header
        if (request.Headers.ContainsKey("X-API-Key"))
        {
            Log.Verbose("[CSRF] Skipping validation for API key");
            return; // API key auth - no CSRF needed
        }

        // 5. Exempt requests with API key in query string (for compatibility)
        if (request.Query.ContainsKey("api_key") || request.Query.ContainsKey("apikey"))
        {
            Log.Verbose("[CSRF] Skipping validation for API key in query string");
            return; // API key auth - no CSRF needed
        }

        // 6. Check if request has any authentication cookies
        var hasCookies = request.Cookies.Any();
        if (!hasCookies)
        {
            Log.Verbose("[CSRF] Skipping validation - no cookies present (pure API client)");
            return; // No cookies - pure API client, no CSRF needed
        }

        // 7. This is a cookie-based request (web UI) - validate CSRF token
        Log.Verbose("[CSRF] Validating CSRF token for cookie-based request: {Method} {Path}", 
            request.Method, request.Path);

        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
            Log.Verbose("[CSRF] Token validation successful for {Path}", request.Path);
        }
        catch (AntiforgeryValidationException ex)
        {
            Log.Warning("[CSRF] Token validation failed for {Method} {Path}: {Message}", 
                request.Method, request.Path, ex.Message);
            
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = 400,
                Title = "CSRF token validation failed",
                Detail = "This request requires a valid CSRF token. If you're using the web UI, please try refreshing the page. If you're using the API, use JWT or API key authentication instead of cookies."
            };
            problem.Extensions["hint"] = "Web UI: Refresh page | API: Use Authorization header with Bearer token or X-API-Key header";
            var result = new BadRequestObjectResult(problem);
            result.ContentTypes.Add("application/problem+json");
            context.Result = result;
        }
        catch (Exception ex)
        {
            // Catch any other exceptions from ValidateRequestAsync
            Log.Error(ex, "[CSRF] Unexpected error during token validation for {Method} {Path}", 
                request.Method, request.Path);
            
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = 400,
                Title = "CSRF validation error",
                Detail = ex.Message
            };
            var result = new BadRequestObjectResult(problem);
            result.ContentTypes.Add("application/problem+json");
            context.Result = result;
        }
    }
}
