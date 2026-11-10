using Microsoft.AspNetCore.Mvc;
using FluentResults;
using Somnia.Dominio.Compartilhado;

namespace Somnia.Api.Compartilhado.Http;

public static class ProblemExtensions
{
    public static IActionResult ResultProblem(this ControllerBase controller, ResultBase result)
    {
        var primeiroErro = result.Errors.FirstOrDefault();
        var tipo = primeiroErro?.Metadata.GetValueOrDefault(nameof(TipoErro)) as TipoErro?;

        var status = tipo switch
        {
            TipoErro.Validacao => 400,
            TipoErro.NaoEncontrado => 404,
            TipoErro.Conflito => 409,
            TipoErro.NaoAutenticado => 401,
            TipoErro.Indisponivel => 503,
            _ => 500
        };

        ProblemDetails problem;

        if (tipo == TipoErro.Validacao)
        {
            var errors = result.Errors
                .GroupBy(error => error.Metadata.GetValueOrDefault("Campo")?.ToString() ?? string.Empty)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray());
            problem = new ValidationProblemDetails(errors) { Title = "Requisição inválida" };
        }
        else
        {
            problem = new ProblemDetails
            {
                Title = status switch
                {
                    401 => "Não autenticado",
                    404 => "Recurso não encontrado",
                    409 => "Conflito",
                    503 => "Serviço temporariamente indisponível",
                    _ => "Erro interno do servidor"
                },
                Detail = status == 500 ? null : primeiroErro?.Message
            };
        }

        problem.Status = status;

        if (problem is ValidationProblemDetails)
        {
            var validationResponse = new ObjectResult(problem) { StatusCode = status };
            validationResponse.ContentTypes.Add("application/problem+json");
            return validationResponse;
        }

        var response = new ObjectResult(problem) { StatusCode = status };
        response.ContentTypes.Add("application/problem+json");

        return response;
    }
}
