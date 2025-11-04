using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TrayApp
{
    public class ChatServer
    {
        public delegate void MessageReceivedHandler(string message);
        public event MessageReceivedHandler MessageReceived;

        private TcpListener listener;
        private bool isRunning = false;

        public ChatServer(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);
        }

        public void Start()
        {
            listener.Start();
            isRunning = true;
            ListenForClients();
        }

        private async void ListenForClients()
        {
            while (isRunning)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClient(client));
            }
        }

        private async Task HandleClient(TcpClient client)
        {
            var stream = client.GetStream();
            byte[] buffer = new byte[4096];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead > 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                MessageReceived?.Invoke(message);
            }
            client.Close();
        }

        public void Stop()
        {
            isRunning = false;
            listener.Stop();
        }
    }
}
