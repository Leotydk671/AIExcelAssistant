using Godot;
using System.Collections.Generic;

public partial class ProjectSelectionWindow : Window
{
    [Export] private ItemList _projectList;
    [Export] private Button _openButton;
    [Export] private Button _deleteButton;
    [Export] private Button _newProjectButton;
    [Export] private Button _refreshButton;
    [Export] private LineEdit _searchBox;
    [Export] private Button _cancelButton;
    
    private List<ProjectSummary> _currentProjects;
    private string _selectedProjectId;

	private bool CreatingProject = false;
    
    public delegate void ProjectSelectedEventHandler(string projectId);
    
    public override void _Ready()
    {
        // 连接信号
        _projectList.ItemSelected += OnProjectItemSelected;
        _projectList.ItemActivated += OnProjectItemActivated;
        
        _openButton.Pressed += OnOpenButtonPressed;
        _deleteButton.Pressed += OnDeleteButtonPressed;
        _newProjectButton.Pressed += OnNewProjectButtonPressed;
        _refreshButton.Pressed += OnRefreshButtonPressed;
        _cancelButton.Pressed += OnCancelButtonPressed;
        
        _searchBox.TextChanged += OnSearchTextChanged;
        
        CloseRequested += () => Hide();
        
        // 初始加载
        RefreshProjectList();
    }
    
    private void RefreshProjectList(string searchKeyword = "")
    {
        _projectList.Clear();
        
        if (string.IsNullOrWhiteSpace(searchKeyword))
        {
            _currentProjects = ProjectManager.Instance.GetAllProjects();
        }
        else
        {
            _currentProjects = ProjectManager.Instance.SearchProjects(searchKeyword);
        }
        
        foreach (var project in _currentProjects)
        {
            var itemIndex = _projectList.AddItem($"{project.Name}\n{project.Description}");
            
            // 设置元数据存储项目ID
            _projectList.SetItemMetadata(itemIndex, new Godot.Collections.Dictionary
            {
                ["id"] = project.Id
            });
            
            // 设置工具提示
            _projectList.SetItemTooltip(itemIndex, 
                $"最后修改: {project.LastModifiedString}\n" +
                $"进度: {project.ProgressString}\n" +
                $"标签: {string.Join(", ", project.Tags)}");
            
            // 显示进度（可选）
            var progressBar = new ProgressBar
            {
                Value = project.Progress * 100,
                Size = new Vector2(200, 16)
            };
            _projectList.SetItemCustomBgColor(itemIndex, new Color(0.2f, 0.2f, 0.2f));
        }
        
        UpdateButtonStates();
    }
    
    private void OnProjectItemSelected(long index)
    {
        if (index >= 0 && index < _projectList.ItemCount)
        {
            var metadata = (Godot.Collections.Dictionary)_projectList.GetItemMetadata((int)index);
            _selectedProjectId = (string)metadata["id"];
            UpdateButtonStates();
        }
    }
    
    private void OnProjectItemActivated(long index)
    {
        if (index >= 0 && index < _projectList.ItemCount)
        {
            var metadata = (Godot.Collections.Dictionary)_projectList.GetItemMetadata((int)index);
            _selectedProjectId = (string)metadata["id"];
            OpenSelectedProject();
        }
    }
    
    private void OnOpenButtonPressed()
    {
        if (!string.IsNullOrEmpty(_selectedProjectId))
        {
            OpenSelectedProject();
        }
    }
    
    private void OnDeleteButtonPressed()
    {
        if (!string.IsNullOrEmpty(_selectedProjectId))
        {
            ShowDeleteConfirmation();
        }
    }
    
    private void OnNewProjectButtonPressed()
    {
        if(!CreatingProject)
			ShowCreateProjectDialog();
    }
    
    private void OnRefreshButtonPressed()
    {
        RefreshProjectList(_searchBox.Text);
    }
    
    private void OnCancelButtonPressed()
    {
        Hide();
    }
    
    private void OnSearchTextChanged(string newText)
    {
        RefreshProjectList(newText);
    }
    
    private void UpdateButtonStates()
    {
        bool hasSelection = !string.IsNullOrEmpty(_selectedProjectId);
        _openButton.Disabled = !hasSelection;
        _deleteButton.Disabled = !hasSelection;
    }
    
    private void OpenSelectedProject()
    {
        GD.Print("打开项目");
        //OnProjectSelected?.Invoke(_selectedProjectId);
        Global.Instance.SetCurrentProject(_selectedProjectId);
        Hide();
    }
    
    private void ShowDeleteConfirmation()
    {
        var dialog = new ConfirmationDialog();
        dialog.Title = "删除项目";
        dialog.DialogText = "确定要删除这个项目吗？此操作不可撤销。";
        dialog.Confirmed += () =>
        {
            if (ProjectManager.Instance.DeleteProject(_selectedProjectId))
            {
                RefreshProjectList(_searchBox.Text);
                _selectedProjectId = null;
                UpdateButtonStates();
                Global.Instance.ClearCurrentProject();
            }
        };
        
        AddChild(dialog);
        dialog.PopupCentered();
    }
    
    private void ShowCreateProjectDialog()
    {
		CreatingProject = true;
        var dialog = new Window();
        dialog.Title = "新建项目";
        dialog.Size = new Vector2I(400, 340);
		dialog.Unresizable = true;
		dialog.AlwaysOnTop = true;
        
        var vbox = new VBoxContainer();
        vbox.AnchorRight = 1;
        vbox.AnchorBottom = 1;
        vbox.OffsetLeft = 20;
        vbox.OffsetRight = -20;
        vbox.OffsetTop = 20;
        vbox.OffsetBottom = -20;
        
        var nameLabel = new Label { Text = "项目名称:" };
        var nameInput = new LineEdit { PlaceholderText = "输入项目名称" };
        
        var descLabel = new Label { Text = "描述 (可选):" };
        var descInput = new TextEdit 
        { 
            CustomMinimumSize = new Vector2(0, 80),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        
        var buttonContainer = new HBoxContainer();
        buttonContainer.Alignment = BoxContainer.AlignmentMode.Center;
        
        var createButton = new Button { Text = "创建" };
        var cancelButton = new Button { Text = "取消" };
        
        createButton.Pressed += () =>
        {
            if (!string.IsNullOrWhiteSpace(nameInput.Text))
            {
                var project = ProjectManager.Instance.CreateNewProject(
                    nameInput.Text, 
                    descInput.Text
                );
                
                dialog.QueueFree();
                RefreshProjectList();
                
                // 自动打开新项目
                _selectedProjectId = project.Id;
                OpenSelectedProject();
            }
        };
        
		var _closeCreateDialog = () => 
		{
			dialog.QueueFree();
			CreatingProject = false;
		};

        cancelButton.Pressed += _closeCreateDialog;
		dialog.CloseRequested += _closeCreateDialog;
        
        buttonContainer.AddChild(createButton);
        buttonContainer.AddChild(cancelButton);
        
        vbox.AddChild(nameLabel);
        vbox.AddChild(nameInput);
        vbox.AddChild(descLabel);
        vbox.AddChild(descInput);
        vbox.AddChild(buttonContainer);
        
        dialog.AddChild(vbox);
        AddChild(dialog);
        dialog.PopupCentered();
    }
    
    // 公共方法，供外部调用显示窗口
    public void ShowWindow()
    {
        RefreshProjectList();
        _selectedProjectId = null;
        UpdateButtonStates();
        
        PopupCentered();
    }
}
