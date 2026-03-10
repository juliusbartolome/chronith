namespace Chronith.Application.Interfaces;

public interface ITemplateRenderer
{
    string Render(string template, IReadOnlyDictionary<string, string> context);
}
