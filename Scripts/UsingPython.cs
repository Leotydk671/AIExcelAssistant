using Godot;
using System;

public partial class UsingPython : Node
{
	public string ProjectBaseDir { get; private set; }

	public string InterpreterPath {get; private set;}

	public string ScriptPath { get; private set; }


    public override void _Ready()
    {
        ProjectBaseDir = OS.GetExecutablePath().GetBaseDir();
		if(OS.HasFeature("standalone"))
		{
			InterpreterPath = ProjectBaseDir + "/PythonFiles/PyWebCatch/python.exe";
			ScriptPath = ProjectBaseDir + "/PythonFiles/deepseek_analysis.py";
		}
		else
		{
			InterpreterPath = ProjectSettings.GlobalizePath("res://PythonFiles/PyWebCatch/python.exe");
			ScriptPath = ProjectSettings.GlobalizePath("res://PythonFiles/deepseek_analysis.py");
		}
		
    }

	public void ExecutePython()
	{
		OS.Execute(InterpreterPath, [ScriptPath]);
	}

}
