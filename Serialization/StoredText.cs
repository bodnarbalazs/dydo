namespace DynaDocs.Serialization;

using System.Text;

/// <summary>
/// A file's text exactly as stored, a BOM kept as U+FEFF, and the encoding its bytes were in, so a
/// rewrite goes back in that encoding (<see cref="JsonText.Encode"/>).
/// </summary>
internal sealed record StoredText(string Text, Encoding Encoding);
