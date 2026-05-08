namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Runtime state of an Aspire resource, mapped from <see cref="Aspire.Hosting.ApplicationModel.KnownResourceStates"/>
/// and used to colour the corresponding LikeC4 diagram element.
/// </summary>
public enum LikeC4ResourceState
{
	/// <summary>No state information available yet.</summary>
	Unknown,

	/// <summary>Resource is starting up or waiting for dependencies.</summary>
	Starting,

	/// <summary>Resource is running and healthy.</summary>
	Running,

	/// <summary>Resource is in the process of stopping.</summary>
	Stopping,

	/// <summary>Resource exited cleanly (exit code 0).</summary>
	Exited,

	/// <summary>Resource exited with a non-zero exit code.</summary>
	Failed,

	/// <summary>Resource failed to start or reported a runtime error.</summary>
	Error,
}
