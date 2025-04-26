using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using backend___central.Interfaces;

namespace backend___central.Controllers
{
    [ApiController]
    [Route("api/synchronizing")]
    public class DictionarySynchronizingController : ControllerBase
    {
        private readonly IDictionarySynchronizingService dictionarySynchronizingService;

        public DictionarySynchronizingController(IDictionarySynchronizingService dictionarySynchronizingService)
        {
            this.dictionarySynchronizingService = dictionarySynchronizingService;
        }

        [HttpPost("dictionary")]
        public async Task<IActionResult> SynchronizeDictionary()
        {
            return await dictionarySynchronizingService.SynchronizeDictionaryResult(HttpContext);
        }

        [HttpGet("dictionary")]
        public IActionResult GetActualDictionaryFile()
        {
            return dictionarySynchronizingService.GetCurrentDictionaryPackResult();
        }
    }
}