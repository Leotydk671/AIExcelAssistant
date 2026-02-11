using Godot;
using System;

[GlobalClass]
public partial class SyncManager : Node
{
	public static SyncManager Instance { get; private set; } 

	[Export]
	private Control _loadingUI;

    private CommunicationManager _syncManager;

	private bool _pyReady;

	private object _lock = new();
    
    public override void _Ready()
    {
		Instance = this;

        // 初始化同步管理器
        _syncManager = new CommunicationManager();
        
        // 订阅事件
		_syncManager.OnConnectionStateChanged += HandleConnectionStateChanged;

        _syncManager.OnSignalReceived += HandleSignal;

        _syncManager.OnError += HandleError;
        _syncManager.OnLog += HandleLog;
        
        Start();
    }
    
    public override void _ExitTree()
    {
        // 清理资源
        if (_syncManager != null)
        {
			_syncManager.OnConnectionStateChanged -= HandleConnectionStateChanged;
            _syncManager.OnSignalReceived -= HandleSignal;

            _syncManager.OnError -= HandleError;
            _syncManager.OnLog -= HandleLog;
            
            _syncManager.Dispose();
        }
    }
    
	public void Start()
	{
		// 启动服务器或连接服务器
        // 方式1: 作为服务器等待连接
        StartConnectionAsync();
        
        // 方式2: 作为客户端连接服务器
        // _syncManager.ConnectToServer("127.0.0.1", 12111);
	}

    // 事件处理函数
    private void HandleSignal(string signalName)
    {
        // 在主线程中处理信号
        CallDeferred(nameof(DeferredHandleSignal), signalName);
    }
    
    private void DeferredHandleSignal(string signalName)
    {
        GD.Print($"收到信号: {signalName}");
        
        // 根据信号类型执行不同的操作
        switch (signalName)
        {
            case "execute":
                GD.Print("执行命令");
                // 发送确认
                _ = _syncManager.SendSignal("execute_ack");
                break;
            case "pause":
                GD.Print("暂停");
                break;
			case "ready":
				lock (_lock)
				{
					_pyReady = true;
				}
				break;
            case "execute_ack":
                GD.Print("收到执行确认");
                break;
            default:
				break;
        }
    }
    
    private void HandleError(string error)
	{
        CallDeferred(nameof(DeferredHandleError), error);
    }
    
	private void DeferredHandleError(string error)
	{
		GD.PrintErr($"同步管理器错误: {error}");
	}
    
    private void HandleLog(string message)
    {
        DeferredHandleLog(message);
    }

	private void DeferredHandleLog(string message)
	{
		GD.Print($"[Sync] {message}");
	}

	private async void StartConnectionAsync()
    {
        // 显示转圈界面
        ShowLoadingUI("正在等待Python程序连接...");
        
        // 异步等待连接（设置30秒超时）
        bool connected = await _syncManager.StartServerAsync(12111, 30f);
		//_ = Global.Instance.StartPyLogic();
        
        if (connected)
        {
            // 连接成功，界面已经在 HandleConnectionStateChanged 中隐藏
            GD.Print("连接成功，可以开始通信");
            
            // 连接成功后发送ready信号
            _ = _syncManager.SendSignal("ready");
        }
        else
        {
            // 连接失败或超时
            GD.Print("连接失败或超时");
            
            // 这里可以添加重试逻辑
            // 例如：await Task.Delay(2000); StartConnectionAsync();
        }
    }
    
    private void HandleConnectionStateChanged(ConnectionStatusEnum st, string message)
    {
        CallDeferred(nameof(DeferredHandleConnectionStateChanged), (byte)st, message);
    }
    
    private void DeferredHandleConnectionStateChanged(ConnectionStatusEnum st, string message)
    {
		switch (st)
		{
			case ConnectionStatusEnum.ReadytoConnect:
				GD.Print(message);
				ShowLoadingUI(message);
				break;

			case ConnectionStatusEnum.Connecting:
				GD.Print(message);
				_ = Global.Instance.StartPyLogic();
				break;

			case ConnectionStatusEnum.Connected:
				GD.Print(message);
				HideLoadingUI();
				break;

			case ConnectionStatusEnum.ConnectionLost:
				HideLoadingUI();
            	ShowErrorUI(message);
				break;
			case ConnectionStatusEnum.ReadytoClose:
				break;
			case ConnectionStatusEnum.Closed:
				ShowErrorUI(message);
				break;
			default:
				break;
		}
    }
    
    private void ShowLoadingUI(string message = "")
    {
        _loadingUI.Visible = true;
        
        // 如果有文本标签显示消息
        Label messageLabel = _loadingUI.GetNodeOrNull<Label>("MessageLabel");
        if (messageLabel != null && !string.IsNullOrEmpty(message))
        {
            messageLabel.Text = message;
        }
        
        // 启动转圈动画
        AnimationPlayer anim = _loadingUI.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (anim != null)
        {
            anim.Play("loading_spin");
        }
    }
    
    private void HideLoadingUI()
    {
        _loadingUI.Visible = false;
        
        // 停止转圈动画
        AnimationPlayer anim = _loadingUI.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (anim != null)
        {
            anim.Stop();
        }
    }
    
    private void ShowErrorUI(string errorMessage)
    {
        // 实现你的错误显示逻辑
        GD.PrintErr($"需要显示错误界面: {errorMessage}");
    }
    
    // 手动重试连接的方法
    public async void RetryConnection()
    {
        ShowLoadingUI("正在重新连接...");
        bool connected = await _syncManager.StartServerAsync(12111, 30f);
        
        if (!connected)
        {
            ShowErrorUI("重试连接失败");
        }
    }
    
    // 手动取消等待连接
    public void CancelWaiting()
    {
        _syncManager.StopWaitingForConnection();
        HideLoadingUI();
    }
    

    // 示例：发送信号并等待响应
    public async void ExecuteWithResponse(object data)
    {
        if (_syncManager?.IsConnected != true)
        {
            GD.PrintErr("未连接，无法发送信号");
            return;
        }
        
        try
        {
            // 发送执行信号
            await _syncManager.SendSignal("execute", data);
            
            // 等待确认信号
            await _syncManager.WaitForSignal("execute_ack", 10);
            GD.Print("执行确认已收到");
        }
        catch (TimeoutException)
        {
            GD.PrintErr("等待确认超时");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"执行失败: {ex.Message}");
        }
    }

	public bool IsPyReady()
	{
		lock(_lock)
		{
			return _pyReady;
		}
	}
}
