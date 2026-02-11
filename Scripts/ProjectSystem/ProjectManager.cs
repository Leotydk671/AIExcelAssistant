using System.Collections.Generic;
using System.Text.Json;
using System;
using Godot;


public partial class ProjectManager : Node
{
    private static ProjectManager _instance;
    public static ProjectManager Instance => _instance;
    
    private List<ProjectSummary> _projectCache;
    private bool _isCacheDirty = true;
    private string PROJECTS_DIR;
    private string INDEX_FILE;
    
    public override void _Ready()
    {
		var ProjectBaseDir = OS.GetExecutablePath().GetBaseDir();
		if(OS.HasFeature("standalone"))
		{
			PROJECTS_DIR = ProjectBaseDir + "/ProjectFiles";
			INDEX_FILE = ProjectBaseDir + "/ProhectFiles/projects_index.json";
		}
		else
		{
			PROJECTS_DIR = ProjectSettings.GlobalizePath("res://ProjectFiles");
			INDEX_FILE = ProjectSettings.GlobalizePath("res://ProjectFiles/projects_index.json");
		}

        if (_instance == null)
        {
            _instance = this;
            Initialize();
        }
        else
        {
            QueueFree();
        }

    }
    
    private void Initialize()
    {
		GD.Print($"初始化检查--PROJECTS_DIR:{PROJECTS_DIR}, INDEX_FILE{INDEX_FILE}");
        // 确保项目目录存在
        if (!DirAccess.DirExistsAbsolute(PROJECTS_DIR))
        {
            DirAccess.MakeDirAbsolute(PROJECTS_DIR);
        }
        
        // 初始化索引文件
        if (!FileAccess.FileExists(INDEX_FILE))
        {
            SaveProjectIndex(new List<ProjectSummary>());
        }
    }
    
    // === 索引管理方法 ===
    
    private List<ProjectSummary> LoadProjectIndex()
    {
        if (!FileAccess.FileExists(INDEX_FILE))
            return new List<ProjectSummary>();
            
        using var file = FileAccess.Open(INDEX_FILE, FileAccess.ModeFlags.Read);
        var json = file.GetAsText();
        
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            return JsonSerializer.Deserialize<List<ProjectSummary>>(json, options) ?? new List<ProjectSummary>();
        }
        catch (JsonException e)
        {
            GD.PrintErr($"Failed to load project index: {e.Message}");
            // 索引损坏，重建索引
            return RebuildProjectIndex();
        }
    }
    
    private void SaveProjectIndex(List<ProjectSummary> index)
    {
        // 按最后修改时间排序（最新的在前）
        index.Sort((a, b) => b.LastModified.CompareTo(a.LastModified));
        
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        var json = JsonSerializer.Serialize(index, options);
        
        using var file = FileAccess.Open(INDEX_FILE, FileAccess.ModeFlags.Write);
        file.StoreString(json);
    }
    
    // 重建索引（当索引文件损坏时）
    private List<ProjectSummary> RebuildProjectIndex()
    {
        GD.Print("Rebuilding project index...");
        var index = new List<ProjectSummary>();
        
        if (!DirAccess.DirExistsAbsolute(PROJECTS_DIR))
            return index;
        
        using var dir = DirAccess.Open(PROJECTS_DIR);
        if (dir == null) return index;
        
        dir.ListDirBegin();
        string fileName = dir.GetNext();
        
        while (fileName != "")
        {
            if (fileName.EndsWith(".json") && !fileName.EndsWith("_backup.json"))
            {
                var filePath = $"{PROJECTS_DIR}/{fileName}";
                
                try
                {
                    var project = LoadProjectFromFile(filePath);
                    if (project != null)
                    {
                        index.Add(project.ToSummary());
                    }
                }
                catch
                {
                    GD.PrintErr($"Failed to load project file: {fileName}");
                }
            }
            fileName = dir.GetNext();
        }
        
        dir.ListDirEnd();
        
        SaveProjectIndex(index);
        return index;
    }
    
    private void UpdateProjectIndex(Project project)
    {
        var index = LoadProjectIndex();
        
        // 查找现有项目
        var existing = index.FindIndex(p => p.Id == project.Id);
        
        var summary = project.ToSummary();
        
        if (existing >= 0)
        {
            index[existing] = summary;
        }
        else
        {
            index.Add(summary);
        }
        
        SaveProjectIndex(index);
        _isCacheDirty = true;
    }
    
    private void RemoveFromProjectIndex(string projectId)
    {
        var index = LoadProjectIndex();
        index.RemoveAll(p => p.Id == projectId);
        SaveProjectIndex(index);
        _isCacheDirty = true;
    }
    
    // === 项目文件管理 ===
    
    private string GetProjectFilePath(string projectId)
    {
        return $"{PROJECTS_DIR}/{projectId}.json";
    }
    
    private string GetProjectBackupFilePath(string projectId)
    {
        return $"{PROJECTS_DIR}/{projectId}_backup.json";
    }
    
    // 保存项目到文件
    public void SaveProject(Project project)
    {
        project.LastModified = DateTime.Now;
        
        var filePath = GetProjectFilePath(project.Id);
        
        try
        {
            // 创建备份
            CreateBackup(project);
            
            // 保存主文件
            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
            var json = project.ToJson();
            file.StoreString(json);
            
            // 更新索引
            UpdateProjectIndex(project);
            
            GD.Print($"Project saved: {project.Name}");
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"Failed to save project {project.Name}: {e.Message}");
            throw;
        }
    }
    
    // 从文件加载项目
    public Project LoadProject(string projectId)
    {
        var filePath = GetProjectFilePath(projectId);
        return LoadProjectFromFile(filePath);
    }
    
    private Project LoadProjectFromFile(string filePath)
    {
        if (!FileAccess.FileExists(filePath))
        {
            GD.PrintErr($"Project file not found: {filePath}");
            return null;
        }
        
        try
        {
            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            var json = file.GetAsText();
            return Project.FromJson(json);
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"Failed to load project from {filePath}: {e.Message}");
            return null;
        }
    }
    
    // 删除项目
    public bool DeleteProject(string projectId)
    {
        try
        {
            var filePath = GetProjectFilePath(projectId);
            
            if (FileAccess.FileExists(filePath))
            {
                // 删除项目文件
                DirAccess.RemoveAbsolute(filePath);
                
                // 删除备份文件（如果存在）
                var backupPath = GetProjectBackupFilePath(projectId);
                if (FileAccess.FileExists(backupPath))
                {
                    DirAccess.RemoveAbsolute(backupPath);
                }
                
                // 从索引中移除
                RemoveFromProjectIndex(projectId);
                
                GD.Print($"Project deleted: {projectId}");
                return true;
            }
            return false;
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"Failed to delete project {projectId}: {e.Message}");
            return false;
        }
    }
    
    // === 公共API ===
    
    public void AddSteptoProject(Project project, ref ProjectStep projectStep)
    {
        project.AddStep(ref projectStep);
        SaveProject(project);
    }

    public List<ProjectSummary> GetAllProjects()
    {
        if (_isCacheDirty || _projectCache == null)
        {
            _projectCache = LoadProjectIndex();
            _isCacheDirty = false;
        }
        return new List<ProjectSummary>(_projectCache);
    }
    
    public Project CreateNewProject(string name, string description = "")
    {
        var project = new Project(name)
        {
            Description = description
        };
        
        // 添加默认步骤
        //project.Steps.Add(new ProjectStep("第一步", "开始你的项目..."));
        
        SaveProject(project);
        return project;
    }
    
    // 搜索项目
    public List<ProjectSummary> SearchProjects(string keyword)
    {
        var allProjects = GetAllProjects();
        
        if (string.IsNullOrWhiteSpace(keyword))
            return allProjects;
            
        keyword = keyword.ToLower();
        
        var results = new List<ProjectSummary>();
        foreach (var project in allProjects)
        {
            if (project.Name.ToLower().Contains(keyword) ||
                project.Description.ToLower().Contains(keyword) ||
                project.Tags.Exists(tag => tag.ToLower().Contains(keyword)))
            {
                results.Add(project);
            }
        }
        
        return results;
    }
    
    // 按标签筛选
    public List<ProjectSummary> FilterByTag(string tag)
    {
        var allProjects = GetAllProjects();
        return allProjects.FindAll(p => p.Tags.Contains(tag));
    }
    
    // 获取所有标签
    public List<string> GetAllTags()
    {
        var allProjects = GetAllProjects();
        var tags = new HashSet<string>();
        
        foreach (var project in allProjects)
        {
            foreach (var tag in project.Tags)
            {
                tags.Add(tag);
            }
        }
        
        return new List<string>(tags);
    }
    
    // === 备份功能 ===
    
    private void CreateBackup(Project project)
    {
        try
        {
            var backupPath = GetProjectBackupFilePath(project.Id);
            using var backupFile = FileAccess.Open(backupPath, FileAccess.ModeFlags.Write);
            var json = project.ToJson();
            backupFile.StoreString(json);
        }
        catch
        {
            // 备份失败不影响主操作
			GD.Print("备份失败，不影响主操作");
        }
    }
    
    public Project RestoreFromBackup(string projectId)
    {
        var backupPath = GetProjectBackupFilePath(projectId);
        
        if (FileAccess.FileExists(backupPath))
        {
            var project = LoadProjectFromFile(backupPath);
            if (project != null)
            {
                SaveProject(project); // 这会覆盖当前文件并更新索引
                return project;
            }
        }
        
        return null;
    }
    
    // 手动触发索引重建
    public void RebuildIndex()
    {
        _projectCache = RebuildProjectIndex();
        _isCacheDirty = false;
        GD.Print("Project index rebuilt");
    }
    
    // 获取项目统计信息
    public (int totalProjects, int totalSteps, int completedSteps) GetStats()
    {
        var projects = GetAllProjects();
        int totalSteps = 0;
        int completedSteps = 0;
        
        foreach (var project in projects)
        {
            totalSteps += project.TotalSteps;
            completedSteps += project.CompletedSteps;
        }
        
        return (projects.Count, totalSteps, completedSteps);
    }
}
