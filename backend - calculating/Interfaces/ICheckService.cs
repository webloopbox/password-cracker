using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend___calculating.Interfaces {
    public interface ICheckService {
        public IActionResult HandleCheckDictionaryHashRequest(HttpContext httpContext);
    }
}