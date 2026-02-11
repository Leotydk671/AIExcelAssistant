// FoldableLabelList.cs
using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class FoldableLabelList : Control
{
    [Export]
    public bool IsFolded { get; set; } = false;
    
    [Export]
    public int LabelSpacing { get; set; } = 5;
    
    [Export]
    public int MaxVisibleLabels { get; set; } = 10;
    
    [Export]
    public float FoldAnimationDuration { get; set; } = 0.3f;
    
	protected Button _toggleButton;
    protected PanelContainer _listPanel;
    protected VBoxContainer _labelContainer;
    
    protected List<LabelWithStep> _labels = new List<LabelWithStep>();


    private Tween _foldTween;
    private float _originalPanelWidth;
    private bool _isAnimating = false;
    
    public override void _Ready()
    {
        // 获取节点引用
        _toggleButton = GetNode<Button>("MainContainer/ToggleButton");
        _listPanel = GetNode<PanelContainer>("MainContainer/ListPanel");
        _labelContainer = GetNode<VBoxContainer>("MainContainer/ListPanel/ScrollContainer/LabelContainer");
        
        // 保存原始宽度
        
        
        // 连接信号
        _toggleButton.Pressed += OnToggleButtonPressed;
        
        // 应用初始折叠状态
        ApplyFoldState();
        
        // 添加样式
        ApplyStyles();
    }
    
    
    private void OnToggleButtonPressed()
    {
        if (_isAnimating) return;
        
        IsFolded = !IsFolded;
        ApplyFoldStateWithAnimation();
    }
    
    private void ApplyFoldState()
    {
        if (IsFolded)
        {
            _listPanel.Visible = false;
            _toggleButton.Text = "▶";
            //_mainContainer.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        }
        else
        {
            _listPanel.Visible = true;
            _toggleButton.Text = "◀";
            //_mainContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }

    }
    
    private void ApplyFoldStateWithAnimation()
    {
        _isAnimating = true;
        
        if (_foldTween != null && _foldTween.IsValid())
        {
            _foldTween.Kill();
        }
        
        _foldTween = CreateTween();
        
        
        if (IsFoldingOut())
        {
			_foldTween.SetEase(Tween.EaseType.Out);
        	_foldTween.SetTrans(Tween.TransitionType.Quad);

            // 展开动画
            _listPanel.Visible = true;
            _listPanel.Modulate = new Color(1, 1, 1, 0);
            _listPanel.Size = new Vector2(0, _listPanel.Size.Y);
            
            _foldTween.TweenProperty(_listPanel, "size:x", _originalPanelWidth, FoldAnimationDuration);
            _foldTween.Parallel().TweenProperty(_listPanel, "modulate:a", 1, FoldAnimationDuration * 0.5f);
            _foldTween.TweenCallback(Callable.From(() => {
                _toggleButton.Text = "◀";
                _isAnimating = false;
            }));

			GD.Print($"原先宽度{_originalPanelWidth}");
        }
        else
        {
			_foldTween.SetEase(Tween.EaseType.Out);
        	_foldTween.SetTrans(Tween.TransitionType.Sine);

            // 折叠动画
			_originalPanelWidth = _listPanel.Size.X;

			_toggleButton.Visible = false;
            _foldTween.TweenProperty(_listPanel, "size:x", 0, FoldAnimationDuration);
            _foldTween.Parallel().TweenProperty(_listPanel, "modulate:a", 0, FoldAnimationDuration * 0.9f);
            _foldTween.TweenCallback(Callable.From(() => {
                _listPanel.Visible = false;
                _toggleButton.Text = "▶";
                _isAnimating = false;
				_toggleButton.Visible = true;
            }));

        }
    }
    
    private bool IsFoldingOut()
    {
        return !IsFolded && !_listPanel.Visible;
    }
    
    // 公共方法：添加标签
    public void AddLabel(string text, in ProjectStep projectStep, string name = "")
    {
		GD.Print("添加标签");
        LabelWithStep label = new();
        label.Name = string.IsNullOrEmpty(name) ? $"Label_{_labels.Count}" : name;
        label.SizeFlagsVertical = (int)Control.SizeFlags.ShrinkBegin;
        label.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        label.CustomMinimumSize = new Vector2(0, Mathf.Floor(_listPanel.Size.Y / MaxVisibleLabels));
        label.SetStep(in projectStep, true);

        _labelContainer.AddChild(label);
        _labels.Add(label);

    }
    
    // 公共方法：移除所有标签
    public void ClearLabels()
    {
        foreach (var label in _labels)
        {
            label.QueueFree();
        }
        _labels.Clear();
    }
    
    // 公共方法：获取所有标签
    public List<LabelWithStep> GetLabels()
    {
        return [.. _labels];
    }

	public int GetLabelCount()
	{
		return _labels.Count;
	}
    
    // 公共方法：设置折叠状态
    public void SetFolded(bool folded, bool animate = true)
    {
        if (folded == IsFolded) return;
        
        IsFolded = folded;
        
        if (animate)
        {
            ApplyFoldStateWithAnimation();
        }
        else
        {
            ApplyFoldState();
        }
    }
    
    private void ApplyStyles()
    {
        // 列表面板样式
        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        panelStyle.BorderColor = new Color(0.3f, 0.3f, 0.3f);
        panelStyle.BorderWidthLeft = 1;
        panelStyle.BorderWidthTop = 1;
        panelStyle.BorderWidthRight = 1;
        panelStyle.BorderWidthBottom = 1;
        panelStyle.CornerRadiusTopLeft = 3;
        panelStyle.CornerRadiusTopRight = 3;
        panelStyle.CornerRadiusBottomLeft = 3;
        panelStyle.CornerRadiusBottomRight = 3;
        panelStyle.ContentMarginLeft = 5;
        panelStyle.ContentMarginTop = 5;
        panelStyle.ContentMarginRight = 5;
        panelStyle.ContentMarginBottom = 5;
        
        _listPanel.AddThemeStyleboxOverride("panel", panelStyle);
        
        // 标签容器样式
        _labelContainer.AddThemeConstantOverride("separation", LabelSpacing);
        
        
        // 按钮样式
        var buttonStyle = new StyleBoxFlat();
        buttonStyle.BgColor = new Color(0.2f, 0.2f, 0.2f);
        buttonStyle.BorderColor = new Color(0.4f, 0.4f, 0.4f);
        buttonStyle.BorderWidthLeft = 1;
        buttonStyle.BorderWidthTop = 1;
        buttonStyle.BorderWidthRight = 1;
        buttonStyle.BorderWidthBottom = 1;
        buttonStyle.CornerRadiusTopLeft = 3;
        buttonStyle.CornerRadiusTopRight = 3;
        buttonStyle.CornerRadiusBottomLeft = 3;
        buttonStyle.CornerRadiusBottomRight = 3;
        
        _toggleButton.AddThemeStyleboxOverride("normal", buttonStyle);
        _toggleButton.AddThemeStyleboxOverride("hover", buttonStyle);
        _toggleButton.AddThemeStyleboxOverride("pressed", buttonStyle);
        
        // 设置标签样式
        var labelFont = new FontFile();
        //labelFont.FontData = FileAccess.GetFileAsBytes("res://path/to/your/font.ttf"); // 可选
        var fontSize = 14;
        
        foreach (var label in _labels)
        {
            label.AddThemeFontOverride("font", labelFont);
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", Colors.White);
        }
    }

}
