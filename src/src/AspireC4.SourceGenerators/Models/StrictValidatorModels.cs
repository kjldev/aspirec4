using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Aspire.Hosting.AspireC4.SourceGenerators.Models;

readonly record struct StrictValidatorGenerationModel(
	GenerationContext Context,
	EquatableArray<GeneratorResult<LikeC4RegistryTarget>> Targets
)
{
	public bool IsDisabled { get; init; } = false;

	public bool IsStrict { get; init; } = false;

	public DSLDefinitions DSLDefinition { get; init; }

	public EquatableArray<CallSiteInfo> TagCallSites { get; init; }

	public EquatableArray<CallSiteInfo> KindCallSites { get; init; }

	public EquatableArray<CallSiteInfo> GroupCallSites { get; init; }

	public EquatableArray<CallSiteInfo> MetadataCallSites { get; init; }
}

readonly record struct LikeC4RegistryTarget(
	SeverityDefinition DefaultSeverity,
	ImmutableDictionary<RegistryTypeDefinition, EquatableArray<RegistrySpecDefinition>> Specifications
);

readonly record struct RegistrySpecDefinition(
	string SpecName,
	SeverityDefinition Severity,
	ImmutableArray<Location> Locations
)
{
	public override int GetHashCode() => SpecName.GetHashCode();
}
