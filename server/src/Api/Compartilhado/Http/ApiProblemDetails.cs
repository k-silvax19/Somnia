using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Somnia.Api.Compartilhado.Http;

public static class ApiProblemDetails
{
    public static void Completar(HttpContext context, ProblemDetails problem)
    {
        var status = problem.Status ?? context.Response.StatusCode;
        if (status < 400)
            status = StatusCodes.Status500InternalServerError;

        problem.Status = status;
        problem.Type = $"urn:somnia:problem:http:{status}";
        problem.Title = string.IsNullOrWhiteSpace(problem.Title) ? Titulo(status) : problem.Title;
        problem.Detail = string.IsNullOrWhiteSpace(problem.Detail) ? Detalhe(status) : problem.Detail;
        if (status >= 500)
        {
            problem.Title = Titulo(status);
            problem.Detail = Detalhe(status);
        }
        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        context.Response.Headers.CacheControl = "no-store";
    }

    public static Task EscreverAsync(HttpContext context, int status, CancellationToken ct = default)
    {
        context.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = Titulo(status) };
        Completar(context, problem);
        return context.Response.WriteAsJsonAsync(problem, (JsonSerializerOptions?)null,
            "application/problem+json", ct);
    }

    private static string Titulo(int status) => status switch
    {
        400 => "Requisição inválida",
        401 => "Não autenticado",
        403 => "Acesso proibido",
        404 => "Recurso não encontrado",
        405 => "Método não permitido",
        409 => "Conflito",
        413 => "Requisição muito grande",
        415 => "Formato não suportado",
        429 => "Limite de requisições excedido",
        503 => "Serviço temporariamente indisponível",
        502 => "Resposta inválida do serviço externo",
        _ => status >= 500 ? "Erro interno do servidor" : "Não foi possível concluir a requisição"
    };

    private static string Detalhe(int status) => status switch
    {
        400 => "Verifique os dados enviados e os erros de validação por campo.",
        401 => "Envie um token de acesso válido para acessar este recurso.",
        403 => "A requisição não atende às regras de acesso ou de proteção da operação.",
        404 => "O recurso solicitado não existe ou não está acessível.",
        405 => "O método HTTP informado não está disponível neste endereço.",
        409 => "A operação conflita com o estado atual do recurso.",
        413 => "O corpo da requisição excede o limite permitido.",
        415 => "O formato do corpo da requisição não é aceito por esta operação.",
        429 => "Aguarde antes de tentar novamente.",
        503 => "O serviço solicitado está indisponível. Tente novamente mais tarde.",
        502 => "O serviço externo retornou uma resposta que não pôde ser processada.",
        _ => status >= 500 ? "Ocorreu uma falha interna. Informe o traceId ao solicitar suporte."
            : "Não foi possível concluir a requisição."
    };
}

// Também alcança respostas criadas por validação automática e filtros de segurança.
public sealed class ApiProblemDetailsFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { Value: ProblemDetails problem } result)
            return;
        problem.Status ??= result.StatusCode;
        ApiProblemDetails.Completar(context.HttpContext, problem);
        result.StatusCode = problem.Status;
        result.ContentTypes.Clear();
        result.ContentTypes.Add("application/problem+json");
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
