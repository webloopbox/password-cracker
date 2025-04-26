using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend___central.Interfaces
{
    public interface IDictionarySynchronizingService
    {
        Task<IActionResult> SynchronizeDictionaryResult(HttpContext httpContext);
        string GetCurrentDictionaryHashResult();
        IActionResult GetCurrentDictionaryPackResult();
    }
}