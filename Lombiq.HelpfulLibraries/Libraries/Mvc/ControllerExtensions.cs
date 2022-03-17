using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using System;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Mvc;

public static class ControllerExtensions
{
    /// <summary>
    /// Will redirect to the given URL if that is local. Otherwise it will redirect to "~/".
    /// </summary>
    /// <param name="redirectUrl">Local URL to redirect to.</param>
    /// <returns>Redirect action result.</returns>
    /// <remarks>
    /// <para>
    /// Could be part of Orchard but <see href="https://github.com/OrchardCMS/OrchardCore/issues/2830">it won't</see>.
    /// </para>
    /// </remarks>
    public static RedirectResult RedirectToLocal(this Controller controller, string redirectUrl) =>
        controller.Redirect(controller.Url.IsLocalUrl(redirectUrl) ? redirectUrl : "~/");

    /// <summary>
    /// Uses <see cref="Routing.UrlHelperExtensions.DisplayContentItem"/> extension method to redirect to this <see
    /// cref="ContentItem"/>'s display page.
    /// </summary>
    public static RedirectResult RedirectToContentDisplay(this Controller controller, IContent content) =>
        controller.Redirect(controller.Url.DisplayContentItem(content));

    /// <summary>
    /// Similar to <c>controller.Json(data)</c>, but catches any exception in the <paramref name="dataFactory"/> and if
    /// one happens returns a JSON with the <c>error</c> property. If run from a local dev machine the <c>data</c>
    /// property is also filled with the exception string.
    /// </summary>
    public static async Task<JsonResult> SafeJsonAsync<T>(this Controller controller, Func<Task<T>> dataFactory)
    {
        try
        {
            return controller.Json(await dataFactory());
        }
        catch (Exception exception)
        {
            var context = controller.HttpContext;

            if (exception.IsFatal())
            {
                var logger = context
                    .RequestServices
                    .GetService<ILoggerFactory>()
                    .CreateLogger(controller.GetType());
                logger.LogError(
                    exception,
                    "An error has occurred while generating a JSON result. (Request Route Values: {RouteValues})",
                    JsonConvert.SerializeObject(context.Request.RouteValues));
            }

            return controller.Json(context.IsDevelopmentAndLocalhost()
                ? new { error = exception.Message, data = exception.ToString() }
                : new
                {
                    error = context
                        .RequestServices
                        .GetService<IStringLocalizerFactory>()?
                        .Create(controller.GetType())["An error has occurred while trying to process your request."]
                        .Value ?? "An error has occurred while trying to process your request.",
                });
        }
    }
}
