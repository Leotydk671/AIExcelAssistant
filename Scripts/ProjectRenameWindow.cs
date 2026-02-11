using Godot;
using System;

public partial class ProjectRenameWindow : Window
{
	[Export]
	public LineEdit RenameLineEdit { get; private set; }

	[Export]
	public Button RenameConfirmButton { get; private set; }

	public bool Renaming { get; private set; } = false;

	private bool AutoSave = false;

	public override void _Ready()
	{
		CloseRequested += CloseWindow;
		RenameConfirmButton.Pressed += RenameTheProject;
	}

	public void CloseWindow()
	{
		Hide();
		Renaming = false;
		RenameLineEdit.Text = "";
	}

	public void ShowWindow(bool autosave = false)
	{
		Renaming = false;
		AutoSave = autosave;
		Show();
	}

	private void RenameTheProject()
	{
		Global.Instance.RenameCurrentProject(RenameLineEdit.Text, AutoSave);
		CloseWindow();
	}

}
