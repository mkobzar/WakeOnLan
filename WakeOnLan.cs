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
            const string usage = "WakeOnLan allows a computer to be turned on or awakened" +
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
                        remotePort = remotePort == 0 ? (ushort) 7 : remotePort;
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
        /// This method used and suggested everywhere, but for some reason it never work for me. Don't use it!
        /// </summary>
        /// <param name="macAddress">MAC Address</param>
        /// <param name="port">Port</param>
        [Obsolete]
        public static void WakeUp(string macAddress, int port)
        {
            var client = new UdpClient();
            client.Connect(new IPAddress(0xffffffff), port);

            var datagram = new byte[102];
            for (var i = 0; i <= 5; i++)
            {
                datagram[i] = 0xff;
            }

            var macAddressStripped = Regex.Replace(macAddress, @"[^0-9A-Fa-f]", "");
            if (macAddressStripped.Length != 12)
                throw new ArgumentException($"{macAddress} is incorrect MAC address");

            const int start = 6;
            for (var i = 0; i < 16; i++)
            for (var x = 0; x < 6; x++)
                datagram[start + i * 6 + x] = (byte) Convert.ToInt32(macAddressStripped.Substring(x * 2, 2), 16);

            client.Send(datagram, datagram.Length);
        }

        /// <summary>
        /// awake by mac address
        /// </summary>
        /// <param name="macAddress">MAC Address</param>
        public static void WakeUp(string macAddress)
        {
            NetworkInterface.GetAllNetworkInterfaces()
                .Where(x => x.OperationalStatus == OperationalStatus.Up &&
                            x.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(x => x.GetIPProperties().UnicastAddresses)
                .Where(x => x.IsDnsEligible)
                .ToList()
                .ForEach(x => WakeUp(macAddress, x.Address.ToString(), x.IPv4Mask.ToString(), 7));
        }

        /// <summary>
        /// WakeUp by mac address, specifying IP, subnet mask and port number
        /// </summary>
        /// <param name="macAddress">MAC Address</param>
        /// <param name="ipAddress">IPv4 Address</param>
        /// <param name="subnetMask">Subnet Mask</param>
        /// <param name="port">Port Number</param>
        public static void WakeUp(string macAddress, string ipAddress, string subnetMask, int port)
        {
            if (port < ushort.MinValue || port > ushort.MaxValue)
                throw new ArgumentException($"{port} is incorrect Port number");

            IPAddress address, mask;
            if (!IPAddress.TryParse(ipAddress, out address))
                throw new ArgumentException($"{ipAddress} is invalid IPv4 Address");

            if (!IPAddress.TryParse(subnetMask, out mask))
                throw new ArgumentException($"{subnetMask} is invalid Subnet Mask value");

            var macAddressStripped = Regex.Replace(macAddress, @"[^0-9A-Fa-f]", "");
            if (macAddressStripped.Length != 12)
                throw new ArgumentException($"{macAddress} is incorrect MAC address");


            var client = new UdpClient();
            var datagram = new byte[102];
            for (var i = 0; i <= 5; i++)
            {
                datagram[i] = 0xff;
            }

            for (var i = 0; i < 16; i++)
            for (var x = 0; x < 6; x++)
                datagram[6 + i * 6 + x] = (byte) Convert.ToInt32(macAddressStripped.Substring(x * 2, 2), 16);

            var broadcastAddress = address.GetBroadcastAddress(mask);
            var r = client.Send(datagram, datagram.Length, broadcastAddress.ToString(), port);
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
                broadcastAddress[i] = (byte) (ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
            }
            return new IPAddress(broadcastAddress);
        }
    }
}