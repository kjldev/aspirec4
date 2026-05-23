namespace Aspire.Hosting.AspireC4.ApplicationModel;

/// <summary>
/// A one-shot container resource that runs <c>likec4 --version</c> and exits.
/// Its sole purpose is to discover the exact version of the <c>latest</c> image so the
/// lifecycle hook can determine the correct HMR port mode before the main
/// <see cref="LikeC4ServerResource"/> starts.
/// </summary>
/// <remarks>
/// Hidden from the dashboard, excluded from the manifest, and excluded from the LikeC4 diagram.
/// Only registered when the tag is <c>"latest"</c> and
/// <see cref="AspireC4DiagramOptions.CheckLatestImageVersion"/> is <see langword="true"/>.
/// The <see cref="LikeC4ServerResource"/> is configured to wait for this resource's completion
/// via <c>WaitForCompletion</c> so the version is resolved before <c>likec4 start</c> args are built.
/// </remarks>
sealed class LikeC4VersionProbeResource(string name) : ContainerResource(name) { }
