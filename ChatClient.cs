using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TrayApp
{
    public class ChatClient
    {
        private int port;

        public ChatClient(int port)
        {
            this.port = port;
        }

        public async Task<bool> SendMessageAsync(string ip, string message)
        {
            try
            {
                using TcpClient client = new TcpClient();

                var connectTask = client.ConnectAsync(ip, port);
                if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
                {
                    // Timeout
                    return false;
                }

                byte[] data = Encoding.UTF8.GetBytes(message);
                NetworkStream stream = client.GetStream();

                var writeTask = stream.WriteAsync(data, 0, data.Length);
                if (await Task.WhenAny(writeTask, Task.Delay(5000)) != writeTask)
                {
                    // Timeout
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
