namespace PastebinClone.Api.Models;

public sealed record ProblemDetails(string Message);

public sealed record ValidationProblemDetails(Dictionary<string, string[]> Errors);
