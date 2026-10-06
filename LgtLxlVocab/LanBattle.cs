using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LgtLxlVocab;

/// <summary>
/// 两台手机之间的直连对战（TCP）。
/// 走 Wi-Fi / 手机热点局域网，不需要流量、不需要服务器，所以没有网络也能对战。
/// </summary>
public class LanBattle : IDisposable
{
    public const int Port = 47615;

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

    /// <summary>本机在局域网里的 IPv4，用来显示给另一台手机。</summary>
    public static string LocalIp()
    {
        string? fallback = null;
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;
                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    var ip = addr.Address.ToString();
                    if (ip.StartsWith("169.254", StringComparison.Ordinal))
                        continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                        return ip;
                    fallback ??= ip;
                }
            }
        }
        catch
        {
        }
        return fallback ?? "0.0.0.0";
    }

    /// <summary>房主：监听端口并等待对方接入，返回对方发来的 hello 消息。</summary>
    public async Task<NetMessage> HostAsync(CancellationToken ct)
    {
        _listener = new TcpListener(IPAddress.Any, Port);
        _listener.Start();
        try
        {
            _client = await _listener.AcceptTcpClientAsync(ct);
        }
        finally
        {
            _listener.Stop();
            _listener = null;
        }
        Setup();
        var hello = await ReceiveAsync(ct);
        if (hello?.Name is { Length: > 0 } n)
            PeerName = n;
        return hello ?? new NetMessage();
    }

    /// <summary>加入方：连接房主。</summary>
    public async Task ConnectAsync(string ip, CancellationToken ct)
    {
        if (!IPAddress.TryParse(ip.Trim(), out var address))
            throw new InvalidOperationException("IP 地址格式不对，请检查房主手机上显示的地址。");
        _client = new TcpClient();
        await _client.ConnectAsync(address, Port, ct);
        Setup();
    }

    void Setup()
    {
        var stream = _client!.GetStream();
        _reader = new StreamReader(stream, new UTF8Encoding(false));
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
    }

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
