using Globot.Web.App.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace Globot.Web.App.Routes;

public static class Requests
{
    public static ApiResult Get([FromServices]GlobRequestService globRequests)
    {
        var results = globRequests.GetAll();
        return ApiResult.Success(results);
    }

    public static async Task<JsonHttpResult<ApiResult>> Post([FromQuery]string key, [FromQuery]string sources, [FromServices]GlobRequestService globRequests)
    {
        var sourceKeys = (sources ?? string.Empty).Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var request = new PushGlobRequest(sourceKeys);

        var requestContext = await globRequests.AddGlobRequest(request);

        ApiResult result = ApiResult.Success(requestContext);
        return TypedResults.Json(result, statusCode: 201);

       
    }
}
