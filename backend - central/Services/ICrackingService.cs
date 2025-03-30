namespace backend___central.Services
{
    public interface ICrackingService
    {
        IResult HandleBruteForceCracking(HttpContext httpContext);
        IResult HandleDictionaryCracking(HttpContext httpContext);
    }
}