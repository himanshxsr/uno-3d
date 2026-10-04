#nullable enable

using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Uno.Infrastructure.Networking
{
    public static class RoomCodeUtil
    {
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static string GenerateRoomCode(int length = 6)
        {
            var rng = new Random(Environment.TickCount ^ Guid.NewGuid().GetHashCode());
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                sb.Append(Alphabet[rng.Next(Alphabet.Length)]);
            }

            return sb.ToString();
        }

        public static string GetLocalIPv4()
        {
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up)
                    {
                        continue;
                    }

                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    IPInterfaceProperties props = nic.GetIPProperties();
                    foreach (UnicastIPAddressInformation addr in props.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string ip = addr.Address.ToString();
                            if (!ip.StartsWith("169.254.", StringComparison.Ordinal))
                            {
                                return ip;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fall through.
            }

            return "127.0.0.1";
        }

        public static string BuildShareString(string address, int port, string roomCode) =>
            $"{address}:{port}:{roomCode}";

        public static bool TryParseShareString(string input, out string address, out int port, out string roomCode)
        {
            address = "127.0.0.1";
            port = GameSessionPorts.DefaultTcpPort;
            roomCode = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string trimmed = input.Trim();
            string[] parts = trimmed.Split(':');
            if (parts.Length >= 3 && int.TryParse(parts[1], out int parsedPort))
            {
                address = parts[0];
                port = parsedPort;
                roomCode = parts[2].ToUpperInvariant();
                return true;
            }

            if (parts.Length == 1)
            {
                roomCode = parts[0].ToUpperInvariant();
                return true;
            }

            if (parts.Length == 2 && int.TryParse(parts[1], out parsedPort))
            {
                address = parts[0];
                port = parsedPort;
                return true;
            }

            return false;
        }
    }

    public static class GameSessionPorts
    {
        public const int DefaultTcpPort = 17777;
        public const int DefaultUdpAdvertisePort = 17778;
    }
}
