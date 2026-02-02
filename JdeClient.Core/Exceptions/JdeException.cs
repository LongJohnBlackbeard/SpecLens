namespace JdeClient.Core.Exceptions;

/// <summary>
/// Base exception for all JDE client errors
/// </summary>
public class JdeException : Exception
{
    /// <summary>
    /// JDE API result code (if applicable)
    /// </summary>
    public int? ResultCode { get; }

    public JdeException(string message) : base(message)
    {
    }

    public JdeException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public JdeException(string message, int resultCode) : base(message)
    {
        ResultCode = resultCode;
    }

    public JdeException(string message, int resultCode, Exception innerException) : base(message, innerException)
    {
        ResultCode = resultCode;
    }
}

/// <summary>
/// Exception thrown when JDE connection fails or is lost
/// </summary>
public class JdeConnectionException : JdeException
{
    public JdeConnectionException(string message) : base(message)
    {
    }

    public JdeConnectionException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public JdeConnectionException(string message, int resultCode) : base(message, resultCode)
    {
    }
}

/// <summary>
/// Exception thrown when a JDE API call fails
/// </summary>
public class JdeApiException : JdeException
{
    /// <summary>
    /// Name of the API function that failed
    /// </summary>
    public string? ApiFunction { get; }

    public JdeApiException(string apiFunction, string message) : base($"{apiFunction} failed: {message}")
    {
        ApiFunction = apiFunction;
    }

    public JdeApiException(string apiFunction, string message, int resultCode)
        : base($"{apiFunction} failed: {message} (Result: {resultCode})", resultCode)
    {
        ApiFunction = apiFunction;
    }

    public JdeApiException(string apiFunction, string message, Exception innerException)
        : base($"{apiFunction} failed: {message}", innerException)
    {
        ApiFunction = apiFunction;
    }
}

/// <summary>
/// Exception thrown when a table operation fails
/// </summary>
public class JdeTableException : JdeException
{
    /// <summary>
    /// Name of the table involved in the error
    /// </summary>
    public string? TableName { get; }

    public JdeTableException(string tableName, string message) : base($"Table {tableName}: {message}")
    {
        TableName = tableName;
    }

    public JdeTableException(string tableName, string message, int resultCode)
        : base($"Table {tableName}: {message} (Result: {resultCode})", resultCode)
    {
        TableName = tableName;
    }

    public JdeTableException(string tableName, string message, Exception innerException)
        : base($"Table {tableName}: {message}", innerException)
    {
        TableName = tableName;
    }
}