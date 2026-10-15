using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Extensions;

namespace IdeioCreative.Tools;

public static class UrlNormalizationExtensions
{
    public static IApplicationBuilder UseLowercaseUrlRedirects(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var request = context.Request;
            // Only normalize navigation requests, never form submissions or internal error pages.
            if ((HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) &&
                context.Features.Get<IExceptionHandlerFeature>() is null &&
                context.Features.Get<IStatusCodeReExecuteFeature>() is null)
            {
                var path = request.Path.Value;
                var lowercasePath = path?.ToLowerInvariant();
                if (!string.Equals(path, lowercasePath, StringComparison.Ordinal))
                {
                    // Keep query values intact and use the same origin even for paths starting with //.
                    var location = UriHelper.BuildAbsolute(request.Scheme, request.Host,
                        request.PathBase, new PathString(lowercasePath), request.QueryString);
                    context.Response.Redirect(location, permanent: true);
                    return;
                }
            }

            await next();
        });
}
