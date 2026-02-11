using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Godot;


public delegate void SignalReceivedHandler(string signalName);
public delegate void ConnectionEventHandler();
public delegate void ErrorHandler(string error);
public delegate void LogHandler(string message);

public class Message
{
    [JsonPropertyName("type")]
    public string Type { get; set; }
    
    [JsonPropertyName("timestamp")]
    public double Timestamp { get; set; }
    
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, JsonElement> Data { get; set; }

    public static Message Create(string type, object data = null)
    {
        var message = new Message
        {
            Type = type,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        
        if (data != null)
        {
            // 直接反序列化为 Dictionary<string, JsonElement>
            // 不需要手动解析和克隆
            var json = JsonSerializer.Serialize(data);
            message.Data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        }
        
        return message;
    }
}


public class HeartbeatManager : IDisposable
{
    private TcpClient client;
    private NetworkStream stream;
    private CancellationTokenSource cts;

    private DateTime lastReceived = DateTime.Now;
    private DateTime lastSent = DateTime.Now;
    
    private int heartbeatInterval = 45; // 秒
    private int timeout = 60; // 秒
    private bool isRunning = false;
    
    public event Action OnTimeout;
    public event ErrorHandler OnError;
    
    public HeartbeatManager(TcpClient client)
    {
        this.client = client;
        stream = client.GetStream();
        cts = new CancellationTokenSource();
    }
    
    public void Start()
    {
        if (isRunning) return;
        isRunning = true;
        
        _ = HeartbeatLoop(cts.Token);
        _ = TimeoutCheckLoop(cts.Token);
    }
    
    public void Stop()
    {
        if (!isRunning) return;
        
        isRunning = false;
        cts?.Cancel();
    }
    
    public void UpdateReceived()
    {
        lastReceived = DateTime.Now;
    }
    
    private async Task HeartbeatLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && client?.Connected == true)
        {
            try
            {
                await Task.Delay(heartbeatInterval * 1000, token);
                
                if (!client.Connected || token.IsCancellationRequested)
                    break;
                    
                await SendHeartbeat();
                lastSent = DateTime.Now;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"心跳发送错误: {ex.Message}");
                break;
            }
        }
    }
    
    private async Task TimeoutCheckLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && client?.Connected == true)
        {
            try
            {
                await Task.Delay(1000, token);
                
                if (!client.Connected || token.IsCancellationRequested)
                    break;
                    
                var timeSinceLastReceived = (DateTime.Now - lastReceived).TotalSeconds;
                if (timeSinceLastReceived >= timeout)
                {
                    OnError?.Invoke($"心跳超时: {timeSinceLastReceived:F1}秒未收到消息");
                    OnTimeout?.Invoke();
                    Stop();
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"超时检查错误: {ex.Message}");
                break;
            }
        }
    }
    
    private async Task SendHeartbeat()
    {
        if (client?.Connected != true || cts?.IsCancellationRequested == true)
            return;

        try
        {
            /*var heartbeat = new 
            { 
                type = "heartbeat",
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                data = new { source = "csharp" }
            };*/
            
            var heartbeat = Message.Create(type : "heartbeat", data : new { source = "csharp"} );
            string json = JsonSerializer.Serialize(heartbeat);

            byte[] data = Encoding.UTF8.GetBytes(json + "\n");
            await stream.WriteAsync(data, 0, data.Length, cts.Token);
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"发送心跳失败: {ex.Message}");
            throw;
        }
    }
    
    public async Task SendHeartbeatAck()
    {
        if (client?.Connected != true || cts?.IsCancellationRequested == true)
            return;
        
        try
        {
            GD.Print("发送心跳确认");
            var ack = new 
            { 
                type = "heartbeat_ack",
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                data = new { source = "csharp" }
            };
            
            string json = JsonSerializer.Serialize(ack);
            byte[] data = Encoding.UTF8.GetBytes(json + "\n");
            await stream.WriteAsync(data, 0, data.Length, cts.Token);
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"发送心跳确认失败: {ex.Message}");
        }
    }
    
    public void Dispose()
    {
        Stop();
        cts?.Dispose();
        stream?.Close();
        client?.Close();
    }
}

public class CommunicationManager : IDisposable
{
    private TcpListener _server;
    private TcpClient _client;
    private NetworkStream _stream;
    private HeartbeatManager _heartbeat;
    
    private Task _receiveTask;
    private CancellationTokenSource _receiveCts;
    private bool _isReceiving = false;

    public delegate void ConnectionStateHandler(ConnectionStatusEnum st, string message);
    public event ConnectionStateHandler OnConnectionStateChanged;
    private CancellationTokenSource _acceptCts; // 用于取消接受连接
    
    // 公共事件
    public event SignalReceivedHandler OnSignalReceived;
   
    public event ErrorHandler OnError;
    public event LogHandler OnLog;
    
    public bool IsConnected => _client?.Connected == true;
    

    public async Task<bool> StartServerAsync(int port = 12111, float? timeoutSeconds = null)
    {
        Cleanup();

        try
        {
            //OnConnectionStateChanged?.Invoke(ConnectionStatusEnum.ReadytoConnect, $"正在启动服务器，端口: {port}...");
            
            _server = new TcpListener(System.Net.IPAddress.Any, port);
            _server.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _server.Start();
            
            
            _acceptCts = new CancellationTokenSource();
            var acceptTask = _server.AcceptTcpClientAsync(); // 开始等待连接

            OnLog?.Invoke($"服务器已启动，等待连接...");
            OnConnectionStateChanged?.Invoke(ConnectionStatusEnum.Connecting , "等待客户端连接...");

            // 设置一个延迟取消任务（如果提供了超时）
            Task timeoutTask = Task.Delay(-1); // 默认无限延迟
            if (timeoutSeconds.HasValue)
            {
                timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds.Value), _acceptCts.Token);
            }
            
            // 等待“连接成功”或“超时/取消”这两个事件谁先发生
            var completedTask = await Task.WhenAny(acceptTask, timeoutTask);
            
            // 情况1：连接成功先发生（这是我们期望的）
            if (completedTask == acceptTask && acceptTask.Status == TaskStatus.RanToCompletion)
            {
                // 即使取消令牌被触发，也要优先处理已建立的连接
                _client = acceptTask.Result;
                OnLog?.Invoke($"[成功] 检测到客户端连接，远程端点: {_client.Client.RemoteEndPoint}");
                
                _stream = _client.GetStream();

                // 初始化心跳管理器
                _heartbeat = new HeartbeatManager(_client);
                _heartbeat.OnTimeout += OnHeartbeatTimeout;
                _heartbeat.OnError += OnHeartbeatError;
                _heartbeat.Start();

                OnLog?.Invoke("心跳管理器已启动");
                OnConnectionStateChanged?.Invoke(ConnectionStatusEnum.Connected, "连接已建立");

                StartReceiving();
                return true; // 成功返回
            }
            
            // 情况2：超时或取消先发生
            OnLog?.Invoke("等待连接已超时或被主动取消");
            OnConnectionStateChanged?.Invoke(ConnectionStatusEnum.ConnectionLost, "连接超时或已取消");
            
            // 重要：即使超时，也要检查一下acceptTask是否在最后瞬间完成了
            if (acceptTask.IsCompleted)
            {
                OnLog?.Invoke($"[警告] Accept任务在超时后完成。状态: {acceptTask.Status}");
                // 这里可以选择处理这个迟到的连接，但通常我们丢弃它
                try { acceptTask.Result?.Close(); } catch { }
            }
            
            // 执行清理
            _acceptCts.Cancel(); // 确保取消令牌被触发
            Cleanup(); // 清理资源（会关闭_server）
            return false;
        }
        catch (Exception ex) when (ex is ObjectDisposedException || ex is InvalidOperationException)
        {
            // 这些异常通常在清理时发生，可以忽略
            OnLog?.Invoke($"启动过程中被中断: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"启动服务器发生意外错误: {ex.Message}");
            OnError?.Invoke($"启动服务器失败: {ex.Message}");
            OnConnectionStateChanged?.Invoke(ConnectionStatusEnum.ConnectionLost, $"连接失败: {ex.Message}");
            Cleanup();
            return false;
        }
    }
    
    /// <summary>
    /// 停止等待连接
    /// </summary>
    public void StopWaitingForConnection()
    {
        _acceptCts?.Cancel();
    }
    
    /// <summary>
    /// 异步连接到现有服务器
    /// </summary>
    public async Task<bool> ConnectToServerAsync(string host, int port = 12111, float? timeoutSeconds = null)
    {
        Cleanup();
        
        try
        {
            OnConnectionStateChanged?.Invoke(ConnectionStatusEnum.Connecting, $"正在连接到 {host}:{port}...");
            
            _client = new TcpClient();
            
            CancellationTokenSource cts = new CancellationTokenSource();
            if (timeoutSeconds.HasValue)
            {
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds.Value));
            }
            
            // 异步连接
            Task connectTask = _client.ConnectAsync(host, port);
            await connectTask.WithCancellation(cts.Token);
            
            if (cts.Token.IsCancellationRequested)
            {
                OnConnectionStateChanged?.Invoke(ConnectionStatusEnum.ConnectionLost, "连接超时");
                return false;
            }
            
            _stream = _client.GetStream();
            
            // 初始化心跳管理器
            _heartbeat = new HeartbeatManager(_client);
            _heartbeat.OnTimeout += OnHeartbeatTimeout;
            _heartbeat.OnError += OnHeartbeatError;
            _heartbeat.Start();
            
            OnLog?.Invoke($"已连接到服务器 {host}:{port}");
            OnConnectionStateChanged?.Invoke(ConnectionStatusEnum.Connected, "连接已建立");
            
            // 启动消息接收
            StartReceiving();
            
            return true;
        }
        catch (OperationCanceledException)
        {
            OnConnectionStateChanged?.Invoke(ConnectionStatusEnum.Closed, "连接已取消");
            return false;
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"连接服务器失败: {ex.Message}");
            OnError?.Invoke($"连接服务器失败: {ex.Message}");
            OnConnectionStateChanged?.Invoke(ConnectionStatusEnum.ConnectionLost, $"连接失败: {ex.Message}");
            Cleanup();
            return false;
        }
    }
    
    private void OnHeartbeatTimeout()
    {
        OnLog?.Invoke("心跳超时，连接断开");
        OnConnectionStateChanged?.Invoke(ConnectionStatusEnum.ConnectionLost, "心跳超时，连接断开");
        Cleanup();
    }
    
    private void OnHeartbeatError(string error)
    {
        OnError?.Invoke($"心跳错误: {error}");
    }
    
    private void StartReceiving()
    {
        if (_isReceiving) return;
        
        _isReceiving = true;
        _receiveCts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveMessages(_receiveCts.Token));
    }
    
    private void StopReceiving()
    {
        _isReceiving = false;
        _receiveCts?.Cancel();
        
        try
        {
            _receiveTask?.Wait(1000);
        }
        catch { }
    }
    
    private async Task ReceiveMessages(CancellationToken token)
    {
        byte[] buffer = new byte[4096];
        StringBuilder messageBuilder = new StringBuilder();
        
        while (!token.IsCancellationRequested && IsConnected)
        {
            try
            {
                if (_stream.DataAvailable)
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead == 0)
                    {
                        OnLog?.Invoke("连接已关闭");
                        OnConnectionStateChanged?.Invoke(ConnectionStatusEnum.ConnectionLost, "连接已关闭");
                        break;
                    }
                    
                    string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    messageBuilder.Append(chunk);
                    
                    // 处理完整消息
                    ProcessMessageBuffer(messageBuilder);
                }
                else
                {
                    // 稍微延迟，避免CPU占用过高
                    await Task.Delay(10, token);
                }
            }
            catch (OperationCanceledException)
            {
                // 任务被取消，正常退出
                break;
            }
            catch (Exception ex)
            {
                if (IsConnected) // 只有在连接时报告错误
                {
                    OnError?.Invoke($"接收消息错误: {ex.Message}");
                }
                break;
            }
        }
        
        Cleanup();
    }
    
    private void ProcessMessageBuffer(StringBuilder messageBuilder)
    {
        string buffer = messageBuilder.ToString();
        int newlineIndex;
        
        while ((newlineIndex = buffer.IndexOf('\n')) >= 0)
        {
            string message = buffer.Substring(0, newlineIndex).Trim();
            if (!string.IsNullOrEmpty(message))
            {
                ProcessSingleMessage(message);
            }
            
            buffer = buffer.Substring(newlineIndex + 1);
        }
        
        messageBuilder.Clear();
        messageBuilder.Append(buffer);
    }
    
    private void ProcessSingleMessage(string message)
    {
        try
        {
            OnLog?.Invoke($"收到消息: {message}");
            
            var json = JsonDocument.Parse(message);
            string type = json.RootElement.GetProperty("type").GetString();
            
            _heartbeat?.UpdateReceived();
            
            if (type == "heartbeat")
            {
                _ = _heartbeat?.SendHeartbeatAck(); // 不等待，后台发送
            }
            else
            {
                OnSignalReceived?.Invoke(type);
            }
        }
        catch (JsonException ex)
        {
            OnError?.Invoke($"JSON解析错误: {ex.Message}");
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"处理消息错误: {ex.Message}");
        }
    }
    
    public async Task WaitForSignal(string signalName, float timeoutSeconds = 30)
    {
        if (!IsConnected)
            throw new InvalidOperationException("未连接");
        
        var tcs = new TaskCompletionSource<bool>();
        var timeoutSource = new CancellationTokenSource();
        
        // 设置超时
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        
        // 定义事件处理函数
        void OnSignalReceivedHandler(string receivedSignal)
        {
            if (receivedSignal == signalName)
            {
                tcs.TrySetResult(true);
            }
        }
        
        // 订阅事件
        OnSignalReceived += OnSignalReceivedHandler;
        
        try
        {
            await Task.WhenAny(
                tcs.Task,
                Task.Delay(-1, timeoutSource.Token)
            );
            
            if (timeoutSource.Token.IsCancellationRequested)
            {
                throw new TimeoutException($"等待信号 {signalName} 超时");
            }
        }
        finally
        {
            // 取消订阅
            OnSignalReceived -= OnSignalReceivedHandler;
            timeoutSource.Dispose();
        }
    }
    
    public async Task SendSignal(string signalName, object data = null)
    {
        if (!IsConnected)
            throw new InvalidOperationException("未连接");
        
        try
        {
            /*var signal = new 
            { 
                type = signalName,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                data = data ?? new { }
            };*/

            var signal = Message.Create(type:signalName, data:data);
            
            string jsonSignal = JsonSerializer.Serialize(signal);
            byte[] bytes = Encoding.UTF8.GetBytes(jsonSignal + "\n");
            await _stream.WriteAsync(bytes, 0, bytes.Length);
            OnLog?.Invoke($"信号已发送: {signalName}");
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"发送信号失败: {ex.Message}");
            throw;
        }
    }
    
    private void Cleanup()
    {
        StopReceiving();
        
        _acceptCts?.Cancel();
        
        _heartbeat?.Dispose();
        _heartbeat = null;
        
        try { _stream?.Close(); } catch { }
        try { _client?.Close(); } catch { }
        try { _server?.Stop(); } catch { }
        
        // 不重置 OnConnectionStateChanged 事件，让UI知道连接已关闭
        if (_client != null)
        {
            OnConnectionStateChanged?.Invoke(ConnectionStatusEnum.Closed, "连接已关闭");
        }
    }
    
    public void Dispose()
    {
        Cleanup();
        _receiveCts?.Dispose();
    }
}

public static class TaskExtensions
{
    public static async Task WithCancellation(this Task task, CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
        
        using (cancellationToken.Register(() => tcs.TrySetCanceled()))
        {
            if (task != await Task.WhenAny(task, tcs.Task))
            {
                throw new OperationCanceledException(cancellationToken);
            }
            
            await task;
        }
    }
    
    public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
        
        using (cancellationToken.Register(() => tcs.TrySetCanceled()))
        {
            if (task != await Task.WhenAny(task, tcs.Task))
            {
                throw new OperationCanceledException(cancellationToken);
            }
            
            return await task;
        }
    }
}

public enum ConnectionStatusEnum : byte
{
    ReadytoConnect,
    Connecting,
    Connected,
    ConnectionLost,
    ReadytoClose,
    Closed
}