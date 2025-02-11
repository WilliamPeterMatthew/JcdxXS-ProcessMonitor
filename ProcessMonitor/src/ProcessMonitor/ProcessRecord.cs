namespace ProcessMonitor
{
    public class ProcessRecord
    {
        public int Pid { get; set; }
        public required string ProcessName { get; set; }
        public required string StartTime { get; set; }
        public required string EndTime { get; set; }
        
        public ProcessRecord()
        {
            ProcessName = string.Empty;
            StartTime = string.Empty;
            EndTime = string.Empty;
        }
    }
}
