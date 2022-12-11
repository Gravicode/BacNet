using System;
using Bacnet.Models;

namespace Bacnet.Web
{
    public class DevicePerformanceService : IDevicePerformanceService
    {
        private readonly DevicePerformanceDbContext _ctx;
        public DevicePerformanceService(DevicePerformanceDbContext ctx)
        {
            _ctx = ctx;
        }
        public async Task<DevicePerformance> CreateDevicePerformance(DevicePerformance devicePerformance)
        {
            DevicePerformance entity = new DevicePerformance()
            {
                CpuTemperature = devicePerformance.CpuTemperature,
                CpuUsage = devicePerformance.CpuUsage,
                MemoryUsage = devicePerformance.MemoryUsage,
                TimeStamp = devicePerformance.TimeStamp
            };
            _ctx.DevicePerformances.Add(entity);
            await _ctx.SaveChangesAsync();
            return entity;
        }
        public async Task<List<DevicePerformance>> GetAllData()
        {
            try
            {
                var datas = _ctx.DevicePerformances.ToList();
                return datas;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return default;
            }
        }
    }

}

