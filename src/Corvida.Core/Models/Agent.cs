namespace Corvida.Models;

public class Agent
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public string Color { get; set; } = "#4C6EF5";
    public string? AvatarDataUri { get; set; }
}
