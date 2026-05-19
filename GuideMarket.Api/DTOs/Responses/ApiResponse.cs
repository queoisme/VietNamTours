namespace GuideMarket.Api.DTOs.Responses;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string Message { get; set; } = "OK";
    public string[]? Errors { get; set; }
    public PaginationMeta? Meta { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "OK", PaginationMeta? meta = null) =>
        new() { Success = true, Data = data, Message = message, Meta = meta };

    public static ApiResponse<T> Fail(string message, string[]? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };
}

public class PaginationMeta
{
    public int Page { get; set; }
    public int Size { get; set; }
    public long Total { get; set; }
}
