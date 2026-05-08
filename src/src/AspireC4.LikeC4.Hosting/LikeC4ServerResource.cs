using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.LikeC4;

/// <summary>
/// An Aspire resource that runs the LikeC4 live server (<c>npx likec4 serve</c>)
/// as a sidecar, providing a hot-reloading interactive architecture diagram.
/// </summary>
public sealed class LikeC4ServerResource : ExecutableResource
{
    public const string HttpEndpointName = "http";
    internal const int DefaultPort = 5156;

    internal LikeC4ServerResource(string name, string workingDirectory)
        : base(name, "node", workingDirectory)
    {
    }
}
