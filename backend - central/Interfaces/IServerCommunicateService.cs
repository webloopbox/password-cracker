using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace backend___central.Interfaces
{
    public interface IServerCommunicationService
    {
        Task<HttpResponseMessage> SendRequestToServer(HttpClient httpClient, string serverIpAddress, string payloadJson);
        Task ValidateServerConnection(string serverIpAddress);
        Task<List<Task<CrackingResult>>> CreateTasksForPortions(List<string> charPortions, int passwordLength, string userLogin, List<string> serverIPs);
        Task<CrackingResult> ProcessServerResponse(HttpResponseMessage response, string serverIpAddress, int serverRequestTime);
    }
}