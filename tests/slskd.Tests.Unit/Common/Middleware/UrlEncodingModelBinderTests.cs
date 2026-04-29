// <copyright file="UrlEncodingModelBinderTests.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
// </copyright>

// <copyright file="UrlEncodingModelBinderTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Common.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using slskd;
using Xunit;

public class UrlEncodingModelBinderTests
{
    [Fact]
    public async Task BindModelAsync_WithEncodedRouteValue_BindsDecodedValue()
    {
        var binder = new UrlEncodingModelBinder();
        var context = CreateBindingContext(
            modelName: "filename",
            routeTemplate: "api/files/{filename}",
            rawTarget: "/api/files/hello%20world");

        await binder.BindModelAsync(context);

        Assert.True(context.Result.IsModelSet);
        Assert.Equal("hello world", Assert.IsType<string>(context.Result.Model));
    }

    [Fact]
    public async Task BindModelAsync_WithMalformedQueryInRawTarget_DoesNotThrow()
    {
        var binder = new UrlEncodingModelBinder();
        var context = CreateBindingContext(
            modelName: "filename",
            routeTemplate: "api/files/{filename}",
            rawTarget: "/api/files/hello%20world?bad=|");

        await binder.BindModelAsync(context);

        Assert.True(context.Result.IsModelSet);
        Assert.Equal("hello world", Assert.IsType<string>(context.Result.Model));
    }

    private static DefaultModelBindingContext CreateBindingContext(string modelName, string routeTemplate, string rawTarget)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<IHttpRequestFeature>(new HttpRequestFeature
        {
            RawTarget = rawTarget,
        });

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor
            {
                AttributeRouteInfo = new AttributeRouteInfo
                {
                    Template = routeTemplate,
                },
            });

        return new DefaultModelBindingContext
        {
            ActionContext = actionContext,
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(string)),
            ModelName = modelName,
            ModelState = new ModelStateDictionary(),
            ValueProvider = new RouteValueProvider(BindingSource.Path, new RouteValueDictionary(), System.Globalization.CultureInfo.InvariantCulture),
        };
    }
}
