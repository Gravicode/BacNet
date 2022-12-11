using Bacnet.Models;
using Bacnet.Slave;
using Bacnet.Models;
using System.Net;
using System.IO.BACnet;
using System.IO.BACnet.Storage;

namespace Bacnet.Slave;

public class Worker : BackgroundService
{

    BacnetClient bacnet_client;
    DeviceStorage m_storage;
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

  

    private void WriteData(DevicePerformance performance)
    {
        var props = typeof(DevicePerformance).GetProperties();
        ushort address = 12345;
        uint instance = 0;
        foreach (var prop in props)
        {
            Console.WriteLine(prop.PropertyType.ToString());
            if (!prop.PropertyType.ToString().Contains("DateTime"))
            {
                var value = Convert.ToSingle(prop.GetValue(performance));
                BacnetObjectId OBJECT_ANALOG_INPUT = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, instance);
                lock (m_storage)         // read and write callback are fired in a separated thread, so multiple access needs protection
                {
                    // Write the Present Value
                    IList<BacnetValue> valtowrite = new BacnetValue[1] { new BacnetValue(value) };
                    var write = m_storage.WriteProperty(OBJECT_ANALOG_INPUT, BacnetPropertyIds.PROP_PRESENT_VALUE, 1, valtowrite, true);
                    Console.WriteLine($"write {prop.Name}, result:{write}");
                }
                instance += 1;
            }
        }
    }

    void StartActivity()
    {
        // Load the device descriptor from the embedded resource file
        // Get myId as own device id
        m_storage = DeviceStorage.Load("DeviceDescriptor.xml");

        // Bacnet on UDP/IP/Ethernet
        bacnet_client = new BacnetClient(new BacnetIpUdpProtocolTransport(0xBAC0, false));
        // or Bacnet Mstp on COM4 à 38400 bps, own master id 8
        // m_bacnet_client = new BacnetClient(new BacnetMstpProtocolTransport("COM4", 38400, 8);
        // Or Bacnet Ethernet
        // bacnet_client = new BacnetClient(new BacnetEthernetProtocolTransport("Connexion au réseau local"));    
        // Or Bacnet on IPV6
        // bacnet_client = new BacnetClient(new BacnetIpV6UdpProtocolTransport(0xBAC0));

        bacnet_client.OnWhoIs += new BacnetClient.WhoIsHandler(handler_OnWhoIs);
        bacnet_client.OnIam += new BacnetClient.IamHandler(bacnet_client_OnIam);
        bacnet_client.OnReadPropertyRequest += new BacnetClient.ReadPropertyRequestHandler(handler_OnReadPropertyRequest);
        bacnet_client.OnReadPropertyMultipleRequest += new BacnetClient.ReadPropertyMultipleRequestHandler(handler_OnReadPropertyMultipleRequest);
        bacnet_client.OnWritePropertyRequest += new BacnetClient.WritePropertyRequestHandler(handler_OnWritePropertyRequest);

        bacnet_client.Start();    // go
                                  // Send Iam
        bacnet_client.Iam(m_storage.DeviceId, new BacnetSegmentations());

    }

    void bacnet_client_OnIam(BacnetClient sender, BacnetAddress adr, uint device_id, uint max_apdu, BacnetSegmentations segmentation, ushort vendor_id)
    {
        //ignore Iams from other devices. (Also loopbacks)
    }

    /*****************************************************************************************************/
    void handler_OnWritePropertyRequest(BacnetClient sender, BacnetAddress adr, byte invoke_id, BacnetObjectId object_id, BacnetPropertyValue value, BacnetMaxSegments max_segments)
    {
        // only OBJECT_ANALOG_VALUE:0.PROP_PRESENT_VALUE could be write in this sample code
        if ((object_id.type != BacnetObjectTypes.OBJECT_ANALOG_VALUE) || (object_id.instance != 0) || ((BacnetPropertyIds)value.property.propertyIdentifier != BacnetPropertyIds.PROP_PRESENT_VALUE))
        {
            sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_WRITE_PROPERTY, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_WRITE_ACCESS_DENIED);
            return;
        }

        lock (m_storage)
        {
            try
            {
                DeviceStorage.ErrorCodes code = m_storage.WriteCommandableProperty(object_id, (BacnetPropertyIds)value.property.propertyIdentifier, value.value[0], value.priority);
                if (code == DeviceStorage.ErrorCodes.NotForMe)
                    code = m_storage.WriteProperty(object_id, (BacnetPropertyIds)value.property.propertyIdentifier, value.property.propertyArrayIndex, value.value);

                if (code == DeviceStorage.ErrorCodes.Good)
                    sender.SimpleAckResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_WRITE_PROPERTY, invoke_id);
                else
                    sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_WRITE_PROPERTY, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_OTHER);
            }
            catch (Exception)
            {
                sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_WRITE_PROPERTY, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_OTHER);
            }
        }
    }
    /*****************************************************************************************************/
    void handler_OnWhoIs(BacnetClient sender, BacnetAddress adr, int low_limit, int high_limit)
    {
        if (low_limit != -1 && m_storage.DeviceId < low_limit) return;
        else if (high_limit != -1 && m_storage.DeviceId > high_limit) return;
        sender.Iam(m_storage.DeviceId, new BacnetSegmentations());
    }

    /*****************************************************************************************************/
    void handler_OnReadPropertyRequest(BacnetClient sender, BacnetAddress adr, byte invoke_id, BacnetObjectId object_id, BacnetPropertyReference property, BacnetMaxSegments max_segments)
    {
        lock (m_storage)
        {
            try
            {
                IList<BacnetValue> value;
                DeviceStorage.ErrorCodes code = m_storage.ReadProperty(object_id, (BacnetPropertyIds)property.propertyIdentifier, property.propertyArrayIndex, out value);
                if (code == DeviceStorage.ErrorCodes.Good)
                    sender.ReadPropertyResponse(adr, invoke_id, sender.GetSegmentBuffer(max_segments), object_id, property, value);
                else
                    sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_OTHER);
            }
            catch (Exception)
            {
                sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_OTHER);
            }
        }
    }

    /*****************************************************************************************************/
    void handler_OnReadPropertyMultipleRequest(BacnetClient sender, BacnetAddress adr, byte invoke_id, IList<BacnetReadAccessSpecification> properties, BacnetMaxSegments max_segments)
    {
        lock (m_storage)
        {
            try
            {
                IList<BacnetPropertyValue> value;
                List<BacnetReadAccessResult> values = new List<BacnetReadAccessResult>();
                foreach (BacnetReadAccessSpecification p in properties)
                {
                    if (p.propertyReferences.Count == 1 && p.propertyReferences[0].propertyIdentifier == (uint)BacnetPropertyIds.PROP_ALL)
                    {
                        if (!m_storage.ReadPropertyAll(p.objectIdentifier, out value))
                        {
                            sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROP_MULTIPLE, invoke_id, BacnetErrorClasses.ERROR_CLASS_OBJECT, BacnetErrorCodes.ERROR_CODE_UNKNOWN_OBJECT);
                            return;
                        }
                    }
                    else
                        m_storage.ReadPropertyMultiple(p.objectIdentifier, p.propertyReferences, out value);
                    values.Add(new BacnetReadAccessResult(p.objectIdentifier, value));
                }

                sender.ReadPropertyMultipleResponse(adr, invoke_id, sender.GetSegmentBuffer(max_segments), values);

            }
            catch (Exception)
            {
                sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROP_MULTIPLE, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_OTHER);
            }
        }
    }

}

