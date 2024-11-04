namespace Globot.Web.App.Routes
{
    public static class _Definition
    {
        public static IEndpointRouteBuilder RegisterRoutes(this IEndpointRouteBuilder routes)
        {
            routes.MapGet("/Requests", Routes.Requests.Get);
            routes.MapGet("/Requests/Submit", Routes.Requests.Post);

            return routes;
        }
    }
}
