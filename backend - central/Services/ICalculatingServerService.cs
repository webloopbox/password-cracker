namespace backend___central.Services
{
    public interface ICalculatingServerService
    {
        Task<IResult> HandleConnectToCentralServerRequest(HttpContext httpContext);
    }
}