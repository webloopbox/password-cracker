namespace backend___calculating.Services
{
    public interface IDictionaryService
    {
        Task<IResult> SynchronizeDictionaryResult(HttpContext httpContext);
    }
}