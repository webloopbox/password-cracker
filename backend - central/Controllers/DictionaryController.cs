using Microsoft.AspNetCore.Mvc;

namespace backend___central.Controllers
{
    [ApiController]
    [Route("api/synchronizing")]
    public class DictionaryController : ControllerBase
    {
        [HttpPost("dictionary")]
        public IActionResult SynchronizeDictionary()
        {
            return Ok();
        }

        [HttpGet("dictionary")]
        public IActionResult GetActualDictionaryFile()
        {
            return Ok();
        }

        [HttpGet("dictionary-hash")]
        public IActionResult GetActualDictionaryHash()
        {
            return Ok();
        }
    }
}