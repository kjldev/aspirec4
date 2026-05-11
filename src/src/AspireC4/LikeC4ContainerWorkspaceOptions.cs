namespace Aspire.Hosting.AspireC4;

sealed class LikeC4ContainerWorkspaceOptions
{
	public string VolumeName { get; set; } = "";

	public string ContainerImageReference { get; set; } = "";

	public string ContainerRuntimeExecutable { get; set; } = "";

	public LikeC4HMRPortMode HMRPortMode { get; set; } = LikeC4HMRPortMode.FixedPort;

	/// <summary>
	/// When true, the host-side TCP relay listens on port <see cref="LikeC4ServerResource.DefaultContainerUpdatePort"/>
	/// and bridges incoming HMR connections to the dynamically-allocated Docker host port.
	/// Always true for FixedPort images; also true on Windows to avoid Hyper-V port reservation issues.
	/// </summary>
	public bool UseHMRRelay { get; set; }

	/// <summary>
	/// Absolute paths of additional DSL files that are covered by a container bind mount.
	/// The lifecycle hook skips volume-syncing these files to avoid duplicate definitions
	/// in the container (the bind mount already makes the files accessible).
	/// </summary>
#pragma warning disable IDE0028 // Simplify collection initialization
	public HashSet<string> BindMountedSourceFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
#pragma warning restore IDE0028 // Simplify collection initialization

	/// <summary>
	/// Maps absolute host folder path → workspace-root-relative path (e.g. <c>"ext/abc12345"</c>),
	/// where the workspace root is <see cref="LikeC4ServerResource.WorkspacePath"/> (<c>/data</c>).
	/// The actual bind-mount target is therefore <c>/data/ext/abc12345</c>, which is intentionally
	/// <em>outside</em> the named volume that is mounted at
	/// <see cref="LikeC4ServerResource.GeneratedPath"/> (<c>/data/output</c>).
	/// Used when generating the container-side <c>likec4.config.json</c>; paths in that file are
	/// prefixed with <c>"../"</c> because the config lives at <c>/data/output/likec4.config.json</c>.
	/// </summary>
#pragma warning disable IDE0028
	public Dictionary<string, string> BindMountedFolderTargets { get; } = new(StringComparer.OrdinalIgnoreCase);
#pragma warning restore IDE0028

	/// <summary>
	/// Maps image alias key (e.g. <c>"@icons"</c>) → workspace-root-relative path
	/// (e.g. <c>"img/def67890"</c>) for each alias registered via
	/// <see cref="IAspireC4Builder.WithImageAliasFolder"/>.
	/// The actual bind-mount target is <c>/data/img/def67890</c>, outside the named volume.
	/// Config generation prefixes these values with <c>"../"</c> because the config file is at
	/// <c>/data/output/likec4.config.json</c>.
	/// </summary>
#pragma warning disable IDE0028
	public Dictionary<string, string> BindMountedImageAliasFolderTargets { get; } =
		new(StringComparer.OrdinalIgnoreCase);
#pragma warning restore IDE0028
}
