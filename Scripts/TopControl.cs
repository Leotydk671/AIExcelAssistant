using Godot;
using ProjectNotice;
using System;
using System.IO;

namespace ProjectNotice
{
	public enum NoticeRectType : byte
	{
		UNSAVE,
		DOING,
		WARNING,
	}
}

public partial class TopControl : Control
{
	[Export]
    private WorkSpace _workspace;

	[Export]
	private Button _addLabelButton;

	[Export]
    private Label _projectLabel;

	[Export]
	private ProjectRenameWindow _renameWindow;

	[Export]
	private Button _fileButton;

	private string projectLabelText;

	public override void _Ready()
	{
		if(_workspace == null)
			_workspace = GetNode<WorkSpace>("FoldableLabelList");
      
		_addLabelButton.ButtonDown += OnAddLabelButtonClicked;
		_projectLabel.GuiInput += OnProjectLabelGuiInput;
		_fileButton.Pressed += SelectedRelativeFile;

		Global.Instance.CurrentProjectSet += SetProjectLabel;
		Global.Instance.CurrentProjectRename += RenameProjectLabelUnSave;
		Global.Instance.CurrentProjectClear += ClearAll;
		Global.Instance.CurrentProjectSaved += () => _projectLabel.Text = projectLabelText;
	}

	private void OnAddLabelButtonClicked()
	{
		if(Global.Instance.currentProject == null)
			return;

        ProjectStep projectStep = new("新步骤", "");

        ProjectManager.Instance.AddSteptoProject(Global.Instance.currentProject, ref projectStep);

		_workspace.AddLabel("新步骤", in projectStep);

        //ProjectManager.Instance.SaveProject(project);
	}

	private void OnProjectLabelGuiInput(InputEvent @event)
	{	
		if(_renameWindow.Renaming || Global.Instance.currentProject == null)
			return;

		if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed)
            {
                if (mouseEvent.DoubleClick)
				{
					_renameWindow.ShowWindow();
				}
                
            }
		}
	}

	private void SetProjectLabel()
	{
		Project project = Global.Instance.currentProject;
		if(project == null)
		{
			projectLabelText = "";
		}
		else 
		{
			projectLabelText = project.Name;
			SetProjectRelativaFileButton(project.FilePath);
		}
		_projectLabel.Text = projectLabelText;
	}

	private void SetProjectRelativaFileButton(string file_path)
	{
		if(Global.Instance.CheckFilePath(file_path))
		{
			_fileButton.Text = Path.GetFileName(file_path);
		}
		else
		{
			_fileButton.Text = "无";
		}
	}
		
	private void SelectedRelativeFile()
	{
		DisplayServer.FileDialogShow(
				title: "设置操作文件", 
				currentDirectory: "C:/Users",
				fileName: "",
				showHidden: false,
				mode: DisplayServer.FileDialogMode.OpenFile,
				filters: ["*.xlsx"],
				callback: new Callable(this, nameof(OnFileDialogResult)));
	}

	private void OnFileDialogResult(bool status, Godot.Collections.Array<string> selectedPaths, int selectedFilterIndex)
	{
		if(selectedPaths.Count > 0)
		{
			GD.Print($"选择了：{selectedPaths[0]}");
			SetProjectRelativaFileButton(selectedPaths[0]);
			Global.Instance.currentProject.FilePath = selectedPaths[0];
			Global.Instance.SetNoticeRect(NoticeRectType.UNSAVE);
		}
	}

	private void RenameProjectLabelUnSave(string name, bool save)
	{
		GD.Print("项目改名字");
		projectLabelText = name;
		if(save)
		{
			_projectLabel.Text = name;
		}
		else
		{
			_projectLabel.Text = "* " + name;
		}
	}

	private void ClearAll()
	{
		projectLabelText = "";
		_projectLabel.Text = "";
	}



}
