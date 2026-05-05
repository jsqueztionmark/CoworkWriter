namespace CoworkWriter.Core.Scrivener;

public record ParseResult(ScrivenerProject? Project, string? Error)
{
    public bool Success => Project is not null;

    public static ParseResult Ok(ScrivenerProject project) => new(project, null);
    public static ParseResult Fail(string error) => new(null, error);
}
