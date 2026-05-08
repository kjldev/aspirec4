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
	public static LikeC4Model Build(IReadOnlyList<IResource> resources)
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
			elements.Add(BuildElement(resource));
			CollectRelationships(resource, visibleResources, visibleByName, relationships, visitedRelationships);
		}

		return new LikeC4Model
		{
			Elements = elements,
			Relationships = relationships,
		};
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

	static LikeC4Element BuildElement(IResource resource)
	{
		var details = resource.Annotations.OfType<LikeC4NodeDetailsAnnotation>().LastOrDefault();

		var label = details?.Label ?? resource.Name;
		var technology = details?.Technology ?? InferTechnology(resource);
		var description = details?.Description;
		var kind = InferKind(resource);
		var parentName = (resource as IResourceWithParent)?.Parent?.Name;

		return new LikeC4Element
		{
			Name = resource.Name,
			Label = label,
			Kind = kind,
			Technology = technology,
			Description = description,
			ParentName = parentName,
		};
	}

	static string InferKind(IResource resource) => resource switch
	{
		ProjectResource => LikeC4ElementKind.Component,
		ContainerResource => LikeC4ElementKind.Container,
		// IResourceWithConnectionString before ExecutableResource to catch DB-like resources
		IResourceWithConnectionString => LikeC4ElementKind.Database,
		ExecutableResource => LikeC4ElementKind.Executable,
		_ => LikeC4ElementKind.System,
	};

	static string? InferTechnology(IResource resource) => resource switch
	{
		ProjectResource => ".NET",
		ContainerResource container => container.Annotations.OfType<ContainerImageAnnotation>()
			.LastOrDefault()?.Image,
		_ => null,
	};

	static void CollectRelationships(
		IResource resource,
		HashSet<IResource> visibleResources,
		Dictionary<string, IResource> visibleByName,
		List<LikeC4Relationship> relationships,
		HashSet<(string, string)> visited)
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

			relationships.Add(new LikeC4Relationship
			{
				SourceName = resource.Name,
				TargetName = effectiveTarget.Name,
				Label = annotation.Type is not ("Reference" or WaitForRelationshipType)
					? annotation.Type
					: null,
			});
		}
	}
}
