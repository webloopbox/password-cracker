using Microsoft.AspNetCore.Mvc;
using backend___calculating.Services;
using System.Threading.Tasks;

namespace backend___calculating.Controllers
{
    [ApiController]
    [Route("api/synchronizing")]
    public class DictionaryController : ControllerBase
    {
        private readonly IDictionaryService dictionaryService;

        public DictionaryController(IDictionaryService dictionaryService) {
            this.dictionaryService = dictionaryService;
        }

        [HttpPost("dictionary")]
        public async Task<IActionResult> SynchronizeDictionary()
        {
            return await dictionaryService.SynchronizeDictionaryResult(HttpContext);
        }
    }
}