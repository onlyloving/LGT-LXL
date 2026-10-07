using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text;

namespace LgtLxlVocab;

/// <summary>
/// 局域网自动发现：房主在 UDP 47616 上应答，加入方广播一句就能找到房主，
/// 这样就不用自己敲 IP 了（手机键盘打小数点很麻烦）。
/// </summary>
public static class LanDiscovery
{
    public const int UdpPort = 47616;
    const string Magic = "LGT-LXL-DISCOVER";

    /// <summary>房主启动应答服务，返回的 CTS 用来停止。</summary>
    public static CancellationTokenSource StartResponder(string roomName, int tcpPort)
    {
        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            UdpClient? udp = null;
            try
            {
                udp = new UdpClient();
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.EnableBroadcast = true;
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, UdpPort));
                while (!cts.IsCancellationRequested)
                {
                    var result = await udp.ReceiveAsync(cts.Token);
                    var text = Encoding.UTF8.GetString(result.Buffer);
                    if (!text.StartsWith(Magic, StringComparison.Ordinal))
                        continue;
                    var reply = Encoding.UTF8.GetBytes(Magic + "|" + roomName + "|" + tcpPort);
                    await udp.SendAsync(reply, reply.Length, result.RemoteEndPoint);
                }
            }
            catch
            {
            }
            finally
            {
                try
                {
                    udp?.Dispose();
                }
                catch
                {
                }
            }
        }, cts.Token);
        return cts;
    }

    /// <summary>搜索附近的房主，返回 (ip, 房主名, 端口)。</summary>
    public static async Task<List<(string Ip, string Name, int Port)>> DiscoverAsync(int waitMs = 3000)
    {
        var found = new Dictionary<string, (string Ip, string Name, int Port)>();
        try
        {
            using var udp = new UdpClient();
            udp.EnableBroadcast = true;
            var probe = Encoding.UTF8.GetBytes(Magic);
            foreach (var target in Targets())
            {
                try
                {
                    await udp.SendAsync(probe, probe.Length, target);
                }
                catch
                {
                }
            }

            var deadline = DateTime.UtcNow.AddMilliseconds(waitMs);
            while (DateTime.UtcNow < deadline)
            {
                var receive = udp.ReceiveAsync();
                var left = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                if (left <= 0)
                    break;
                var done = await Task.WhenAny(receive, Task.Delay(left));
                if (done != receive)
                    break;
                var res = receive.Result;
                var text = Encoding.UTF8.GetString(res.Buffer);
                if (!text.StartsWith(Magic, StringComparison.Ordinal))
                    continue;
                var parts = text.Split('|');
                if (parts.Length < 3 || !int.TryParse(parts[2], out var port))
                    continue;
                var ip = res.RemoteEndPoint.Address.ToString();
                found[ip] = (ip, parts[1], port);
            }
        }
        catch
        {
        }
        return found.Values.ToList();
    }

    /// <summary>受限广播 + 每个网卡的子网广播，尽量让路由器把包转给房主。</summary>
    static IEnumerable<IPEndPoint> Targets()
    {
        yield return new IPEndPoint(IPAddress.Broadcast, UdpPort);
        var nets = new List<IPEndPoint>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;
                foreach (var info in ni.GetIPProperties().UnicastAddresses)
                {
                    if (info.Address.AddressFamily != AddressFamily.InterNetwork || info.IPv4Mask == null)
                        continue;
                    var ip = info.Address.GetAddressBytes();
                    var mask = info.IPv4Mask.GetAddressBytes();
                    if (ip.Length != 4 || mask.Length != 4)
                        continue;
                    var broadcast = new byte[4];
                    for (var i = 0; i < 4; i++)
                        broadcast[i] = (byte)(ip[i] | (byte)~mask[i]);
                    nets.Add(new IPEndPoint(new IPAddress(broadcast), UdpPort));
                }
            }
        }
        catch
        {
        }
        foreach (var ep in nets)
            yield return ep;
    }
}
