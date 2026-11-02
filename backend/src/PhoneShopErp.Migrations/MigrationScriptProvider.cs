using System.Security.Cryptography;
using System.Text;
using DbUp.Engine;

namespace PhoneShopErp.Migrations;

/// <summary>
/// Loads reviewed SQL scripts from a migration root, ordered by their
/// repository-relative path. The path is the stable script identity used in the
/// journal, so moving a file is a reviewed change and renaming one is never
/// silently treated as a new migration of the same name.
/// </summary>
public static class MigrationScriptProvider
{
    public static IReadOnlyList<SqlScript> Load(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Migration directory '{root}' does not exist.");
        }

        return Directory
            .EnumerateFiles(root, "*.sql", SearchOption.AllDirectories)
            .Select(path => new
            {
                FullPath = path,
                Name = Path.GetRelativePath(root, path).Replace('\\', '/'),
            })
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .Select(item => new SqlScript(item.Name, File.ReadAllText(item.FullPath)))
            .ToArray();
    }

    public static string Checksum(SqlScript script)
    {
        ArgumentNullException.ThrowIfNull(script);

        // Normalise line endings so a Windows checkout and a Linux checkout of the
        // same reviewed file produce the same checksum.
        var normalised = script.Contents.Replace("\r\n", "\n", StringComparison.Ordinal);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalised)));
    }
}
