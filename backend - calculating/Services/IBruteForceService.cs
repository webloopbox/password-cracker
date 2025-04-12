using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend___calculating.Services
{
    public interface IBruteForceService
    {
        Task<IActionResult> SynchronizeBruteForce(HttpContext httpContext);
    }
}