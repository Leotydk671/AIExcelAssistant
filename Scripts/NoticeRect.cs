using Godot;
using ProjectNotice;

public partial class NoticeRect : TextureRect
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Global.Instance.SetNoticeForProject(this);
	}

	public void ShowNoticeRect(NoticeRectType noticetype)
	{
		switch (noticetype)
		{
			case NoticeRectType.UNSAVE:
				Texture = GD.Load<CompressedTexture2D>("res://Assets/红色叹号.png");
				TooltipText = "未保存";
				break;
			case NoticeRectType.DOING:
				break;
			case NoticeRectType.WARNING:
				Texture = GD.Load<CompressedTexture2D>("res://Assets/警告.png");
				TooltipText = "文件路径不存在";
				break;
			default:
				break;
		}

		Show();
	}
}
