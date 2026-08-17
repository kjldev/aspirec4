using System.Collections.Immutable;
using Aspire.Hosting.AspireC4.SourceGenerators.Models;
using Microsoft.CodeAnalysis;

namespace Aspire.Hosting.AspireC4.SourceGenerators.Helpers;

partial class SourceGenHelper
{
	static ImmutableDictionary<RegistryTypeDefinition, ImmutableArray<RegistrySpecDefinition>> CollectRegistryMembers(
		INamedTypeSymbol targetSymbol,
		ISourceGenLogger? logger,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		TypeValueObject registryType = new(targetSymbol);

		Dictionary<RegistryTypeDefinition, List<RegistrySpecDefinition>> registryMembers = new()
		{
			{ TypeLibrary.RegistryTypeValues.Tag, [] },
			{ TypeLibrary.RegistryTypeValues.ElementKind, [] },
			{ TypeLibrary.RegistryTypeValues.RelationshipKind, [] },
			{ TypeLibrary.RegistryTypeValues.Group, [] },
			{ TypeLibrary.RegistryTypeValues.MetadataKey, [] },
		};

		logger?.Info($"Collecting registry members for: {registryType}");

		foreach (var member in targetSymbol.GetTypeMembers())
		{
			if (member.TypeKind == TypeKind.Class)
			{
				if (ScanNestedType(logger, registryMembers, member))
					logger?.Info($"Found class: {member.Name}", 1);
			}
		}

		foreach (var member in targetSymbol.GetMembers())
		{
			if (member is IFieldSymbol fieldSymbol && IsValidField(fieldSymbol))
			{
				if (ScanField(logger, registryMembers, fieldSymbol))
					logger?.Info($"Found field: {member.Name}", 1);
			}
		}

		return registryMembers
			.Where(m => m.Value.Count > 0)
			.ToImmutableDictionary(k => k.Key, v => v.Value.ToImmutableArray());
	}

	static bool IsValidField(IFieldSymbol fieldSymbol) =>
		fieldSymbol.IsConst && fieldSymbol.Type.SpecialType == SpecialType.System_String;

	static bool ScanField(
		ISourceGenLogger? logger,
		Dictionary<RegistryTypeDefinition, List<RegistrySpecDefinition>> registryMembers,
		IFieldSymbol fieldSymbol
	)
	{
		var knownTypeAttribute = KnownTypesAttributeData.FromAttributeData(fieldSymbol);
		if (!knownTypeAttribute.Exists)
			return false;

		var registrationType = TypeLibrary.RegistryTypeValues.GetByName(knownTypeAttribute.Type);
		if (registrationType == RegistryTypeDefinition.Empty)
			return false;

		logger?.Info($"Field is a {registrationType.Name}", 2);
		registryMembers[registrationType].Add(new((string)fieldSymbol.ConstantValue!, fieldSymbol.Locations));

		return true;
	}

	static bool ScanNestedType(
		ISourceGenLogger? logger,
		Dictionary<RegistryTypeDefinition, List<RegistrySpecDefinition>> registryMembers,
		INamedTypeSymbol nestedType
	)
	{
		var registrationType = TypeLibrary.RegistryTypeValues.GetByName(nestedType.Name);
		return registrationType == RegistryTypeDefinition.Empty
			? false
			: ScanNestedClassFields(logger, registryMembers[registrationType], nestedType);
	}

	static bool ScanNestedClassFields(
		ISourceGenLogger? logger,
		List<RegistrySpecDefinition> specDefinitions,
		INamedTypeSymbol nestedType
	)
	{
		logger?.Info($"Scanning nested class fields for: {nestedType.Name}", 2);
		var foundField = false;
		foreach (var member in nestedType.GetMembers())
		{
			if (member is IFieldSymbol fieldSymbol && IsValidField(fieldSymbol))
			{
				specDefinitions.Add(new((string)fieldSymbol.ConstantValue!, fieldSymbol.Locations));
				logger?.Info($"Found field: {member.Name}", 3);
				foundField = true;
			}
		}

		return foundField;
	}
}
