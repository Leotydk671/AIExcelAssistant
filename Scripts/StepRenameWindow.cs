using Godot;


public partial class StepRenameWindow : Window
{
	[Export]
	public LineEdit RenameLineEdit { get; private set; }

	[Export]
	public Button RenameConfirmButton { get; private set; }

	[Export]
	public WorkSpace workSpaceNode { get; private set; }


	public delegate LabelWithStep CurrentStepRenameFunc(string name);

	public CurrentStepRenameFunc CurrentStepRenameCall = null; 

	public bool Renaming { get; private set; } = false;


	public override void _Ready()
	{
		CloseRequested += CloseWindow;
		RenameConfirmButton.Pressed += RenameTheStep;
	}

	public void CloseWindow()
	{
		Hide();
        RenameLineEdit.Text = "";
        Renaming = false;
	}

	public void ShowWindow(CurrentStepRenameFunc callback)
	{
		CurrentStepRenameCall = callback;
		Renaming = false;
		Show();
	}

	private void RenameTheStep()
	{
		LabelWithStep labelWithStep = CurrentStepRenameCall?.Invoke(RenameLineEdit.Text);
		if(workSpaceNode.GetCurrentSelectedLabel() == labelWithStep)
		{
			workSpaceNode.EditableContainer.RenameLocalSync(false);
		}
		Global.Instance.SetNoticeRect(ProjectNotice.NoticeRectType.UNSAVE);
		CloseWindow();
	}

}

