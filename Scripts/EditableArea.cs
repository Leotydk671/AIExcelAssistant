using Godot;
using System;
using System.IO;
using System.Text;

public partial class EditableArea : CenterContainer
{
	[Export]
	public Label StepNameLabel { get; private set; }

	[Export]
	public Button QuestionButton { get; private set; }

	[Export]
	public Button SaveButton { get; private set; }

	[Export]
	public Button RenameButton { get; private set; }

	[Export]
	public Button DeleteButton { get; private set; }

	[Export]
	public TextEdit ContentTextEdit { get; private set; }

	[Export]

	public StepRenameWindow RenameWindow { get; private set; }

	private LabelWithStep _relative_label;

	private bool _contentChanged = false;	

	private string _stepNameText;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if(StepNameLabel == null)
		{
			StepNameLabel = GetNode<Label>("Label");
		}
		if(QuestionButton == null)
		{
			QuestionButton = GetNode<Button>("QuestionButton");
		}
		if(SaveButton == null)
		{
			SaveButton = GetNode<Button>("SaveButton");
		}
		if(RenameButton == null)
		{
			RenameButton = GetNode<Button>("RenameButton");
		}
		if(DeleteButton == null)
		{
			DeleteButton = GetNode<Button>("DeleteButton");
		}
		if(ContentTextEdit == null)
		{
			ContentTextEdit = GetNode<TextEdit>("ContentTextEdit");
        }


		ContentTextEdit.TextChanged += () =>
		{
			if(_contentChanged)
				return;
			StepNameLabel.Modulate = new Color(0xe47f80ff);
			StepNameLabel.Text = "* " + _stepNameText;
			QuestionButton.Disabled = true;
		};

		SaveButton.Pressed += SaveContent;

        RenameButton.Pressed += Rename;

		DeleteButton.Pressed += DeleteStep;

		QuestionButton.Pressed += PrepareQuestion;

		Global.Instance.CurrentProjectSaved += CoolDown;
	}

	public void SetCurrentStep(LabelWithStep label)
	{
		_relative_label = label;

		ref readonly ProjectStep projectStep = ref label.Step;

		StepNameLabel.Text = projectStep.Name;
		_stepNameText = projectStep.Name;

		ContentTextEdit.Text = projectStep.Content;
		//ContentTextEdit.GetEnd();

	}

	private void SetAllDisabled()
	{
		QuestionButton.Disabled = true;

		DeleteButton.Disabled = true;

		ContentTextEdit.Editable = false;
	}

	private void SetAllAbled()
	{
		QuestionButton.Disabled = false;

		DeleteButton.Disabled = false;

		ContentTextEdit.Editable = true;
	}

	public void ClearAll()
	{
		_relative_label = null;
		StepNameLabel.Text = "";
		_stepNameText = "";
		ContentTextEdit.Text = "";
	}

	public void RenameLocalSync(bool saved)
	{
		ref readonly ProjectStep projectStep = ref _relative_label.Step;

		_stepNameText = projectStep.Name;
		if(saved)
			StepNameLabel.Text = projectStep.Name;
		else
			StepNameLabel.Text = "* " + projectStep.Name;
	}

	public void Rename()
	{	
		if(_relative_label == null)
			return;
		
		RenameWindow.ShowWindow( (string new_name) => { _relative_label.SetStepName(new_name, false); return _relative_label; } );

	}


	public void SaveContent()
	{
		_relative_label?.SetContent(ContentTextEdit.Text);
		CoolDown();
	}


	private void CoolDown()
	{
		StepNameLabel.Text = _stepNameText;	
		StepNameLabel.Modulate = Colors.White;
		QuestionButton.Disabled = false;
	}

	public void DeleteStep()
	{
		Global.Instance.DeleteStep(_relative_label);
		ClearAll();
		Hide();
	}

	public void PrepareQuestion()
	{
		if(!Global.Instance.CheckFilePath(Global.Instance.currentProject.FilePath))
		{
			return;	
		}

		string directory = Global.Instance.InterFilePath;
		GD.Print($"准备写入{directory}");
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            GD.Print($"创建目录: {directory}");
        }

		string _filePath = Path.Combine(directory, "input.txt");
		if(string.IsNullOrEmpty(ContentTextEdit.Text))
		{
			GD.Print("没东西给它");
			return;
		}

		try
        {
            using (var writer = new StreamWriter(_filePath, false, Encoding.UTF8))
            {
                writer.Write(ContentTextEdit.Text);
            }
            GD.Print($"已写入文件: {_filePath}");
			if(SyncManager.Instance.IsPyReady())
			{
				SyncManager.Instance.ExecuteWithResponse(new { rfile = Global.Instance.currentProject.FilePath});
			}
        }
        catch (Exception ex)
        {
            GD.PrintErr($"写入文件失败: {ex.Message}");
            throw;
        }
	}
}
