namespace Chronith.Application.Interfaces;

public interface ITemplateRenderer
{
    string Render(string template, Dictionary<string, string> context);
}
