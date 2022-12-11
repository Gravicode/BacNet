using Bacnet.Models;
using Bacnet.Slave;
using Bacnet.Models;
using System.Net;
using System.IO.BACnet;

namespace Bacnet.Slave;

public class Worker : BackgroundService
{
    
    BacnetClient bacnet_client;

    // All the present Bacnet Device List
    List<BacNode> DevicesList = new List<BacNode>();
    private readonly ILogger<Worker> _logger;
   
    private readonly IPiDevicePerformanceInfo _devicePerformance;
    public Worker(ILogger<Worker> logger,        
        IPiDevicePerformanceInfo piDevicePerformance)
    {
        _devicePerformance = piDevicePerformance;
        _logger = logger;

    }
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        var ipAddress = host.AddressList.Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).FirstOrDefault() ?? IPAddress.Parse("127.0.0.1");

        //var ipAddress = IPAddress.Parse("192.168.18.73");
        try
        {
            StartActivity();

            Thread.Sleep(1000); // Wait a fiew time for WhoIs responses (managed in handler_OnIam)
        }
        catch(Exception ex) {
            Console.WriteLine(ex);
        }

        Console.WriteLine($"Bacnet client on IP:{ipAddress}");
        return base.StartAsync(cancellationToken);
    }
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        
        bacnet_client.Dispose();
        return base.StopAsync(cancellationToken);
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var performanceData = _devicePerformance.GetPerformanceInfo();
            WriteData(performanceData);
            _logger.LogInformation($"Cpu Usage:{performanceData.CpuUsage} -- Cpu Temperature:{performanceData.CpuTemperature} -- Ram Usage:{performanceData.MemoryUsage} -- TimeStamp:{DateTime.Now}");
            await Task.Delay(3000, stoppingToken);
        }
    }

    void WriteBacnet(int DeviceId, float NewVal)
    {
        BacnetValue newValue = new BacnetValue(Convert.ToSingle(NewVal));   // expect it's a float
        var ret = WriteScalarValue(DeviceId, new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, 0), BacnetPropertyIds.PROP_PRESENT_VALUE, newValue);
        Console.WriteLine("Write feedback : " + ret.ToString());
    }

    float ReadBacnet(int DeviceId)
    {
        BacnetValue Value;
        bool ret;
        // Read Present_Value property on the object ANALOG_INPUT:0 provided by the device 12345
        // Scalar value only
        ret = ReadScalarValue(DeviceId, new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, 0), BacnetPropertyIds.PROP_PRESENT_VALUE, out Value);

        if (ret == true)
        {
            return Convert.ToSingle(Value.Value);
        }
        return 0f;
    }

    private void WriteData(DevicePerformance performance)
    {
        var props = typeof(DevicePerformance).GetProperties();
        ushort address = 12345;
        foreach (var prop in props)
        {
            Console.WriteLine(prop.PropertyType.ToString());
            if (!prop.PropertyType.ToString().Contains("DateTime"))
            {
                var value = Convert.ToSingle(prop.GetValue(performance));
                //var convertedValue = value.ToUnsignedShortArray();
                WriteBacnet(address, value);
                address += 1;
            }
        }
    }
    void StartActivity()
    {
        // Bacnet on UDP/IP/Ethernet
        bacnet_client = new BacnetClient(new BacnetIpUdpProtocolTransport(0xBAC0, false));
        // or Bacnet Mstp on COM4 à 38400 bps, own master id 8
        // m_bacnet_client = new BacnetClient(new BacnetMstpProtocolTransport("COM4", 38400, 8);
        // Or Bacnet Ethernet
        // bacnet_client = new BacnetClient(new BacnetEthernetProtocolTransport("Connexion au réseau local"));          
        // Or Bacnet on IPV6
        // bacnet_client = new BacnetClient(new BacnetIpV6UdpProtocolTransport(0xBAC0));

        bacnet_client.Start();    // go

        // Send WhoIs in order to get back all the Iam responses :  
        bacnet_client.OnIam += new BacnetClient.IamHandler(handler_OnIam);

        bacnet_client.WhoIs();

        /* Optional Remote Registration as A Foreign Device on a BBMD at @192.168.1.1 on the default 0xBAC0 port

        bacnet_client.RegisterAsForeignDevice("192.168.1.1", 60);
        Thread.Sleep(20);
        bacnet_client.RemoteWhoIs("192.168.1.1");
        */
    }

    void handler_OnIam(BacnetClient sender, BacnetAddress adr, uint device_id, uint max_apdu, BacnetSegmentations segmentation, ushort vendor_id)
    {
        lock (DevicesList)
        {
            // Device already registred ?
            foreach (BacNode bn in DevicesList)
                if (bn.getAdd(device_id) != null) return;   // Yes

            // Not already in the list
            DevicesList.Add(new BacNode(adr, device_id));   // add it
        }
    }

    /*****************************************************************************************************/
    bool ReadScalarValue(int device_id, BacnetObjectId BacnetObjet, BacnetPropertyIds Propriete, out BacnetValue Value)
    {
        BacnetAddress adr;
        IList<BacnetValue> NoScalarValue;

        Value = new BacnetValue(null);

        // Looking for the device
        adr = DeviceAddr((uint)device_id);
        if (adr == null) return false;  // not found

        // Property Read
        if (bacnet_client.ReadPropertyRequest(adr, BacnetObjet, Propriete, out NoScalarValue) == false)
            return false;

        Value = NoScalarValue[0];
        return true;
    }

    /*****************************************************************************************************/
    bool WriteScalarValue(int device_id, BacnetObjectId BacnetObjet, BacnetPropertyIds Propriete, BacnetValue Value)
    {
        BacnetAddress adr;

        // Looking for the device
        adr = DeviceAddr((uint)device_id);
        if (adr == null) return false;  // not found

        // Property Write
        BacnetValue[] NoScalarValue = { Value };
        if (bacnet_client.WritePropertyRequest(adr, BacnetObjet, Propriete, NoScalarValue) == false)
            return false;

        return true;
    }

    /*****************************************************************************************************/
    BacnetAddress DeviceAddr(uint device_id)
    {
        BacnetAddress ret;

        lock (DevicesList)
        {
            foreach (BacNode bn in DevicesList)
            {
                ret = bn.getAdd(device_id);
                if (ret != null) return ret;
            }
            // not in the list
            return null;
        }
    }
}

