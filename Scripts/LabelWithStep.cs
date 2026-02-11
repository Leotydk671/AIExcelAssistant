using Godot;


public partial class LabelWithStep : Label
{
	private ProjectStep _step;

	public ref  ProjectStep Step => ref _step;

	public bool UnSave { get; private set; } = false;

	private string _nameText;

    public override void _Ready()
    {
        Global.Instance.CurrentProjectSaved += BeSaved;
    }

    public override void _ExitTree()
    {
        Global.Instance.CurrentProjectSaved -= BeSaved;
    }

	public void BeSaved()
	{
		UnSave = false;
		Text = _nameText;
	}

	public void SetStep(in ProjectStep projectStep, bool saved)
	{
		_step = projectStep;

		string name = projectStep.Name;
		GD.Print($"设置步骤，名字：{name}");

		_nameText = name;

		UnSave = !saved;

		if(saved)
			Text = name;
		else
			Text = "* " + name;
	}

	public void SetStepName(string name, bool saved)
	{
		_step.Name = name;
		_nameText = name;

		UnSave = !saved;
		if(saved)
			Text = name;
		else
			Text = "* " + name;
	}

	public void SetContent(string content)
	{
		_step.Content = content;

		if(!UnSave)
			Text = "* " + _nameText;

		UnSave = true;
		Global.Instance.SetNoticeRect(ProjectNotice.NoticeRectType.UNSAVE);
	}

	public void SetUnsave(bool unsave)
	{
		UnSave = unsave;
	}

}
