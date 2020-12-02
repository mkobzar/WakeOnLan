using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace WakeOnLan
{
    class Program
    {
        static void Main(string[] args)
        {
            const string usage = "WakeOnLan allows a computer to be turned on or awakened. Usage:" +
                                 "\n\nWakeOnLan [MAC Address]" +
                                 "\nWakeOnLan [MAC Address] [IPv4 Address] [Subnet Mask]" +
                                 "\nWakeOnLan [MAC Address] [IPv4 Address] [Subnet Mask] [Port]";
            try
            {
                switch (args.Length)
                {
                    case 1:
                        if (args[0].Contains("?") || args[0].ToLower().Contains("help"))
                        {
                            Console.WriteLine(usage);
                            return;
                        }
                        WakeUp(args[0]);
                        break;
                    case 3:
                    case 4:
                        ushort remotePort = 7;
                        if (args.Length > 3)
                            ushort.TryParse(args[3], out remotePort);
                        remotePort = remotePort == 0 ? (ushort)7 : remotePort;
                        WakeUp(args[0], args[1], args[2], remotePort);
                        break;
                    default:
                        Console.WriteLine("invalid arguments\n");
                        Console.WriteLine(usage);
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }



        /// <summary>
        /// Convert mac address to magic packet
        /// </summary>
        /// <param name="macAddress"></param>
        /// <returns>Datagram</returns>
        private static byte[] GetDatagram(string macAddress)
        {
            var datagram = new byte[102];
            const byte z = 6;
            for (var i = 0; i < z; i++)
            {
                datagram[i] = 0xff;
            }

            for (var i = 0; i < 16; i++)
                for (var x = 0; x < z; x++)
                {
                    datagram[z + i * z + x] = (byte)Convert.ToInt32(macAddress.Substring(x * 2, 2), 16);
                }

            return datagram;
        }

        /// <summary>
        /// WakeUp by mac address, specifying IP, subnet mask and port number
        /// </summary>
        /// <param name="macAddress">MAC Address</param>
        /// <param name="ipAddress">IPv4 Address</param>
        /// <param name="subnetMask">Subnet Mask</param>
        /// <param name="port">Port Number</param>
        private static void WakeUp(string macAddress, string ipAddress, string subnetMask, ushort port)
        {
            IPAddress address, mask;
            if (!IPAddress.TryParse(ipAddress, out address))
                throw new ArgumentException($"{ipAddress} is invalid IP Address");
           
            if(address.AddressFamily!=AddressFamily.InterNetwork)
                throw new ArgumentException($"{ipAddress} is not IPv4 Address");

            if (!IPAddress.TryParse(subnetMask, out mask))
                throw new ArgumentException($"{subnetMask} is invalid Subnet Mask value");

            if (address.GetAddressBytes().Count() != mask.GetAddressBytes().Count())
            {
                Console.WriteLine($"address[{address}] Bytes Count[{address.GetAddressBytes().Count()}] != mask[{mask}] Bytes Count[{mask.GetAddressBytes().Count()}].");
                Console.WriteLine("This address/mask will be skipped for waking up");
                return;
            }

            var macAddressStripped = Regex.Replace(macAddress, @"[^0-9A-Fa-f]", "");
            if (macAddressStripped.Length != 12)
                throw new ArgumentException($"{macAddress} is incorrect MAC address");

            var client = new UdpClient();
            var datagram = GetDatagram(macAddressStripped);
            var broadcastAddress = address.GetBroadcastAddress(mask);
            client.Send(datagram, datagram.Length, broadcastAddress.ToString(), port);
            Console.WriteLine($"WakeUp signal sent to mac address[{macAddress}], using IP[{ipAddress}, subnet mask[{subnetMask}] and port[{port}]");
        }

        /// <summary>
        /// awake by mac address
        /// </summary>
        /// <param name="macAddress">MAC Address</param>
        private static void WakeUp(string macAddress)
        {
            NetworkInterface.GetAllNetworkInterfaces()
                .Where(x => x.OperationalStatus == OperationalStatus.Up &&
                            x.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(x => x.GetIPProperties().UnicastAddresses)
                //.Where(x => x.IsDnsEligible)
                .Where(x => x.IsDnsEligible && x.Address.AddressFamily == AddressFamily.InterNetwork)
                .ToList()
                .ForEach(x => WakeUp(macAddress, x.Address.ToString(), x.IPv4Mask.ToString(), 7));
        }
    }


    public static class IpAddressExtensions
    {
        /// <summary>
        /// Get Broadcast Address
        /// </summary>
        /// <param name="address"></param>
        /// <param name="subnetMask"></param>
        /// <returns></returns>
        public static IPAddress GetBroadcastAddress(this IPAddress address, IPAddress subnetMask)
        {
            var ipAdressBytes = address.GetAddressBytes();
            var subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException(
                    $" ipAdressBytes.Length[{ipAdressBytes.Length}] != subnetMaskBytes.Length[{subnetMaskBytes.Length}]");

            var broadcastAddress = new byte[ipAdressBytes.Length];
            for (var i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
            }
            return new IPAddress(broadcastAddress);
        }
    }
}