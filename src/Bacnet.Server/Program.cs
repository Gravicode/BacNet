// See https://aka.ms/new-console-template for more information
using Bacnet.Models;
using System.Configuration;
using System.Net.Sockets;
using System.Net.Http.Json;
using System.IO.BACnet;
using Bacnet.Server;

int DeviceId = 12345;
BacnetClient bacnet_client = null;
List<float> dataList = new();
// All the present Bacnet Device List
List<BacNode> DevicesList = new List<BacNode>();

Console.WriteLine($"{DateTime.Now} - Bacnet Service Running, get data from bacnet client and push to api..");

HttpClient _client = new HttpClient();

var linuxServiceIp = ConfigurationManager.AppSettings["LinuxServiceIp"];
var linuxServicePort = Convert.ToInt32(ConfigurationManager.AppSettings["LinuxServicePort"]);
var dataStoreServiceUrl = ConfigurationManager.AppSettings["DataStoreUrl"];
bool Success = false;
try
{
    StartActivity(linuxServicePort);
    Console.WriteLine("Started");

    Thread.Sleep(1000); // Wait a fiew time for WhoIs responses (managed in handler_OnIam)
    Success = true;

}
catch (Exception ex){
    Console.WriteLine("error => " + ex);
}

BacnetValue Value;
bool ret;

if (Success)
    while (true)
    {
        // Read Present_Value property on the object ANALOG_INPUT:0 provided by the device 12345
        // Scalar value only
        dataList.Clear();
        for (uint i = 0; i < 5; i++)
        {
            ret = ReadScalarValue(DeviceId, new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, i), BacnetPropertyIds.PROP_PRESENT_VALUE, out Value);

            if (ret == true)
            {
                Console.WriteLine("Read value : " + Value.Value.ToString());
                dataList.Add(Convert.ToSingle(Value.Value));
            }
        }
        if (dataList.Count > 0)
        {
            var sysPerformData = new DevicePerformance()
            {
                CpuUsage = (float)Math.Round(dataList[0], 2),
                MemoryUsage = (float)Math.Round(dataList[1], 2),
                CpuTemperature = (float)Math.Round(dataList[2], 2),
                TimeStamp = DateTime.Now
            };

            _client.PostAsJsonAsync(dataStoreServiceUrl + "/deviceperformance", sysPerformData);
            Console.WriteLine($"Cpu Usage:{sysPerformData.CpuUsage} -- Cpu Temperature:{sysPerformData.CpuTemperature} -- Ram Usage:{sysPerformData.MemoryUsage} -- TimeStamp:{sysPerformData.TimeStamp}");
        }
        Thread.Sleep(4000);
    }

void StartActivity(int Port)
{
    // Bacnet on UDP/IP/Ethernet
    bacnet_client = new BacnetClient(new BacnetIpUdpProtocolTransport(Port, false));
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

/*****************************************************************************************************/
void ReadWriteExample()
{

    BacnetValue Value;
    bool ret;
    // Read Present_Value property on the object ANALOG_INPUT:0 provided by the device 12345
    // Scalar value only
    ret = ReadScalarValue(12345, new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, 0), BacnetPropertyIds.PROP_PRESENT_VALUE, out Value);

    if (ret == true)
    {
        Console.WriteLine("Read value : " + Value.Value.ToString());

        // Write Present_Value property on the object ANALOG_OUTPUT:0 provided by the device 4000
        BacnetValue newValue = new BacnetValue(Convert.ToSingle(Value.Value));   // expect it's a float
        ret = WriteScalarValue(4000, new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, 0), BacnetPropertyIds.PROP_PRESENT_VALUE, newValue);

        Console.WriteLine("Write feedback : " + ret.ToString());
    }
    else
        Console.WriteLine("Error somewhere !");
}

/*****************************************************************************************************/
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