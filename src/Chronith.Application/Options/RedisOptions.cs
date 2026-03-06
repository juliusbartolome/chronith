namespace Chronith.Application.Options;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public bool Enabled { get; init; } = false;
    public string ConnectionString { get; init; } = "localhost:6379";
}
