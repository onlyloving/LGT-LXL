using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LgtLxlVocab;

/// <summary>
/// 两台手机之间的直连通信（TCP）。
/// 走 Wi-Fi / 手机热点局域网，不需要流量、也不需要服务器。
/// </summary>
public class LanBattle : IDisposable
{
    /// <summary>默认端口；如果被占用会自动往后试几个。</summary>
    public const int Port = 47615;

    static readonly int[] CandidatePorts = { 47615, 47617, 47618, 47619, 47620, 47621 };

    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    readonly SemaphoreSlim _writeLock = new(1, 1);
    TcpListener? _listener;
    TcpClient? _client;
    StreamReader? _reader;
    StreamWriter? _writer;

    public string PeerName { get; set; } = "对手";
    public bool Connected => _client is { Connected: true };

    /// <summary>房主实际监听的端口（可能是 47615 之外的）。</summary>
    public int BoundPort { get; private set; } = Port;

    // ---------- 本机地址 ----------

    /// <summary>本机所有可能被对方访问到的地址，标注是 Wi-Fi 还是热点。</summary>
    public static List<(string Ip, string Kind)> LocalIps()
    {
        var list = new List<(string Ip, string Kind)>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var desc = ((ni.Name ?? "") + " " + (ni.Description ?? "")).ToLowerInvariant();
                if (desc.Contains("rmnet") || desc.Contains("ccmni") || desc.Contains("pdp") ||
                    desc.Contains("cell") || desc.Contains("cellular"))
                    continue;   // 蜂窝网卡：对方连不到

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    var ip = addr.Address.ToString();
                    if (!IsPrivateIpv4(ip))
                        continue;
                    var kind = IsHotspotIp(ip) ? "热点" :
                        ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : "其他";
                    if (!list.Any(x => x.Ip == ip))
                        list.Add((ip, kind));
                }
            }
        }
        catch
        {
        }
        // Wi-Fi 地址排前面
        return list.OrderBy(x => x.Kind == "Wi-Fi" ? 0 : x.Kind == "热点" ? 1 : 2).ToList();
    }

    public static string LocalIp()
    {
        var all = LocalIps();
        return all.Count > 0 ? all[0].Ip : "0.0.0.0";
    }

    static bool IsPrivateIpv4(string ip)
    {
        if (ip.StartsWith("169.254", StringComparison.Ordinal))
            return false;
        if (ip.StartsWith("10.", StringComparison.Ordinal) ||
            ip.StartsWith("192.168.", StringComparison.Ordinal))
            return true;
        if (ip.StartsWith("172.", StringComparison.Ordinal))
        {
            var parts = ip.Split('.');
            if (parts.Length > 1 && int.TryParse(parts[1], out var second))
                return second >= 16 && second <= 31;
        }
        return false;
    }

    /// <summary>手机热点自己一般都发 192.168.43.x / 192.168.42.x / 172.20.10.x 这类地址。</summary>
    static bool IsHotspotIp(string ip) =>
        ip.StartsWith("192.168.43.", StringComparison.Ordinal) ||
        ip.StartsWith("192.168.42.", StringComparison.Ordinal) ||
        ip.StartsWith("192.168.44.", StringComparison.Ordinal) ||
        ip.StartsWith("172.20.10.", StringComparison.Ordinal);

    // ---------- 房主 ----------

    /// <summary>
    /// 房主：监听端口并等待对方接入，返回对方发来的 hello 消息。
    /// roomName 不为空时会同时开启局域网自动发现。
    /// </summary>
    public async Task<NetMessage> HostAsync(CancellationToken ct, string? roomName = null)
    {
        Exception? lastError = null;
        foreach (var port in CandidatePorts)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                _listener = listener;
                BoundPort = port;
                lastError = null;
                break;
            }
            catch (Exception ex)
            {
                lastError = ex;
                _listener = null;
            }
        }
        if (_listener == null)
            throw new InvalidOperationException("端口被占用，换个时间再试（" + lastError?.Message + "）");

        if (!string.IsNullOrEmpty(roomName))
            _responder = LanDiscovery.StartResponder(roomName, BoundPort);

        try
        {
            _client = await _listener.AcceptTcpClientAsync(ct);
        }
        finally
        {
            try
            {
                _listener.Stop();
            }
            catch
            {
            }
            _listener = null;
            StopResponder();
        }

        Setup();
        var hello = await ReceiveAsync(ct);
        if (hello?.Name is { Length: > 0 } n)
            PeerName = n;
        return hello ?? new NetMessage();
    }

    CancellationTokenSource? _responder;

    void StopResponder()
    {
        try
        {
            _responder?.Cancel();
            _responder?.Dispose();
        }
        catch
        {
        }
        _responder = null;
    }

    // ---------- 加入方 ----------

    /// <summary>连接房主。hostSpec 支持 "192.168.1.5" 或 "192.168.1.5:47617"。</summary>
    public async Task ConnectAsync(string hostSpec, CancellationToken ct)
    {
        var spec = (hostSpec ?? "").Trim();
        var port = Port;
        var colon = spec.IndexOf(':');
        if (colon > 0)
        {
            var portText = spec.Substring(colon + 1).Trim();
            spec = spec.Substring(0, colon).Trim();
            if (int.TryParse(portText, out var parsed))
                port = parsed;
        }
        if (!IPAddress.TryParse(spec, out var address))
            throw new InvalidOperationException("IP 地址不对，应该像 192.168.1.5 这样（也可以写成 192.168.1.5:" + Port + "）");

        _client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        try
        {
            await _client.ConnectAsync(address, port, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _client.Dispose();
            _client = null;
            throw new InvalidOperationException(
                $"连接 {spec}:{port} 超时。请确认：① 对方已经点了「创建/发起」；② 两台手机在同一个 Wi-Fi 或热点；" +
                "③ 输入的地址是对方屏幕上显示的那个。");
        }
        catch (SocketException ex)
        {
            _client.Dispose();
            _client = null;
            throw new InvalidOperationException($"连接 {spec}:{port} 失败（{ex.SocketErrorCode}）。多半是不同网络或对方没开房间。");
        }
        Setup();
    }

    void Setup()
    {
        var stream = _client!.GetStream();
        _reader = new StreamReader(stream, new UTF8Encoding(false));
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
    }

    // ---------- 收发 ----------

    public async Task SendAsync(NetMessage msg, CancellationToken ct = default)
    {
        if (_writer == null)
            return;
        var text = JsonSerializer.Serialize(msg, Json);
        await _writeLock.WaitAsync(ct);
        try
        {
            await _writer.WriteLineAsync(text.AsMemory(), ct);
            await _writer.FlushAsync(ct);
        }
        catch
        {
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<NetMessage?> ReceiveAsync(CancellationToken ct = default)
    {
        if (_reader == null)
            return null;
        try
        {
            var line = await _reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line))
                return null;
            return JsonSerializer.Deserialize<NetMessage>(line, Json);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        StopResponder();
        try
        {
            _listener?.Stop();
        }
        catch
        {
        }
        try
        {
            _writer?.Dispose();
            _reader?.Dispose();
            _client?.Dispose();
        }
        catch
        {
        }
        _listener = null;
        _client = null;
    }
}
