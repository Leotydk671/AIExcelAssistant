using Godot;

public partial class FloatMenu : PanelContainer
{
	[Export]
	public Button OpenButton {get; private set; }

	[Export]
	public Button RenameButton {get; private set; }

	[Export]
	public Button DeleteButton {get; private set; }

	[Export]
	public StepRenameWindow RenameWindow { get; private set; }

	private LabelWithStep _relative_label;


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Visible = false;
		
		if(OpenButton == null)
			OpenButton = GetNode<Button>("OpenButton");
		if(RenameButton == null)
			RenameButton = GetNode<Button>("RenameButton");
		if(DeleteButton == null)
			DeleteButton = GetNode<Button>("DeleteButton");

		//OpenButton.Pressed += 
		RenameButton.Pressed += Rename;

		DeleteButton.Pressed += DeleteStep;
	}

	public void ShowMenu(Vector2 show_pos, LabelWithStep label)
	{
		_relative_label = label;

		Position = show_pos;
		Show();
	}

	public void Rename()
	{
		Hide();

		if(_relative_label == null)
			return;
		
		RenameWindow.ShowWindow( (string new_name) => { _relative_label.SetStepName(new_name, false); return _relative_label;} );
	}

	public void DeleteStep()
	{
		Hide();
		Global.Instance.DeleteStep(_relative_label);
	}

}
