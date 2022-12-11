namespace Bacnet.Models;
public class DevicePerformance
{
    public float CpuUsage { get; set; }
    public float MemoryUsage { get; set; }
    public float CpuTemperature { get; set; }
    public float CpuHeat { get; set; }
    public int Id { get; set; }
    public DateTime TimeStamp { get; set; }

}
