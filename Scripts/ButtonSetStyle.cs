using Godot;

public partial class ButtonSetStyle : Button
{
	//[Export]
	//public StyleBoxLabel StyleBox {get; private set;}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		//var styleBox = GD.Load<StyleBoxLabel>("res://Theme/LabelButtonBox.tres");
		GD.Print("添加样式");
		//AddThemeStyleboxOverride("normal", styleBox);	
	}

}
