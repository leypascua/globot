using Globot.Web.App.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace Globot.Web.App.Routes;

public static class Requests
{
    public static ApiResult Get([FromServices]GlobRequestQueue globRequests)
    {
        var results = globRequests.GetAll();
        return ApiResult.Success(results);
    }

    public static async Task<JsonHttpResult<ApiResult>> Post([FromQuery]string key, [FromQuery]string sources, [FromServices]GlobRequestQueue globRequests)
    {
        var sourceKeys = (sources ?? string.Empty).Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        
        var results = new List<PushGlobRequestContext>();

        foreach (string sourceKey in sourceKeys)
        {
            var request = new PushGlobRequest(sourceKey);
            var (requestContext, isSuccessful) = await globRequests.Add(request);

            if (isSuccessful)
            {
                results.Add(requestContext);
            }
        }

        ApiResult result = ApiResult.Success(results);
        return TypedResults.Json(result, statusCode: 201);
    }
}
