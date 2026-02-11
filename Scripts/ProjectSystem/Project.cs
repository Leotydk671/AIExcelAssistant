using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;


[Serializable]
public struct ProjectStep
{
    [JsonPropertyName("id")]
    public uint ID { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("content")]
    public string Content { get; set; }
    
    [JsonPropertyName("created")]
    public DateTime CreatedAt { get; set; }
    
    [JsonPropertyName("completed")]
    public bool IsCompleted { get; set; }

    public ProjectStep(string name, string content, uint id = 0)
    {
        ID = id;
        Name = name;
        Content = content;
        CreatedAt = DateTime.Now;
        IsCompleted = false;
    }
}

[Serializable]
public class Project
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
    
    [JsonPropertyName("created")]
    public DateTime CreatedAt { get; set; }
    
    [JsonPropertyName("modified")]
    public DateTime LastModified { get; set; }
    
    [JsonPropertyName("file")]
    public string FilePath { get; set; } 
    
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("laststepid")]
    public uint LastStepId { get; set; }
    
    [JsonPropertyName("steps")]
    [JsonInclude]
    public Dictionary<string, ProjectStep> Steps { get; private set; } = new();
    
    [JsonIgnore]
    public int TotalSteps => Steps.Count;
    
    [JsonIgnore]
    public int CompletedSteps => Steps.Values.Count(s => s.IsCompleted);
    
    [JsonIgnore]
    public float Progress => TotalSteps > 0 ? (float)CompletedSteps / TotalSteps : 0f;
    
    [JsonIgnore]
    public string LastModifiedString => LastModified.ToString("yyyy-MM-dd HH:mm");

    [JsonIgnore]
    public bool IsArranged { get; private set; }
    
    public Project()
    {
        var now = DateTime.Now;
        CreatedAt = now;
        LastModified = now;
        LastStepId = 0;
        IsArranged = false;
    }
    
    public Project(string name) : this()
    {
        Name = name;
    }
    
    // 只负责序列化/反序列化自身，不管理索引
    public string ToJson(bool indented = true)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        return JsonSerializer.Serialize(this, options);
    }
    
    public static Project FromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        Project project = JsonSerializer.Deserialize<Project>(json, options);
        GD.Print($"反序列化项目时大小:{project.Steps.Count}");
        return project;
    }
    
    // 为ProjectManager提供创建摘要的方法
    public ProjectSummary ToSummary()
    {
        return new ProjectSummary
        {
            Id = Id,
            Name = Name,
            Description = Description,
            LastModified = LastModified,
            FilePath = FilePath,
            Tags = new List<string>(Tags),
            TotalSteps = TotalSteps,
            CompletedSteps = CompletedSteps
        };
    }

    public uint AddStep(ref ProjectStep step)
    {
        LastStepId++;

        step.ID = LastStepId;

        Steps.Add(step.ID.ToString(), step);
        return LastStepId;
    }

    public void Rearrange()
    {
        IsArranged = true;

        LastStepId = 0;

        Dictionary<string, ProjectStep> new_steps = new();

        foreach (var item in Steps.Values)
        {
            LastStepId++;

            ProjectStep new_step = item;
            
            new_step.ID = LastStepId;

            new_steps.Add(LastStepId.ToString(), new_step);
        }

        Steps = new_steps;
    }

    public void TryRefreshStep(ref ProjectStep step)
    {
        if(step.ID == 0)
        {
            AddStep(ref step);
            return;
        }
        if(Steps.TryAdd(step.ID.ToString(), step))
        {
            GD.Print("更新的步骤不在原项目中");
        }
        {
            Steps[step.ID.ToString()] = step;
        }
    }

    public void DeleteStep(uint _id)
    {
        string id = _id.ToString();

        Steps.Remove(id);
    }
}
