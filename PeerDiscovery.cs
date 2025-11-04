using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TrayApp
{
    public class PeerDiscovery
    {
        public delegate Task PeerFoundHandler(string message, string ip);
        public event PeerFoundHandler PeerFound;

        private const int Port = 12456;
        private UdpClient udpClient;

        public PeerDiscovery()
        {
            udpClient = new UdpClient(Port);
            StartListening();
        }

        private async void StartListening()
        {
            while (true)
            {
                var result = await udpClient.ReceiveAsync();
                string message = Encoding.UTF8.GetString(result.Buffer);
                string ip = result.RemoteEndPoint.Address.ToString();

                if (PeerFound != null)
                    await PeerFound(message, ip);
            }
        }

        public void Start(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, Port);
            udpClient.Send(bytes, bytes.Length, broadcastEP);
        }
    }
}
