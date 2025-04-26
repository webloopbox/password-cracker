using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend___calculating.Interfaces
{
    public interface IDictionaryService
    {
        Task<ActionResult> SynchronizeDictionaryResult(HttpContext httpContext);
        Task<ActionResult> StartCrackingResult(HttpContext httpContext);
    }
}