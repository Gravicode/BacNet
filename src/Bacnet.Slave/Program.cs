using Bacnet.Slave;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<IPiDevicePerformanceInfo>(new PiDevicePerformanceInfo());
        services.AddHostedService<Worker>();
    })
    .Build();

host.Run();

