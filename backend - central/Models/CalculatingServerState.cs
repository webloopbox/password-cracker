using System.Net;

namespace backend___central.Models
{
    public class CalculatingServerState
    {
        public bool IsBusy { get; set; }

        public IPAddress IpAddress { get; set; }

        public CalculatingServerState(IPAddress IpAddress)
        {
            IsBusy = false;
            this.IpAddress = IpAddress;
        }
    }
}