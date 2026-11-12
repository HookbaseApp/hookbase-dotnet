using System.Reflection;
using System.Text;

namespace Hookbase.Models.Common;

/// <summary>
/// Backs the <c>PrintMembers</c> of records that carry a secret, so their <c>ToString()</c> (and so
/// a log line, an exception message or a debugger view) never shows it.
/// </summary>
internal static class SecretRedaction
{
    internal const string Redacted = "[redacted]";

    /// <summary>
    /// Appends <c>Name = value</c> for each public instance property <paramref name="declaringType"/>
    /// itself declares, as the compiler-generated <c>PrintMembers</c> does, but writes a set member
    /// named in <paramref name="secrets"/> as <c>[redacted]</c>. A null one still prints empty, so
    /// <c>ToString()</c> shows whether a secret came back. Pass in <paramref name="printed"/> what the
    /// base record's <c>PrintMembers</c> returned; the result is whether anything has been printed.
    /// </summary>
    internal static bool PrintMembers(StringBuilder builder, object instance, Type declaringType, bool printed, params string[] secrets)
    {
        foreach (var property in declaringType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (property.GetIndexParameters().Length > 0 || property.GetMethod is not { IsPublic: true })
                continue;

            if (printed)
                builder.Append(", ");
            printed = true;

            builder.Append(property.Name).Append(" = ");
            var value = property.GetValue(instance);
            if (value != null && Array.IndexOf(secrets, property.Name) >= 0)
                builder.Append(Redacted);
            else
                builder.Append(value);
        }

        return printed;
    }
}
