using System;
using System.Collections.Generic;
using Godot;

public partial class MenuSystem : MarginContainer
{
	[Export]
	public ProjectSelectionWindow ProjectselectWindow { get; private set; }

	[Export]
	public ProjectRenameWindow RenameWindow { get; private set; }

	[Export]
	public StyleBoxFlat MenuNormBox { get; private set; }

	[Export]
	public StyleBoxFlat MenuHoverBox { get; private set; }

	[Export]
	public StyleBoxFlat MenuPressedBox { get; private set; }

	[Export]
	public StyleBoxFlat MenuPressedHoverBox { get; private set; }


	[Export]
	public Color FontNormColor { get; private set; }

	[Export]
	public Color FontHoverColor { get; private set; }

	[Export]
	public Color FontPressedColor { get; private set; }

	[Export]
	public Color FontPressedHoverColor { get; private set; } 


	[Export]
	public StyleBoxLine PopupMenuSeparatorBox { get; private set; }

	[Export]
	public StyleBoxFlat PopupMenuNormBox { get; private set; }

	[Export]
	public StyleBoxFlat PopupMenuHoverBox { get; private set; }

	[Export]
	public Color PopupMenuFontColor { get; private set; }

	[Export]
	public Color PopupMenuAcceleratorFontColor { get; private set; }

	[Export]
	public Color PopupMenuHoverFontColor { get; private set; }


	[Export]
	public int FontSize { get; private set;}


	private List<(PopupMenu popup, List<int> indexs)> NeedSetList = new();

	private bool AlreadySet = false;


    public override void _Ready()
    {
        var menuBar = new HBoxContainer();
		menuBar.AnchorLeft = 0.0f;
		menuBar.AnchorRight = 1.0f;
		menuBar.AnchorTop = 0.0f;
		menuBar.AnchorBottom = 1.0f;

		menuBar.OffsetLeft = 0;
		menuBar.OffsetBottom = 0;
		menuBar.OffsetRight = 0;
		menuBar.OffsetTop = 0;

		menuBar.AddThemeConstantOverride("separation", 0);

        AddChild(menuBar);

		Key base_key = (Key)(KeyModifierMask.MaskCtrl | KeyModifierMask.MaskShift);
        
        // 文件菜单
        var fileBtn = CreateMenuButtonWithItems("项目", new (string, Key, bool)[] 
        {
            ("项目管理", Key.N | base_key, false),
            ("重命名", Key.O | base_key, true),
            ("保存", Key.S | (Key)KeyModifierMask.MaskCtrl, true),
            ("-", Key.None, false ),
            ("关闭", Key.C | base_key, true),
            ("-", Key.None, false ),
            ("退出", Key.Q | base_key, false)
        });
        menuBar.AddChild(fileBtn);
        
        // 编辑菜单
        var editBtn = CreateMenuButtonWithItems("编辑", new (string, Key, bool)[] 
        {
            ("撤销", Key.Z | base_key , false),
            ("重做", Key.Y | base_key, false ),
            ("-", Key.None, false ),
            ("剪切", Key.X | base_key, false ),
            ("复制", Key.L | base_key, false ),
            ("粘贴", Key.V | base_key, false )
        });
        menuBar.AddChild(editBtn);


		Global.Instance.CurrentProjectSet += () => SetGlobalNeed(false);
    }
    
    private MenuButton CreateMenuButtonWithItems(string menuName, (string text, Key key, bool need_set)[] items)
    {
        MenuButton menuButton = new MenuButton{
				Text = menuName,
				CustomMinimumSize = new Vector2(70, 0),
				Flat = false,
				SwitchOnHover = true
			};

        menuButton.AddThemeFontSizeOverride("font_size", FontSize);
		menuButton.AddThemeColorOverride("font_color", FontNormColor);
		menuButton.AddThemeColorOverride("font_hover_color", FontHoverColor);
		menuButton.AddThemeColorOverride("font_pressed_color", FontPressedColor);
		menuButton.AddThemeColorOverride("font_hover_pressed_color", FontPressedHoverColor);

		menuButton.AddThemeStyleboxOverride("normal", MenuNormBox);
		menuButton.AddThemeStyleboxOverride("hover", MenuHoverBox);
		menuButton.AddThemeStyleboxOverride("pressed", MenuPressedBox);
		menuButton.AddThemeStyleboxOverride("hover_pressed", MenuPressedHoverBox);
        
        PopupMenu popup = menuButton.GetPopup();
		popup.AddThemeStyleboxOverride("panel", PopupMenuNormBox);
		popup.AddThemeStyleboxOverride("hover", PopupMenuHoverBox);
		popup.AddThemeStyleboxOverride("separator", PopupMenuSeparatorBox);

		popup.AddThemeConstantOverride("h_separation", 20);
		popup.AddThemeConstantOverride("v_separation", 10);
		popup.AddThemeFontSizeOverride("font_size", FontSize-2);
		popup.AddThemeColorOverride("font_color", PopupMenuFontColor);
		popup.AddThemeColorOverride("font_accelerator_color", PopupMenuAcceleratorFontColor);
		popup.AddThemeColorOverride("font_hover_color", PopupMenuHoverFontColor);
        
		int count = 0;
        (PopupMenu popup, List<int> indexs) record = new()
        {
            popup = popup,
			indexs = new List<int>()
        };

        foreach (var item in items)
        {
            if (item.text == "-")
            {
                popup.AddSeparator();
            }
            else
            {
                string menuText = item.text;
                popup.AddItem(menuText, accel:item.key);
				if(item.need_set)
				{
					popup.SetItemDisabled(count, false);
					record.indexs.Add(count);
				}

            }

			count++;
        }

		NeedSetList.Add(record);
		GD.Print($"加入了{record.indexs.Count}个");
        
        // 连接信号
        popup.IdPressed += (id) => 
        {  
            string itemText = popup.GetItemText((int)id);
            GD.Print($"选择了: {menuName} -> {itemText}, index:{id}");

			switch (itemText)
			{
				case "退出":
					GetTree().Quit();
					break;
				case "项目管理":
					ProjectselectWindow?.ShowWindow();
					break;
				case "重命名":
					RenameWindow?.ShowWindow(autosave: true);
					break;
				case "保存":
					Global.Instance.SaveCurrentProject();
					break;
				case "关闭":
					Global.Instance.ClearCurrentProject();
					break;
				default:
					break;
			}
        };
        
        return menuButton;
    }

	private void SetGlobalNeed(bool set)
	{
		if(set == AlreadySet)
			return;

		GD.Print("项目已设置");

		AlreadySet = set;
		foreach (var record in NeedSetList)
		{
			foreach (var index in record.indexs)
			{ 
				record.popup.SetItemDisabled(index, set);
			}
		}
	}
}
