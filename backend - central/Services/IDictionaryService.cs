namespace backend___central.Services
{
    public interface IDictionaryService
    {
        Task<IResult> SynchronizeDictionaryResult(HttpContext httpContext);
        IResult GetCurrentDictionaryHashResult();
        IResult GetCurrentDictionaryPackResult();
    }
}