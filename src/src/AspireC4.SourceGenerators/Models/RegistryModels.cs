using System.Collections.Immutable;
using Aspire.Hosting.AspireC4.SourceGenerators.Helpers;

namespace Aspire.Hosting.AspireC4.SourceGenerators.Models;

[Generate(TypeLibrary.LikeC4RegistryAttributeFullname)]
readonly partial record struct LikeC4RegistryAttributeData(
	[Property(DefaultValue = TypeLibrary.LikeC4RegistryAttributeFullname + ".Inherit", IsEnum = true)] string Strict
);

[Generate(TypeLibrary.KnownTypeAttributeFullname)]
readonly partial record struct KnownTypesAttributeData(
	[Property(TypeLibrary.LikeC4RegistryTypeFullname + ".Inherit", IsEnum = true)] string Type
);

readonly record struct RegistryTypeDefinition(string Name, int Value, ImmutableArray<string> ValidTypeNames)
{
	public string FullName => TypeLibrary.LikeC4RegistryTypeFullname + "." + Name;

	public static readonly RegistryTypeDefinition Empty;
}

readonly record struct SeverityDefinition(string Name, int Value);
