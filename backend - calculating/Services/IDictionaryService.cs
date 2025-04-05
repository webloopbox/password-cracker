using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend___calculating.Services
{
    public interface IDictionaryService
    {
        Task<ActionResult> SynchronizeDictionaryResult(HttpContext httpContext);
    }
}