namespace Aspire.Hosting.LikeC4;

/// <summary>Configuration options for the LikeC4 diagram generation.</summary>
public sealed class LikeC4DiagramOptions
{
    /// <summary>Title shown in the generated LikeC4 view. Defaults to "Architecture".</summary>
    public string Title { get; set; } = "Architecture";

    /// <summary>
    /// Directory where the generated <c>.c4</c> file is written.
    /// Defaults to <c>./likec4</c> relative to the AppHost working directory.
    /// </summary>
    public string OutputDirectory { get; set; } = "./likec4";

    /// <summary>
    /// Name of the generated <c>.c4</c> file (without extension).
    /// Defaults to "model".
    /// </summary>
    public string FileName { get; set; } = "model";
}
