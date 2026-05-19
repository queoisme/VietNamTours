namespace GuideMarket.Api.Exceptions;

public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message = "You do not have permission to perform this action")
        : base(message) { }
}
