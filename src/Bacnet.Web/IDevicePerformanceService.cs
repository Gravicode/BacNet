using Bacnet.Models;

namespace Bacnet.Web
{
    public interface IDevicePerformanceService
    {
        Task<DevicePerformance> CreateDevicePerformance(DevicePerformance devicePerformance);
        Task<List<DevicePerformance>> GetAllData();
    }
}