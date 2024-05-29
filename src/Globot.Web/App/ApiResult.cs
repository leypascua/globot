namespace Globot.Web;

public class ApiResult 
{
    public string[]? Errors { get; set; }
    public object? Data { get; set; }
    public bool IsSuccessful => Errors == null || Errors.Length == 0;

    public static ApiResult Error(string message)
    {
        return new ApiResult
        {
            Errors = [message]
        };
    }

    public static ApiResult Error(params string[] errors)
    {
        return new ApiResult
        {
            Errors = errors
        };
    }

    public static ApiResult Success()
    {
        return new ApiResult();
    }

    public static ApiResultWithData<T> Success<T>(T data)
    {
        return new ApiResultWithData<T>
        {
            Data = data,
            Errors = null
        };
    }
}

public class ApiResultWithData<T> : ApiResult
{   
    public new T? Data { get; set; }
}
