export interface ServerTime {
  server: string;
  time: number;
}

export interface Timing {
  parseTime: number;
  distributionTime: number;
  taskSetupTime: number;
  processingTime: number;
  totalTime: number;
}

export interface CrackingResponse {
  message: string;
  totalExecutionTime: number;
  averageServerTime: number;
  communicationTime: number;
  serversTimes: ServerTime[];
  timing: Timing;
}
