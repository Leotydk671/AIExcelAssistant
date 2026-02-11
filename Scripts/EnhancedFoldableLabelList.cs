using Godot;
using System;


[GlobalClass]
public partial class EnhancedFoldableLabelList : FoldableLabelList
{
    [Signal]
    public delegate void LabelClickedEventHandler(string labelText, int index);
    
    [Signal]
    public delegate void LabelDoubleClickedEventHandler(string labelText, int index);
    
    [Export]
    public bool EnableSelection { get; set; } = true;
    
    [Export]
    public Color SelectedColor { get; set; } = new Color(0.3f, 0.5f, 0.8f, 0.3f);
    
    [Export]
    public Color HoverColor { get; set; } = new Color(0.4f, 0.4f, 0.4f, 0.2f);

    [Export]
    public Color SelectedOpenColor { get; set; } = new Color(0.3f, 0.5f, 0.8f, 0.3f);

	private PanelContainer _selectedOpenContainer;
    
    private LineEdit _searchBox;
    private Button _clearSearchButton;
    private int _selectedIndex = -1;
    
    public override void _Ready()
    {
        base._Ready();
        
        // 添加搜索框
        AddSearchBox();
    }
    
    private void AddSearchBox()
    {
        var searchContainer = new HBoxContainer();
        searchContainer.Name = "SearchContainer";
        searchContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        
        _searchBox = new LineEdit();
        _searchBox.Name = "SearchBox";
        _searchBox.PlaceholderText = "搜索标签...";
        _searchBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _searchBox.TextChanged += OnSearchTextChanged;
        
        _clearSearchButton = new Button();
        _clearSearchButton.Name = "ClearSearchButton";
        _clearSearchButton.Text = "X";
        _clearSearchButton.Visible = false;
        _clearSearchButton.Pressed += ClearSearch;
        
        searchContainer.AddChild(_searchBox);
        searchContainer.AddChild(_clearSearchButton);
        
        // 将搜索框添加到标签容器顶部
        _labelContainer.AddChild(searchContainer);
        _labelContainer.MoveChild(searchContainer, 0);
    }
    
    private void OnSearchTextChanged(string newText)
    {
        _clearSearchButton.Visible = !string.IsNullOrEmpty(newText);
        FilterLabels(newText);
    }
    
    private void ClearSearch()
    {
        _searchBox.Text = "";
        FilterLabels("");
    }
    
    private void FilterLabels(string filter)
    {
        foreach (var label in GetLabels())
        {
            bool matches = string.IsNullOrEmpty(filter) || 
                          label.Text.Contains(filter, StringComparison.OrdinalIgnoreCase);
            label.Visible = matches;
        }
    }
    
    // 重写AddLabel方法，添加交互功能
    public new void AddLabel(string text, in ProjectStep projectStep, string name = "")
    {
        var labelContainer = new PanelContainer();
        labelContainer.Name = string.IsNullOrEmpty(name) ? $"LabelContainer_{GetLabelCount()}" : name;
        labelContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        
        var label = new LabelWithStep();
        label.Name = "Label";
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.CustomMinimumSize = new Vector2(0, Mathf.Floor(635.0f / MaxVisibleLabels));
        label.AddThemeFontSizeOverride("font_size", 28);
        label.ClipText = true;
        label.SetStep(in projectStep, true);
		GD.Print($"设置最小labelSize： {_listPanel.Size.Y}");

        labelContainer.AddChild(label);
        
        // 设置标签容器样式
        var style = new StyleBoxFlat();
        style.BgColor = Colors.Transparent;
        style.BorderWidthBottom = 2;
        style.BorderColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        labelContainer.AddThemeStyleboxOverride("panel", style);

        labelContainer.Theme = GD.Load<Theme>("res://Theme/TooltipTheme.tres");
        
		int num = GetLabelCount();
		GD.Print($"加入时，大小：{num}");

        // 添加鼠标交互
        labelContainer.MouseEntered += () => OnLabelMouseEnter(labelContainer);
        labelContainer.MouseExited += () => OnLabelMouseExit(labelContainer);
        labelContainer.GuiInput += (eventObj) => OnLabelGuiInput(eventObj, labelContainer, num);
        
        _labelContainer.AddChild(labelContainer);
       	_labels.Add(label);
        
    }
    
    private void OnLabelMouseEnter(PanelContainer container)
    {
        if (!EnableSelection) return;
        
        if(container == _selectedOpenContainer)
            return;

        var style = container.GetThemeStylebox("panel").Duplicate() as StyleBoxFlat;
        if (style != null)
        {
            style.BgColor = HoverColor;
            container.AddThemeStyleboxOverride("panel", style);
        }
    }
    
    private void OnLabelMouseExit(PanelContainer container)
    {
        if (!EnableSelection) return;

        if(container == _selectedOpenContainer)
            return;
        
        var index = container.GetIndex();
		GD.Print($"离开了{index}");
        var isSelected = index == (_selectedIndex+1);
        
        var style = container.GetThemeStylebox("panel").Duplicate() as StyleBoxFlat;
        if (style != null)
        {
            style.BgColor = isSelected ? SelectedColor : Colors.Transparent;
            container.AddThemeStyleboxOverride("panel", style);
        }
    }
    
    private void OnLabelGuiInput(InputEvent @event, PanelContainer container, int labelIndex)
    {
		//GD.Print($"收到GUI输入事件: {@event.GetType().Name}");

        if (!EnableSelection) return;
        
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                if (mouseEvent.Pressed)
                {
                    // 单击
                    //SelectLabel(labelIndex);
                    SelectLabel(container.GetIndex() - 1);
                    //EmitSignal(SignalName.LabelClicked, container.GetNode<LabelWithStep>("Label").Text, labelIndex);
					OnLabelClicked(container, mouseEvent.GlobalPosition);
                    
                    if (mouseEvent.DoubleClick)
                    {
                        // 双击
                        //EmitSignal(SignalName.LabelDoubleClicked, container.GetNode<LabelWithStep>("Label").Text, labelIndex);
                        GD.Print("双击");
						OnLabelDoubleClicked(container, mouseEvent.GlobalPosition);
                    }
                }
            }
			else if(mouseEvent.ButtonIndex == MouseButton.Right)
			{
				if (mouseEvent.Pressed)
                {
                    // 右键单击
					//SelectLabel(labelIndex);
                    SelectLabel(container.GetIndex() - 1);
                    OnLabelRightClicked(container, mouseEvent.GlobalPosition);
                }
			}
        }
    }
    
    private void SelectLabel(int index)
    {
		GD.Print($"清除旧的选择{_selectedIndex}");
        // 清除之前的选择
        if (_selectedIndex >= 0 && _selectedIndex < GetLabelCount())
        {
            var prevContainer = _labelContainer.GetChild<PanelContainer>(_selectedIndex + 1); // +1 因为搜索框在位置0

            prevContainer.TooltipText = "";

            if(_selectedOpenContainer != prevContainer)
            {
                var prevStyle = prevContainer.GetThemeStylebox("panel").Duplicate() as StyleBoxFlat;
                if (prevStyle != null)
                {
                    prevStyle.BgColor = Colors.Transparent;
                    prevContainer.AddThemeStyleboxOverride("panel", prevStyle);
                }
            }
        }
        
        // 设置新的选择
        _selectedIndex = index;
		GD.Print($"选择{index}");

        var container = _labelContainer.GetChild<PanelContainer>(index + 1);
        var label = container.GetNode<LabelWithStep>("Label");

        container.TooltipText = (label.Step.IsCompleted ? "完成" : "未完成") 
                                + "\n" 
                                + label.Step.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

        if(_selectedOpenContainer == container)
            return;

        
        var style = container.GetThemeStylebox("panel").Duplicate() as StyleBoxFlat;
        if (style != null)
        {
            style.BgColor = SelectedColor;
            container.AddThemeStyleboxOverride("panel", style);
        }
    }
    
    public new void ClearLabels()
    {
        var container = _labelContainer;

        //GD.Print(container.GetChildCount());
        
        // 保留搜索框（索引0）
        for (int i = container.GetChildCount() - 1; i > 0; i--)
        {
            container.GetChild(i).QueueFree();
        }
        
        base.ClearLabels();
        _selectedIndex = -1;
    }

    public void DeleteOne(LabelWithStep labelWithStep)
    {
        int index = _labels.IndexOf(labelWithStep);
        GD.Print($"删除索引为{index}的元素");
        if(index != -1)
        {
            var delete_container = _labelContainer.GetChild(index + 1);
            delete_container.QueueFree();
            /*if(index + 1 == _selectedIndex)
            {
                _selectedIndex = -1;
            }
            if(delete_container == _selectedOpenContainer)
            {
                _selectedOpenContainer = null;
            }*/
        }
        _selectedIndex = -1;
        _selectedOpenContainer = null;
        _labels.RemoveAt(index);
    }

	public virtual void OnLabelClicked(PanelContainer container, Vector2 click_pos)
	{
		GD.Print("左键点击");
	}

	public virtual void OnLabelRightClicked(PanelContainer container, Vector2 click_pos)
	{
		GD.Print("右键点击");
	}

	public virtual void OnLabelDoubleClicked(PanelContainer container, Vector2 click_pos)
	{
		GD.Print("左键双击");
        SetCurrentSelected(container);
	}

    public void SetCurrentSelected(PanelContainer container)
    {
        if(_selectedOpenContainer == container)
			return;

		if(_selectedOpenContainer != null)
		{
			var prevStyle = _selectedOpenContainer.GetThemeStylebox("panel").Duplicate() as StyleBoxFlat;
			if (prevStyle != null)
            {
                prevStyle.BgColor = Colors.Transparent;
                _selectedOpenContainer.AddThemeStyleboxOverride("panel", prevStyle);
            }
		}

		_selectedOpenContainer = container;

		var style = container.GetThemeStylebox("panel").Duplicate() as StyleBoxFlat;
        if (style != null)
        {
            style.BgColor = SelectedOpenColor;
            container.AddThemeStyleboxOverride("panel", style);
        }
    }

    public LabelWithStep GetCurrentSelectedLabel()
    {
        return _selectedOpenContainer.GetNode<LabelWithStep>("Label");
    }

    public void ClearAll()
    {
        _selectedIndex = -1;
        _selectedOpenContainer = null;
        ClearLabels();
    }

}