using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Somnia.Api.Compartilhado.Auth;

public sealed class ValidarOrigemAuthAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var request = context.HttpContext.Request;
        if (HttpMethods.IsGet(request.Method))
            return;

        if (!request.IsHttps)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = 400,
                Title = "HTTPS obrigatorio para autenticacao."
            }) { StatusCode = 400 };
            return;
        }

        var origin = request.Headers.Origin.ToString();
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var sameOrigin = $"{request.Scheme}://{request.Host}";
        var allowed = string.IsNullOrEmpty(origin) || origin == sameOrigin
            || allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);

        if (request.Headers["X-Somnia-Client"] != "web" || !allowed)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = 403,
                Title = "Origem ou cabecalho de autenticacao invalido."
            }) { StatusCode = 403 };
        }
    }
}
