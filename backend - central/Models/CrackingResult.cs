namespace backend___central {
    public class CrackingResult {
        public int Time {get; set;} 
        public bool Success {get; set;} 
        public string ServerIp {get; set;}  
        public string? Password {get; set;}  

        public CrackingResult() {
            Time = 0;
            Success = false;
            ServerIp = "";
        }

        public CrackingResult(int time, bool success, string serverIp, string password) {
            Time = time;
            Success = success;
            ServerIp = serverIp;
            Password = password;
        }
    }
}