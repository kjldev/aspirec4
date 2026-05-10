using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Builds a <see cref="LikeC4Model"/> from the Aspire application model resources.
/// </summary>
public static class LikeC4ModelBuilder
{
	// Aspire's well-known relationship type for wait-for dependencies — these are infrastructure
	// concerns and should not appear as architecture relationships in the diagram.
	const string WaitForRelationshipType = "WaitFor";

	/// <summary>Builds a <see cref="LikeC4Model"/> from a list of Aspire resources.</summary>
	/// <param name="resources">All resources in the Aspire app model.</param>
	/// <param name="resourceStates">
	/// Optional mapping of resource name → current <see cref="LikeC4ResourceState"/>.
	/// When provided, the corresponding diagram element is coloured to reflect the live state.
	/// </param>
	public static LikeC4Model Build(
		IReadOnlyList<IResource> resources,
		IReadOnlyDictionary<string, LikeC4ResourceState>? resourceStates = null,
		bool autoIconsEnabled = true
	)
	{
		ArgumentNullException.ThrowIfNull(resources);

		var visibleResources = BuildVisibleSet(resources);

		// Build a name → resource lookup so we can resolve "surrogate" resources.
		// When an Azure resource (e.g. AzureRedisCacheResource) is replaced by a local
		// container via RunAsContainer(), the original Azure resource is hidden and a
		// ContainerResource with the same name becomes the visible counterpart.
		// WithReference() still annotates with the hidden Azure resource, so we need
		// this lookup to resolve the visible surrogate by name.
		var visibleByName = visibleResources
			.GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

		var elements = new List<LikeC4Element>(visibleResources.Count);
		var relationships = new List<LikeC4Relationship>();
		var visitedRelationships = new HashSet<(string Source, string Target)>();

		foreach (var resource in visibleResources)
		{
			var state =
				resourceStates is not null && resourceStates.TryGetValue(resource.Name, out var s)
					? s
					: LikeC4ResourceState.Unknown;

			elements.Add(BuildElement(resource, state, autoIconsEnabled));
			CollectRelationships(resource, visibleResources, visibleByName, relationships, visitedRelationships);
		}

		return new LikeC4Model { Elements = elements, Relationships = relationships };
	}

	/// <summary>
	/// Returns the names of all resources that would be included in the diagram
	/// (i.e. not excluded or hidden). Useful for filtering state-change notifications
	/// to only relevant resources.
	/// </summary>
	public static IReadOnlySet<string> GetVisibleResourceNames(IReadOnlyList<IResource> resources)
	{
		ArgumentNullException.ThrowIfNull(resources);

		return BuildVisibleSet(resources).Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	static HashSet<IResource> BuildVisibleSet(IReadOnlyList<IResource> resources)
	{
		var visible = new HashSet<IResource>(resources.Count);

		foreach (var resource in resources)
		{
			if (ShouldExclude(resource))
			{
				continue;
			}

			visible.Add(resource);
		}

		return visible;
	}

	static bool ShouldExclude(IResource resource)
	{
		// Explicitly excluded by the user.
		if (resource.Annotations.OfType<ExcludeFromLikeC4Annotation>().Any())
		{
			return true;
		}

		// Hidden resources (e.g. internal Aspire infrastructure) should not appear.
		var snapshot = resource.Annotations.OfType<ResourceSnapshotAnnotation>().FirstOrDefault();
		return snapshot?.InitialSnapshot.IsHidden == true;
	}

	static LikeC4Element BuildElement(IResource resource, LikeC4ResourceState state, bool autoIconsEnabled)
	{
		var details = resource.Annotations.OfType<LikeC4NodeDetailsAnnotation>().LastOrDefault();
		var inferredTechnology = InferTechnology(resource);

		var label = details?.Label ?? resource.Name;
		var technology = details?.Technology ?? inferredTechnology;
		var description = details?.Description;
		var summary = details?.Summary;
		var icon = ResolveIcon(resource, details, inferredTechnology, autoIconsEnabled);
		var kind = details?.Kind ?? InferKind(resource);
		var parentName = (resource as IResourceWithParent)?.Parent?.Name;
		var group = resource.Annotations.OfType<LikeC4GroupAnnotation>().LastOrDefault()?.GroupName;

		return new()
		{
			Name = resource.Name,
			Label = label,
			Kind = kind,
			Technology = technology,
			Description = description,
			Summary = summary,
			Icon = icon,
			ParentName = parentName,
			State = state,
			Tags = details?.Tags ?? [],
			Links = details?.Links ?? [],
			Metadata = details?.Metadata ?? [],
			Group = group,
		};
	}

	static string InferKind(IResource resource) =>
		resource switch
		{
			ProjectResource => LikeC4ElementKind.Component,
			ContainerResource => LikeC4ElementKind.Container,
			// IResourceWithConnectionString before ExecutableResource to catch DB-like resources
			IResourceWithConnectionString => LikeC4ElementKind.Database,
			ExecutableResource => LikeC4ElementKind.Executable,
			_ => LikeC4ElementKind.System,
		};

	static string? InferTechnology(IResource resource) =>
		resource switch
		{
			ProjectResource => ".NET",
			ContainerResource container => container
				.Annotations.OfType<ContainerImageAnnotation>()
				.LastOrDefault()
				?.Image,
			_ => null,
		};

	static string? ResolveIcon(
		IResource resource,
		LikeC4NodeDetailsAnnotation? details,
		string? inferredTechnology,
		bool autoIconsEnabled
	)
	{
		if (!string.IsNullOrWhiteSpace(details?.Icon))
		{
			return details.Icon;
		}

		if (!(details?.AutoIconEnabled ?? autoIconsEnabled))
		{
			return null;
		}

		return LikeC4IconMatcher.TryInferIcon([
			details?.Technology,
			inferredTechnology,
			resource.Annotations.OfType<ResourceSnapshotAnnotation>().LastOrDefault()?.InitialSnapshot.ResourceType,
			resource.GetType().FullName,
			resource.GetType().Name,
			resource.Name,
		]);
	}

	static void CollectRelationships(
		IResource resource,
		HashSet<IResource> visibleResources,
		Dictionary<string, IResource> visibleByName,
		List<LikeC4Relationship> relationships,
		HashSet<(string, string)> visited
	)
	{
		foreach (var annotation in resource.Annotations.OfType<ResourceRelationshipAnnotation>())
		{
			// Skip infrastructure-only wait-for dependencies.
			if (annotation.Type == WaitForRelationshipType)
			{
				continue;
			}

			// Resolve the effective target: use the annotated resource directly if it is
			// visible, otherwise look for a visible surrogate with the same name.
			// The surrogate pattern arises with Azure resources run via RunAsContainer():
			// the original Azure resource is hidden and replaced by a ContainerResource
			// with the same name, but WithReference() still annotates with the Azure resource.
			var effectiveTarget = visibleResources.Contains(annotation.Resource)
				? annotation.Resource
				: visibleByName.GetValueOrDefault(annotation.Resource.Name);

			if (effectiveTarget is null)
			{
				continue;
			}

			var key = (resource.Name, effectiveTarget.Name);
			if (!visited.Add(key))
			{
				continue;
			}

			// Look for a LikeC4-specific override. Keyed by target name; also checks the resolved
			// effective target for the Azure RunAsContainer() surrogate pattern.
			var details = resource
				.Annotations.OfType<LikeC4RelationshipDetailsAnnotation>()
				.LastOrDefault(a =>
					string.Equals(a.TargetName, annotation.Resource.Name, StringComparison.OrdinalIgnoreCase)
					|| string.Equals(a.TargetName, effectiveTarget.Name, StringComparison.OrdinalIgnoreCase)
				);

			// Fall back to the Aspire relationship type as label when not a generic 'Reference'
			// or infrastructure 'WaitFor' annotation and no explicit label was provided.
			var inferredLabel = annotation.Type is not ("Reference" or WaitForRelationshipType)
				? annotation.Type
				: null;

			relationships.Add(
				new LikeC4Relationship
				{
					SourceName = resource.Name,
					TargetName = effectiveTarget.Name,
					Label = details?.Label ?? inferredLabel,
					Technology = details?.Technology,
					Description = details?.Description,
					Kind = details?.Kind,
					Tags = details?.Tags ?? [],
					Links = details?.Links ?? [],
					Metadata = details?.Metadata ?? new Dictionary<string, string>(),
				}
			);
		}

		// Second pass: emit diagram-only relationships declared with WithLikeC4Reference that are
		// not backed by a ResourceRelationshipAnnotation (i.e. no WithReference was called).
		// The visited set from the first pass ensures we never duplicate a relationship.
		foreach (var details in resource.Annotations.OfType<LikeC4RelationshipDetailsAnnotation>())
		{
			var effectiveTarget = visibleByName.GetValueOrDefault(details.TargetName);
			if (effectiveTarget is null)
			{
				continue;
			}

			var key = (resource.Name, effectiveTarget.Name);
			if (!visited.Add(key))
			{
				continue;
			}

			relationships.Add(
				new LikeC4Relationship
				{
					SourceName = resource.Name,
					TargetName = effectiveTarget.Name,
					Label = details.Label,
					Technology = details.Technology,
					Description = details.Description,
					Kind = details.Kind,
					Tags = details.Tags,
					Links = details.Links,
					Metadata = details.Metadata,
				}
			);
		}
	}
}
