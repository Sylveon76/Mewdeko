using System.Text.Json;

namespace Mewdeko.Common.JsonSettings;

/// <summary>
///     Provides a naming policy that maintains property order.
/// </summary>
public class OrderedResolver : JsonNamingPolicy
{
    /// <summary>
    ///     Converts the specified property name to its serialized form.
    /// </summary>
    /// <param name="name">The property name to convert.</param>
    /// <returns>The name to be used in serialized JSON.</returns>
    /// <remarks>
    ///     System.Text.Json automatically maintains property order based on source code,
    ///     so this resolver simply returns the original name.
    /// </remarks>
    public override string ConvertName(string name)
    {
        return name;
    }
}

/// <summary>
///     Provides a naming policy that converts property names to lowercase.
/// </summary>
public class LowercaseNamingPolicy : JsonNamingPolicy
{
    /// <summary>
    ///     Converts the specified property name to its lowercase form.
    /// </summary>
    /// <param name="name">The property name to convert.</param>
    /// <returns>The lowercase version of the property name.</returns>
    public override string ConvertName(string name)
    {
        return name.ToLower();
    }
}