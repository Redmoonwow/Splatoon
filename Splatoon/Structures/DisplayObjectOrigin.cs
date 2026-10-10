namespace Splatoon.Structures;

/// <summary>
/// Describes what produced a display object: the dynamic element name or script full name (<see cref="Namespace"/>),
/// the layout name and the element name. Any of them can be null.
/// The processing loop sets <see cref="Current"/> before rendering elements, and every display object captures it on creation.
/// </summary>
public readonly record struct DisplayObjectOrigin(string Namespace, string Layout, string Element)
{
    internal static DisplayObjectOrigin Current { get; set; }
}
