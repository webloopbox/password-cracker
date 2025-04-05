using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend___central.Services
{
    public interface IDictionaryService
    {
        Task<IActionResult> SynchronizeDictionaryResult(HttpContext httpContext);
        string GetCurrentDictionaryHashResult();
        IActionResult GetCurrentDictionaryPackResult();
    }
}