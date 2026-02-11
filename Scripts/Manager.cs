using Godot;


public partial class Manager : Control
{
	[Export]
    private WorkSpace _workspace;
    
    public override void _Ready()
    {
        if(_workspace == null)
			_workspace = GetNode<WorkSpace>("FoldableLabelList");

        GuiInput += OnGuiInput;
        Global.Instance.CurrentProjectSet += ShowWorkspace;
        Global.Instance.CurrentProjectClear += () => _workspace.Hide();
    }

    private void OnGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            _workspace.TryHideFloatMenu(mouseEvent.GlobalPosition);
        }
    }

    private void ShowWorkspace()
    {
        _workspace.LoadGlobalProject();
        _workspace.CloseEditableArea();
        _workspace.Show();
    }
    
}
