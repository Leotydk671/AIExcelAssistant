using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

[Serializable]
public class ProjectSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; }
    
    [JsonPropertyName("modified")]
    public DateTime LastModified { get; set; }
    
    [JsonPropertyName("file")]
    public string FilePath { get; set; }
    
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; }
    
    [JsonPropertyName("totalSteps")]
    public int TotalSteps { get; set; }
    
    [JsonPropertyName("completedSteps")]
    public int CompletedSteps { get; set; }
    
    [JsonIgnore]
    public float Progress => TotalSteps > 0 ? (float)CompletedSteps / TotalSteps : 0f;
    
    [JsonIgnore]
    public string LastModifiedString => LastModified.ToString("yyyy-MM-dd HH:mm");
    
    [JsonIgnore]
    public string ProgressString => $"{CompletedSteps}/{TotalSteps}";
}