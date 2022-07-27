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
        RETRY:
            var targetInterface = GetCurrentOnlineNetworkInterface();

            //targetInterface가 다른 네트워크를 잡는 경우가 있기 때문에 target name도 표시
            string temp = "";
            Console.WriteLine("target : " + targetInterface.Name);
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
                            var rndMacAdress = GetRandomMacAddress();
                            temp = rndMacAdress;
                            Console.WriteLine("New MAC Address: " + rndMacAdress);
                            subKey.SetValue("NetworkAddress", rndMacAdress); 

                            //제 pc환경에서는 Ndi 아래 params라는 subkey가 없어서 nullPointerException이 뜸.
                            /*using (var networkAddressKey = subKey.OpenSubKey("Ndi", true).OpenSubKey("Params", true).OpenSubKey("NetworkAddress", true))
                            {
                                networkAddressKey.SetValue(string.Empty, rndMacAdress); 
                                networkAddressKey.SetValue("Param Desc", "Network Address");
                            }*/
                        }
                    }
                }
            }

            var wmiQuery = new SelectQuery($"SELECT * FROM Win32_NetworkAdapter WHERE GUID='{guid}'");

            var searchProcedure = new ManagementObjectSearcher(wmiQuery);

            //다른 네트워크가 존재할 경우(하마치같은 가상 주소 부여하는 프로그램).여기서 지나가질 못함.
            /*foreach (var item in searchProcedure.Get())
            {
                var obj = (ManagementObject)item;

                obj.InvokeMethod("Disable", null);

                //여기서 잡혀서 못지나감
                while (GetCurrentOnlineNetworkInterface() != null)
                    Thread.Sleep(1000);

                obj.InvokeMethod("Enable", null);

                while (GetCurrentOnlineNetworkInterface() == null)
                    Thread.Sleep(1000);
            }*/
            foreach (var item in searchProcedure.Get())
            {
                var obj = (ManagementObject)item;

                obj.InvokeMethod("Disable", null);
                Thread.Sleep(1000);

                obj.InvokeMethod("Enable", null);
                Thread.Sleep(1000);

                while (NetworkInterface.GetIsNetworkAvailable() == false)
                {
                    obj.InvokeMethod("Enable", null);
                    Thread.Sleep(1000);
                }
            }
            Console.WriteLine("인터넷 연결 대기중..");
            while (NetworkInterface.GetIsNetworkAvailable() == false)
            {
                Thread.Sleep(1000);
            }

            int max = 30;//총 30초 대기
            var current = GetCurrentOnlineNetworkInterface();

            Console.WriteLine("변경 확인 중..");
            while (current.GetPhysicalAddress().ToString() != temp)
            {
                current = GetCurrentOnlineNetworkInterface();
                if (current.Name != targetInterface.Name)
                {
                    Console.WriteLine($"타겟이 바뀜 : {current.Name}");
                    break;
                }

                Thread.Sleep(1000);
                if (max == 0)
                {
                    // 이 부분은 커스텀 용도에 따라 커스텀 해야 할 것같음.
                    Console.WriteLine("변경이 확인 되지 않음.(재시도)");
                    goto RETRY;
                }
                max--;
            }

            Console.WriteLine("target : " + current.Name);
            Console.WriteLine("Current MAC: " + GetCurrentOnlineNetworkInterface().GetPhysicalAddress());

            Environment.Exit(0);
        }

        public static string GetRandomMacAddress()
        {
            var random = new Random();
            var buffer = new byte[6];
            random.NextBytes(buffer);
            var result = String.Concat(buffer.Select(x => string.Format("{0}", x.ToString("X2"))).ToArray());
            return result;
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
