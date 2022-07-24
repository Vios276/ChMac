using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ChMac
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var targetInterface = GetCurrentOnlineNetworkInterface();

            Console.WriteLine("Previous MAC: " + targetInterface.GetPhysicalAddress());

            var guid = targetInterface.Id;

            using (var reg = Registry.LocalMachine.OpenSubKey("SYSTEM").OpenSubKey("CurrentControlSet").OpenSubKey("Control").OpenSubKey("Class").OpenSubKey("{4d36e972-e325-11ce-bfc1-08002be10318}"))
            {
                var subKeyNames = reg.GetSubKeyNames();
                foreach (var subKeyName in subKeyNames)
                {
                    if (!Regex.IsMatch(subKeyName, @"\d{4}"))
                        continue;

                    using (var subKey = reg.OpenSubKey(subKeyName, true))
                    {
                        var instanceId = subKey.GetValue("NetCfgInstanceId");

                        if (instanceId?.Equals(guid) == true)
                        {
                            subKey.SetValue("NetworkAddress", "FCAA1472FBD2"); //FC-AA-14-72-FB-D2

                            using (var networkAddressKey = subKey.OpenSubKey("Ndi", true).OpenSubKey("Params", true).OpenSubKey("NetworkAddress", true))
                            {
                                networkAddressKey.SetValue(string.Empty, "FCAA1472FBD2"); //FC-AA-14-72-FB-D2
                                networkAddressKey.SetValue("Param Desc", "Network Address");
                            }
                        }
                    }
                }
            }

            var wmiQuery = new SelectQuery($"SELECT * FROM Win32_NetworkAdapter WHERE GUID='{guid}'");

            var searchProcedure = new ManagementObjectSearcher(wmiQuery);

            foreach (var item in searchProcedure.Get())
            {
                var obj = (ManagementObject)item;

                obj.InvokeMethod("Disable", null);

                while (GetCurrentOnlineNetworkInterface() != null)
                    Thread.Sleep(1000);

                obj.InvokeMethod("Enable", null);

                while (GetCurrentOnlineNetworkInterface() == null)
                    Thread.Sleep(1000);
            }

            Console.WriteLine("Current MAC: " + GetCurrentOnlineNetworkInterface().GetPhysicalAddress());

            Console.ReadLine();
        }

        private static NetworkInterface GetCurrentOnlineNetworkInterface()
        {
            for (var i = 0; i < NetworkInterface.GetAllNetworkInterfaces().Length; i++)
            {
                var ni = NetworkInterface.GetAllNetworkInterfaces()[i];

                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
                    !ni.Name.ToLower().Contains("loopback") &&
                    !ni.Name.Contains("SAMSUNG"))
                    return ni;
            }

            return null;
        }
    }
}
