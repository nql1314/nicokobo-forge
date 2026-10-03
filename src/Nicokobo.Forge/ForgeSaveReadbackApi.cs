using System.Text;
using System.Text.Json;

namespace Nicokobo.Forge;

public enum ForgeSaveReadStatus { Readable, Missing, TooLarge, ChangedDuringRead, Malformed, Unavailable, Invalid }

/// <summary>Owns a detached JSON document. Dispose after all Root element reads.
/// Readable proves a bounded file read and syntax only, not a matching run,
/// committed transaction, durable flush, or successful native reload.</summary>
public sealed class ForgeSaveReadResult : IDisposable
{
    private JsonDocument? _document;
    internal ForgeSaveReadResult(ForgeSaveReadStatus status, int byteCount = 0,
        string? reason = null, JsonDocument? document = null)
    { Status = status; ByteCount = byteCount; Reason = reason; _document = document; }
    public ForgeSaveReadStatus Status { get; }
    public int ByteCount { get; }
    public string? Reason { get; }
    public JsonElement Root => _document?.RootElement ??
        throw new InvalidOperationException("No readable save document, or result has been disposed");
    public void Dispose() => Interlocked.Exchange(ref _document, null)?.Dispose();
}

/// <summary>Read-only ES3 text support, independent of Unity and game singletons.
/// The caller validates native type, run/slot identity, owned state and receipts.</summary>
public static class ForgeSaveReadbackApi
{
    public const int DefaultMaxBytes = ForgeNumbers.SaveReadback.MaxBytes;
    public const int DefaultMaxDepth = ForgeNumbers.SaveReadback.MaxDepth;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static ForgeSaveReadResult ReadFile(string path, int maxBytes = DefaultMaxBytes,
        int maxDepth = DefaultMaxDepth)
    {
        if (string.IsNullOrWhiteSpace(path) || maxBytes <= 0 || maxDepth <= 0)
            return new(ForgeSaveReadStatus.Invalid, reason: "Invalid path or read limits");
        try
        {
            var before = new FileInfo(path);
            if (!before.Exists) return new(ForgeSaveReadStatus.Missing);
            long length = before.Length;
            var modified = before.LastWriteTimeUtc;
            if (length > maxBytes) return new(ForgeSaveReadStatus.TooLarge);
            byte[] bytes;
            // Do not read concurrently with a writer or permit replacement on
            // platforms honoring sharing modes. No waiting/retry or native save call.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > maxBytes) return new(ForgeSaveReadStatus.TooLarge);
                if (stream.Length != length) return new(ForgeSaveReadStatus.ChangedDuringRead);
                bytes = new byte[checked((int)length)];
                int read = 0;
                while (read < bytes.Length)
                {
                    int count = stream.Read(bytes, read, bytes.Length - read);
                    if (count == 0) return new(ForgeSaveReadStatus.ChangedDuringRead, read);
                    read += count;
                }
                if (stream.ReadByte() != -1) return new(ForgeSaveReadStatus.ChangedDuringRead, read);
                var after = new FileInfo(path);
                if (!after.Exists || after.Length != bytes.Length || after.LastWriteTimeUtc != modified)
                    return new(ForgeSaveReadStatus.ChangedDuringRead, read);
            }
            return ReadBytes(bytes, maxBytes, maxDepth);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new(ForgeSaveReadStatus.Unavailable, reason: ex.GetType().Name); }
    }

    public static ForgeSaveReadResult ReadBytes(ReadOnlyMemory<byte> bytes,
        int maxBytes = DefaultMaxBytes, int maxDepth = DefaultMaxDepth)
    {
        if (maxBytes <= 0 || maxDepth <= 0) return new(ForgeSaveReadStatus.Invalid);
        if (bytes.Length > maxBytes) return new(ForgeSaveReadStatus.TooLarge, bytes.Length);
        try
        {
            var document = Parse(StrictUtf8.GetString(bytes.Span).TrimStart('\uFEFF'), maxDepth);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
                return new(ForgeSaveReadStatus.Readable, bytes.Length, document: document);
            document.Dispose();
            return new(ForgeSaveReadStatus.Malformed, bytes.Length, "Save root is not an object");
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException)
        { return new(ForgeSaveReadStatus.Malformed, bytes.Length, ex.GetType().Name); }
    }

    /// <summary>Parse an ES3 object or an embedded inventory JSON string. The
    /// caller owns/disposes the document. Duplicate property checks are explicit.</summary>
    public static JsonDocument Parse(string source, int maxDepth = DefaultMaxDepth)
    {
        if (maxDepth <= 0) throw new ArgumentOutOfRangeException(nameof(maxDepth));
        return JsonDocument.Parse(Normalize(source), new JsonDocumentOptions { MaxDepth = maxDepth });
    }

    /// <summary>False for a non-object or duplicate key; true with present=false
    /// for an absent property. Unlike GetProperty, never chooses the last duplicate.</summary>
    public static bool TryGetUniqueProperty(JsonElement parent, string name,
        out JsonElement value, out bool present)
    {
        value = default; present = false;
        if (parent.ValueKind != JsonValueKind.Object) return false;
        foreach (var property in parent.EnumerateObject())
        {
            if (!property.NameEquals(name)) continue;
            if (present) { value = default; return false; }
            value = property.Value; present = true;
        }
        return true;
    }

    public static JsonElement RequireProperty(JsonElement parent, string name) =>
        TryGetUniqueProperty(parent, name, out var value, out bool present) && present
            ? value : throw new InvalidDataException("Missing, duplicate or invalid property: " + name);

    /// <summary>Normalize ES3 numeric dictionary keys, raw string controls and
    /// legacy backslashes only. Do not use this on ordinary mod-owned JSON.</summary>
    public static string Normalize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new StringBuilder(source.Length);
        bool inString = false;
        for (int i = 0; i < source.Length;)
        {
            char c = source[i];
            if (inString)
            {
                if (c == '\\' && i + 1 < source.Length)
                {
                    char next = source[i + 1];
                    bool valid = "\"\\/bfnrt".Contains(next) || next == 'u' && i + 5 < source.Length &&
                        IsHex(source[i + 2]) && IsHex(source[i + 3]) && IsHex(source[i + 4]) && IsHex(source[i + 5]);
                    if (valid) { result.Append(c).Append(next); i += 2; continue; }
                    result.Append("\\\\"); i++; continue;
                }
                if (c < 0x20) result.Append("\\u").Append(((int)c).ToString("x4"));
                else result.Append(c);
                if (c == '"') inString = false;
                i++; continue;
            }
            if (c == '"') { inString = true; result.Append(c); i++; continue; }
            result.Append(c); i++;
            if (c is not ('{' or ',')) continue;
            int start = i;
            while (i < source.Length && char.IsWhiteSpace(source[i])) i++;
            int digitStart = i;
            if (i < source.Length && source[i] == '-') i++;
            int digits = i;
            while (i < source.Length && source[i] is >= '0' and <= '9') i++;
            if (i == digits) { result.Append(source, start, i - start); continue; }
            int digitEnd = i;
            while (i < source.Length && char.IsWhiteSpace(source[i])) i++;
            if (i < source.Length && source[i] == ':')
            {
                result.Append(source, start, digitStart - start);
                result.Append('"').Append(source, digitStart, digitEnd - digitStart).Append('"');
                result.Append(source, digitEnd, i - digitEnd).Append(':'); i++;
            }
            else result.Append(source, start, i - start);
        }
        return result.ToString();
    }

    private static bool IsHex(char value) => value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
}
