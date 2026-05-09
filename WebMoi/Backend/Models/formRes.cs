public static class ApiResponse
{
    public static object Success(string message, object? data = null)
    {
        return new
        {
            success = true,
            message,
            data
        };
    }

    public static object Fail(string message)
    {
        return new
        {
            success = false,
            message
        };
    }
}