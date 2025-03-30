namespace backend___central.Services
{
    public interface ICalculatingServerService
    {
        IResult HandleConnectToCentralServerRequest(HttpContext httpContext);
    }
}