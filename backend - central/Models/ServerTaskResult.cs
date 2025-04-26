using System.Collections.Generic;

namespace backend___central {
    public class ServerTaskResult {
        public int TaskSetupTime {get; set;}
        public int ProcessingTime {get; set;}
        public IEnumerable<CrackingResult> Results {get; set;}
        
        public ServerTaskResult() {
            TaskSetupTime = 0;
            ProcessingTime = 0;
            Results = new List<CrackingResult>();
        }

        public ServerTaskResult(int taskSetupTime, int processingTime, IEnumerable<CrackingResult> results) {
            TaskSetupTime = taskSetupTime;
            ProcessingTime = processingTime;
            Results = results;
        }
    }
}