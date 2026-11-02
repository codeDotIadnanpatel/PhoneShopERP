using System.Reflection;
using Xunit;

namespace PhoneShopErp.Architecture.Tests;

/// <summary>
/// Executable enforcement of the modular-monolith dependency rules described in
/// <c>docs/01-architecture/ARCHITECTURE_OVERVIEW.md</c> and
/// <c>docs/01-architecture/MODULAR_BOUNDARIES.md</c>.
///
/// The module families do not exist yet (Phase 00 scaffolding only), so several
/// assertions are intentionally vacuous today. They become active the moment a
/// module assembly appears, which is the point: a boundary violation must fail the
/// build instead of being discovered in review.
/// </summary>
public sealed class ModuleBoundaryTests
{
    /// <summary>
    /// Approved module names. Must stay byte-for-byte equivalent (after
    /// normalisation) to the table in MODULAR_BOUNDARIES.md; a test below proves it.
    /// </summary>
    private static readonly string[] ApprovedModuleNames =
    [
        "Tenancy",
        "Identity & Access",
        "Catalog & Pricing",
        "Inventory",
        "Procurement",
        "Sales",
        "Payments & Cash",
        "Trade-in",
        "Repairs",
        "Warranty",
        "Accounting",
        "CRM",
        "Workforce",
        "SaaS Platform",
        "Integrations",
    ];

    /// <summary>
    /// Project references the API host is permitted to hold. Empty by design:
    /// Phase 00 scaffolding must not wire in any module before its architecture
    /// gate is approved. Add an entry only together with the approving ADR/gate
    /// reference, otherwise the review intent is lost.
    /// </summary>
    private static readonly string[] AllowedApiProjectReferences = [];

    private static readonly string[] ModuleLayerNames = ["Domain", "Application", "Infrastructure", "Contracts"];

    [Fact]
    public void ApprovedModuleList_MatchesModularBoundariesDocument()
    {
        var document = Path.Combine(FindRepositoryRoot(), "docs", "01-architecture", "MODULAR_BOUNDARIES.md");
        Assert.True(File.Exists(document), $"Expected modular-boundaries document at '{document}'.");

        var documented = File.ReadLines(document)
            .Where(line => line.StartsWith("| ", StringComparison.Ordinal))
            .Select(line => line.Split('|')[1].Trim())
            .Where(cell => cell.Length > 0 && !string.Equals(cell, "Module", StringComparison.OrdinalIgnoreCase))
            .Select(Normalize)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var expected = ApprovedModuleNames.Select(Normalize).OrderBy(name => name, StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, documented);
    }

    [Fact]
    public void ApiHost_OnlyReferencesApprovedProjectAssemblies()
    {
        var apiAssembly = typeof(Program).Assembly;
        var projectReferences = apiAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null && name.StartsWith("PhoneShopErp.", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(AllowedApiProjectReferences, projectReferences);
    }

    [Fact]
    public void ModuleAssemblies_UseApprovedModuleNamesAndLayers()
    {
        var approved = ApprovedModuleNames.Select(Normalize).ToHashSet(StringComparer.Ordinal);

        foreach (var assembly in PhoneShopErpAssemblies())
        {
            if (ParseModuleIdentity(assembly.GetName().Name) is not { } identity)
            {
                continue;
            }

            Assert.True(
                approved.Contains(identity.Owner),
                $"Assembly '{assembly.GetName().Name}' uses module name '{identity.Owner}', which is not listed in MODULAR_BOUNDARIES.md. " +
                "Add it to the approved module table and architecture review before creating the assembly.");

            Assert.True(
                ModuleLayerNames.Contains(identity.Layer, StringComparer.Ordinal),
                $"Assembly '{assembly.GetName().Name}' must end with one of: {string.Join(", ", ModuleLayerNames)}.");
        }
    }

    [Fact]
    public void ModuleAssemblies_DoNotReferenceAnotherModulesInternals()
    {
        foreach (var assembly in PhoneShopErpAssemblies())
        {
            if (ParseModuleIdentity(assembly.GetName().Name) is not { } source)
            {
                continue;
            }

            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (ParseModuleIdentity(reference.Name) is not { } target ||
                    string.Equals(target.Owner, source.Owner, StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.True(
                    string.Equals(target.Layer, "Contracts", StringComparison.Ordinal),
                    $"Module '{assembly.GetName().Name}' references '{reference.Name}' directly. " +
                    "Cross-module communication must go through the target module's Contracts assembly, " +
                    "a documented interface or a domain/application event.");
            }
        }
    }

    [Fact]
    public void DomainAssemblies_DoNotReferenceDatabaseOrProviderLibraries()
    {
        foreach (var assembly in PhoneShopErpAssemblies())
        {
            var name = assembly.GetName().Name;
            if (name is null || !name.EndsWith(".Domain", StringComparison.Ordinal))
            {
                continue;
            }

            var violations = assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(reference => reference is not null && IsInfrastructureOrProvider(reference))
                .OrderBy(reference => reference, StringComparer.Ordinal)
                .ToArray();

            Assert.True(
                violations.Length == 0,
                $"Domain assembly '{name}' must not depend on infrastructure or provider SDKs, but references: {string.Join(", ", violations)}.");
        }
    }

    private static bool IsInfrastructureOrProvider(string assemblyName) =>
        assemblyName.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
        assemblyName.StartsWith("Npgsql", StringComparison.Ordinal) ||
        assemblyName.StartsWith("Microsoft.Data.Sqlite", StringComparison.Ordinal) ||
        assemblyName.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
        assemblyName.StartsWith("Dapper", StringComparison.Ordinal);

    /// <summary>
    /// Parses <c>PhoneShopErp.Modules.&lt;Owner&gt;.&lt;Layer&gt;</c> (or
    /// <c>PhoneShopErp.Modules.&lt;Owner&gt;.Contracts</c>) assembly identities.
    /// Returns <see langword="null"/> for non-module assemblies.
    /// </summary>
    private static (string Owner, string Layer)? ParseModuleIdentity(string? assemblyName)
    {
        if (assemblyName is null)
        {
            return null;
        }

        var segments = assemblyName.Split('.');
        if (segments.Length < 4 ||
            !string.Equals(segments[0], "PhoneShopErp", StringComparison.Ordinal) ||
            !string.Equals(segments[1], "Modules", StringComparison.Ordinal))
        {
            return null;
        }

        return (Normalize(segments[2]), segments[^1]);
    }

    private static IEnumerable<Assembly> PhoneShopErpAssemblies()
    {
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "PhoneShopErp.*.dll"))
        {
            var assemblyName = AssemblyName.GetAssemblyName(path);
            yield return Assembly.Load(assemblyName);
        }
    }

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (AGENTS.md) above '{AppContext.BaseDirectory}'.");
    }
}
