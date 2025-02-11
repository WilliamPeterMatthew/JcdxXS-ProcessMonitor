namespace ProcessMonitor
{
    public class ProcessRecord
    {
        public int Pid { get; set; }
        public string ProcessName { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
    }
}
