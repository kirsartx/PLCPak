namespace PLCPak.Core.Models;

public sealed class PublishTemplateCatalog
{
    public int Version { get; set; } = 1;
    public string DefaultTemplateId { get; set; } = "telegram-default";
    public List<PublishTemplate> Templates { get; set; } = [];
}

public sealed class PublishTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = "telegram";
    public string Body { get; set; } = string.Empty;
}

public sealed class PublishCopyResult
{
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? ExportPath { get; set; }
    public List<string> MissingFields { get; set; } = [];
}