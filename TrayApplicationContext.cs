using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TrayApp
{
    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private PeerDiscovery discovery;
        private ChatServer server;
        private ChatClient client;

        private string softwareId = "";
        private string pinHash = "";
        private string userName = "";

        private Dictionary<string, PeerInfo> peers = new();

        private readonly SoundPlayer notifyPlayer = new SoundPlayer("notify.wav");

        private System.Windows.Forms.Timer discoveryTimer;

        private ToolStripMenuItem startupMenuItem;

        public TrayApplicationContext()
        {
            LoadOrCreateConfig();  // Először betöltjük az ID-t és usernevet

            trayIcon = new NotifyIcon()
            {
                Icon = new System.Drawing.Icon("app.ico"),
                Visible = true,
                Text = "PNChat (tray)"
            };

            trayIcon.ContextMenuStrip = new ContextMenuStrip();

            // Menü fejléc létrehozása a szoftver ID-val
            var headerItem = new ToolStripMenuItem($"PNTv0.1 | zdnsk - {softwareId}");
            headerItem.Enabled = false;  // Nem kattintható
            trayIcon.ContextMenuStrip.Items.Add(headerItem);

            startupMenuItem = new ToolStripMenuItem("Start with Windows");
            startupMenuItem.Checked = IsStartupEnabled();
            startupMenuItem.CheckOnClick = true;
            startupMenuItem.CheckedChanged += StartupMenuItem_CheckedChanged;
            trayIcon.ContextMenuStrip.Items.Add(startupMenuItem);

            trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

            trayIcon.ContextMenuStrip.Items.Add("Exit", null, OnExit);

            discovery = new PeerDiscovery();
            server = new ChatServer(12456);
            client = new ChatClient(12456);

            discovery.PeerFound += async (msg, ip) => await OnPeerFound(msg, ip);
            server.MessageReceived += OnMessageReceived;

            StartDiscoveryTimer();
            server.Start();
        }

        private void StartupMenuItem_CheckedChanged(object? sender, EventArgs e)
        {
            if (startupMenuItem.Checked)
            {
                EnableStartup();
            }
            else
            {
                DisableStartup();
            }
        }

        private bool IsStartupEnabled()
        {
            using RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
            if (rk == null) return false;
            var value = rk.GetValue("PNChatTrayApp");
            return value != null;
        }

        private void EnableStartup()
        {
            using RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (rk == null) return;
            rk.SetValue("PNChatTrayApp", Application.ExecutablePath);
        }

        private void DisableStartup()
        {
            using RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (rk == null) return;
            rk.DeleteValue("PNChatTrayApp", false);
        }

        private void StartDiscoveryTimer()
        {
            discovery.Start($"{userName}|{softwareId}|{GetActiveWindowTitle()}");

            discoveryTimer = new System.Windows.Forms.Timer { Interval = 10000 };
            discoveryTimer.Tick += (s, e) =>
            {
                discovery.Start($"{userName}|{softwareId}|{GetActiveWindowTitle()}");
            };
            discoveryTimer.Start();
        }

        private async Task OnPeerFound(string rawData, string ip)
        {
            var parts = rawData.Split('|');
            if (parts.Length < 3) return;

            string name = parts[0];
            string id = parts[1];
            string activeWindowTitle = parts[2];

            if (peers.ContainsKey(id))
            {
                var peer = peers[id];
                peer.LastSeen = DateTime.Now;
                peer.Ip = ip;
                peer.Name = name;
                peer.ActiveWindowTitle = activeWindowTitle;
            }
            else
            {
                peers[id] = new PeerInfo(name, id, ip, activeWindowTitle);
            }
            await Task.CompletedTask;
        }

        private void OnMessageReceived(string rawMessage)
        {
            var parts = rawMessage.Split('|', 3);
            if (parts.Length < 3) return;

            string senderName = parts[0];
            string message = parts[2];

            try
            {
                notifyPlayer.Play();
            }
            catch { }

            MessageBox.Show(message, $"Üzenet innen: {senderName}", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnExit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        private void LoadOrCreateConfig()
        {
            const string ConfigFile = "config.dat";
            if (File.Exists(ConfigFile))
            {
                var lines = File.ReadAllLines(ConfigFile);
                if (lines.Length >= 3)
                {
                    softwareId = lines[0];
                    pinHash = lines[1];
                    userName = lines[2];
                    return;
                }
            }
            string ipLastSegment = "000";
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    var props = ni.GetIPProperties();
                    foreach (var addr in props.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork
                            && !IPAddress.IsLoopback(addr.Address))
                        {
                            var segments = addr.Address.ToString().Split('.');
                            ipLastSegment = segments[^1];
                            break;
                        }
                    }
                    if (ipLastSegment != "000")
                        break;
                }
            }
            catch
            {
                ipLastSegment = "000";
            }
            Random rnd = new Random();
            int randomPart = rnd.Next(10, 99);
            softwareId = $"{ipLastSegment}{randomPart}";
            pinHash = Hash("1234");
            userName = Environment.UserName;
            SaveConfig();
        }

        private void SaveConfig()
        {
            const string ConfigFile = "config.dat";
            File.WriteAllLines(ConfigFile, new[] { softwareId, pinHash, userName });
        }

        private string Hash(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(input)));
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
                return Buff.ToString();
            return string.Empty;
        }

        private class PeerInfo
        {
            public string Name { get; set; }
            public string Id { get; set; }
            public string Ip { get; set; }
            public string ActiveWindowTitle { get; set; }
            public DateTime LastSeen { get; set; }

            public PeerInfo(string name, string id, string ip, string activeWindowTitle)
            {
                Name = name;
                Id = id;
                Ip = ip;
                ActiveWindowTitle = activeWindowTitle;
                LastSeen = DateTime.Now;
            }
        }
    }
}
