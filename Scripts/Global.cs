using System.IO;
using System.Threading.Tasks;
using Godot;
using ProjectNotice;

public partial class Global : Node
{
	public static Global Instance { get; private set; } 
    public Project currentProject{ get; private set; }	

	public string ProjectBaseDir { get; private set; }

	public string InterpreterPath {get; private set;}

	public string ScriptPath { get; private set; }

	public string InterFilePath { get; private set; }

	private NoticeRect _noticeRect;

	private WorkSpace _workSpace;

	private bool need_set;

	[Signal]
	public delegate void CurrentProjectClearEventHandler();

	[Signal]
	public delegate void CurrentProjectSetEventHandler();

	[Signal]
	public delegate void CurrentProjectSavedEventHandler();

	[Signal]
	public delegate void CurrentProjectRenameEventHandler(string name, bool save);

	public override void _Ready()
	{
		Instance = this;
		need_set = false;

		ProjectBaseDir = OS.GetExecutablePath().GetBaseDir();
		if(OS.HasFeature("standalone"))
		{
			InterpreterPath = Path.Combine(ProjectBaseDir, "/PythonFiles/PyWebCatch/python.exe");
			ScriptPath = Path.Combine(ProjectBaseDir, "/PythonFiles/main.py");
			InterFilePath = Path.Combine(ProjectBaseDir, "/PythonFiles/InterFiles");
		}
		else
		{
			InterpreterPath = ProjectSettings.GlobalizePath("res://PythonFiles/PyWebCatch/python.exe");
			ScriptPath = ProjectSettings.GlobalizePath("res://PythonFiles/main.py");
			InterFilePath = ProjectSettings.GlobalizePath("res://PythonFiles/InterFiles");
		}

	}

	public void SetNoticeForProject(NoticeRect noticeRect)
	{
		_noticeRect = noticeRect;
	}

	public void SetWorkSpace(WorkSpace workSpace)
	{
		_workSpace = workSpace;
		CurrentProjectClear += workSpace.ClearAll;
	}


	public void SetCurrentProject(string projectId)
	{
		currentProject = ProjectManager.Instance.LoadProject(projectId);
		EmitSignal(SignalName.CurrentProjectSet);
	}

	public void ClearCurrentProject()
	{
		currentProject = null;
		EmitSignal(SignalName.CurrentProjectClear);
	}

	public void SaveCurrentProject()
	{
		GD.Print("保存当前项目");
		RebuildCurrentProjectSteps();
		ProjectManager.Instance.SaveProject(currentProject);
		EmitSignal(SignalName.CurrentProjectSaved);
		_noticeRect.Hide();
	}

	public void RenameCurrentProject(string new_name, bool save)
	{
		currentProject.Name = new_name;

		if(save) 
			ProjectManager.Instance.SaveProject(currentProject);
		else 
			_noticeRect.ShowNoticeRect(NoticeRectType.UNSAVE);

		EmitSignal(SignalName.CurrentProjectRename, new_name, save);
	}

	public void SetNoticeRect(NoticeRectType noticeRectType)
	{
		_noticeRect.ShowNoticeRect(noticeRectType);
	}
	

	public void RebuildCurrentProjectSteps()
	{
		_workSpace.EditableContainer.SaveContent();
		foreach (var label in _workSpace.GetLabels())
		{
			if(label.UnSave)
			{
				currentProject.TryRefreshStep(ref label.Step);
			}
		}
	}

	public void DeleteStep(LabelWithStep labelWithStep)
	{
		_workSpace.DeleteOne(labelWithStep);

		currentProject.DeleteStep(labelWithStep.Step.ID);

		_noticeRect.ShowNoticeRect(NoticeRectType.UNSAVE);
	}

	public bool CheckFilePath(string path)
	{
		if(!string.IsNullOrEmpty(path) && File.Exists(path))
		{
			/*try
			{
				using (var fs = File.Open(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read))
				{
					_noticeRect.Hide();
					return true;
				}
			}
			catch {		}*/
			
			return true;
		}
		GD.Print("无效的文件");
		SetNoticeRect(NoticeRectType.WARNING);
		return false;
	}

	public async Task StartPyLogic()
	{
		//OS.Execute(InterpreterPath, [ScriptPath]);
		//SyncManager.Instance.Start();
		var timer = GetTree().CreateTimer(5.0);
		await ToSignal(timer, Timer.SignalName.Timeout);
		//return;
		int pid = OS.CreateProcess(InterpreterPath, [ScriptPath], openConsole:true);
    
		if (pid > 0)
		{
			GD.Print($"Python进程已启动，PID: {pid}");
			// 可以将pid保存起来，如果需要后续管理
		}
		else
		{
			GD.PrintErr("启动Python进程失败");
		}
	}

}
