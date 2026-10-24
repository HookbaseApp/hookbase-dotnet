using System.Text;

namespace Hookbase.Json;

/// <summary>Query-string names for enum filter values.</summary>
internal static class WireNames
{
    /// <summary><c>NoRoutes</c> becomes <c>no_routes</c>, <c>SchemaFailed</c> becomes <c>schema_failed</c>.</summary>
    public static string SnakeCase(Enum value)
    {
        var name = value.ToString();
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
