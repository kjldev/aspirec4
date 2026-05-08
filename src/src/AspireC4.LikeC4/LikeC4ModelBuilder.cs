using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.LikeC4;

/// <summary>
/// Builds a <see cref="LikeC4Model"/> from the Aspire application model resources.
/// </summary>
public static class LikeC4ModelBuilder
{
    // Aspire's well-known relationship type for wait-for dependencies — these are infrastructure
    // concerns and should not appear as architecture relationships in the diagram.
    private const string WaitForRelationshipType = "WaitFor";

    /// <summary>Builds a <see cref="LikeC4Model"/> from a list of Aspire resources.</summary>
    public static LikeC4Model Build(IReadOnlyList<IResource> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var visibleResources = BuildVisibleSet(resources);

        var elements = new List<LikeC4Element>(visibleResources.Count);
        var relationships = new List<LikeC4Relationship>();
        var visitedRelationships = new HashSet<(string Source, string Target)>();

        foreach (var resource in visibleResources)
        {
            elements.Add(BuildElement(resource));
            CollectRelationships(resource, visibleResources, relationships, visitedRelationships);
        }

        return new LikeC4Model
        {
            Elements = elements,
            Relationships = relationships,
        };
    }

    private static HashSet<IResource> BuildVisibleSet(IReadOnlyList<IResource> resources)
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

    private static bool ShouldExclude(IResource resource)
    {
        // Explicitly excluded by the user.
        if (resource.Annotations.OfType<ExcludeFromLikeC4Annotation>().Any())
        {
            return true;
        }

        // Hidden resources (e.g. internal Aspire infrastructure) should not appear.
        var snapshot = resource.Annotations.OfType<ResourceSnapshotAnnotation>().FirstOrDefault();
        if (snapshot?.InitialSnapshot.IsHidden == true)
        {
            return true;
        }

        return false;
    }

    private static LikeC4Element BuildElement(IResource resource)
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

    private static string InferKind(IResource resource) => resource switch
    {
        ProjectResource => LikeC4ElementKind.Component,
        ContainerResource => LikeC4ElementKind.Container,
        // IResourceWithConnectionString before ExecutableResource to catch DB-like resources
        IResourceWithConnectionString => LikeC4ElementKind.Database,
        ExecutableResource => LikeC4ElementKind.Executable,
        _ => LikeC4ElementKind.System,
    };

    private static string? InferTechnology(IResource resource) => resource switch
    {
        ProjectResource => ".NET",
        ContainerResource container => container.Annotations.OfType<ContainerImageAnnotation>()
            .LastOrDefault()?.Image,
        _ => null,
    };

    private static void CollectRelationships(
        IResource resource,
        HashSet<IResource> visibleResources,
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

            // Skip if the target is hidden/excluded.
            if (!visibleResources.Contains(annotation.Resource))
            {
                continue;
            }

            var key = (resource.Name, annotation.Resource.Name);
            if (!visited.Add(key))
            {
                continue;
            }

            relationships.Add(new LikeC4Relationship
            {
                SourceName = resource.Name,
                TargetName = annotation.Resource.Name,
                Label = annotation.Type is not ("Reference" or WaitForRelationshipType)
                    ? annotation.Type
                    : null,
            });
        }
    }
}
