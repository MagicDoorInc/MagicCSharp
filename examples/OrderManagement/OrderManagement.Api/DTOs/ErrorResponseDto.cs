namespace OrderManagement.Api.DTOs;

public record ErrorResponseDto
{
    public string Error { get; init; } = string.Empty;

    public static ErrorResponseDto FromMessage(string message)
    {
        return new ErrorResponseDto
        {
            Error = message,
        };
    }
}