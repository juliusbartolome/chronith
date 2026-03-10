namespace Chronith.Application.Options;

public sealed class CspOptions
{
    public const string SectionName = "Security:Csp";

    public string DefaultSrc { get; set; } = "'self'";
    public string ScriptSrc { get; set; } = "'self'";
    public string StyleSrc { get; set; } = "'self' 'unsafe-inline'";
    public string ImgSrc { get; set; } = "'self' data: https:";
    public string ConnectSrc { get; set; } = "'self'";
    public string FrameAncestors { get; set; } = "'none'";
}
