using System.ComponentModel.DataAnnotations;

namespace Assistech.Api.Infrastructure;

public static class Validacao
{
    public static IResult ErroDeValidacao<T>(T entrada) where T : class
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(entrada, new ValidationContext(entrada), resultados, validateAllProperties: true);

        if (resultados.Count == 0) return Results.NoContent();

        var erros = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var resultado in resultados)
        {
            var chave = resultado.MemberNames.FirstOrDefault() ?? "geral";
            var mensagem = resultado.ErrorMessage ?? "Valor invalido.";

            erros[chave] = erros.TryGetValue(chave, out var existentes)
                ? [.. existentes, mensagem]
                : [mensagem];
        }

        return Results.ValidationProblem(erros, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    public static IResult NaoEncontrado(string mensagem) => Results.NotFound(new { erro = mensagem });

    public static IResult Falha(string mensagem) => Results.Json(new { erro = mensagem }, statusCode: StatusCodes.Status400BadRequest);

    public static IResult SemPermissao(string mensagem) => Results.Json(new { erro = mensagem }, statusCode: StatusCodes.Status403Forbidden);
}
