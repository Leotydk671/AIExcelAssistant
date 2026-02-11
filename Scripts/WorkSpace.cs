using Godot;

public partial class WorkSpace : EnhancedFoldableLabelList
{
	[Export]
	public FloatMenu FloatOptionMenu { get; private set; }

	[Export]
	public EditableArea EditableContainer { get; private set; }

    public override void _Ready()
    {
        base._Ready();
		Global.Instance.SetWorkSpace(this);
    }


	public void TryHideFloatMenu(Vector2 clicked_pos)
	{
		if(FloatOptionMenu.Visible == false)
			return;

		var menuRect = new Rect2(FloatOptionMenu.GlobalPosition, FloatOptionMenu.Size);
		if(!menuRect.HasPoint(clicked_pos))
		{
			FloatOptionMenu.Hide();
		}
	}

    public override void OnLabelClicked(PanelContainer container, Vector2 click_pos)
    {
		GD.Print("子类被点击");
		FloatOptionMenu.Hide();
    }

    public override void OnLabelRightClicked(PanelContainer container, Vector2 click_pos)
    {
        FloatOptionMenu.ShowMenu(click_pos, container.GetNode<LabelWithStep>("Label"));
    }

    public override void OnLabelDoubleClicked(PanelContainer container, Vector2 click_pos)
    {
		base.OnLabelDoubleClicked(container, click_pos);

        EditableContainer.SetCurrentStep(container.GetNode<LabelWithStep>("Label"));
		ShowEditableArea();
    }

	public void ShowEditableArea()
	{
		EditableContainer.Show();
	}

	public void CloseEditableArea()
	{
		EditableContainer.Hide();
	}

	public new void ClearAll()
	{
		GD.Print("清除");
		base.ClearAll();
	
		EditableContainer.ClearAll();
		EditableContainer.Hide();
	}

	public void LoadGlobalProject()
	{
		ClearAll();

		GD.Print($"尝试加入{Global.Instance.currentProject.Steps.Count}个");

		foreach (var item in Global.Instance.currentProject.Steps.Values)
		{
			AddLabel(item.Name, in item);
		}

		
	}

	
}
