using LabExperiment.Domain;
using Microsoft.AspNetCore.Http;

namespace LabExperiment.Presentation.Results;

public static class ApiResults
{
    public static IResult Problem(Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException();
        }

        return Microsoft.AspNetCore.Http.Results.Problem(
            title: GetTitle(result.Error),
            detail: GetDetail(result.Error),
            type: GetType(result.Error.Type),
            statusCode: GetStatusCode(result.Error.Type),
            extensions: GetErrors(result));

        static string GetTitle(Error error) =>
            error.Type switch
            {
                ErrorType.Validation => error.Code,
                ErrorType.Problem => error.Code,
                ErrorType.NotFound => error.Code,
                ErrorType.Conflict => error.Code,
                ErrorType.Forbidden => error.Code,
                ErrorType.Unprocessable => error.Code,
                _ => "Server failure"
            };

        static string GetDetail(Error error) =>
            error.Type switch
            {
                ErrorType.Validation => error.Description,
                ErrorType.Problem => error.Description,
                ErrorType.NotFound => error.Description,
                ErrorType.Conflict => error.Description,
                ErrorType.Forbidden => error.Description,
                ErrorType.Unprocessable => error.Description,
                _ => "An unexpected error occurred"
            };

        static string GetType(ErrorType errorType) =>
            errorType switch
            {
                ErrorType.Validation => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
                ErrorType.Problem => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
                ErrorType.NotFound => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.5",
                ErrorType.Conflict => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.10",
                ErrorType.Forbidden => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.4",
                ErrorType.Unprocessable => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.21",
                _ => "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1"
            };

        static int GetStatusCode(ErrorType errorType) =>
            errorType switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.Problem => StatusCodes.Status400BadRequest,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                ErrorType.Unprocessable => StatusCodes.Status422UnprocessableEntity,
                _ => StatusCodes.Status500InternalServerError
            };

        static Dictionary<string, object?>? GetErrors(Result result)
        {
            if (result.Error is IProblemExtensions withExtensions)
            {
                return new Dictionary<string, object?>(withExtensions.Extensions);
            }

            if (result.Error is not ValidationError validationError)
            {
                return null;
            }

            return new Dictionary<string, object?>
            {
                { "errors", validationError.Errors }
            };
        }
    }
}
