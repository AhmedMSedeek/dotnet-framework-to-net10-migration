using System.Text.Json;

namespace eShopLegacy.Utilities
{
    /// <summary>
    /// Was BinaryFormatter-based. BinaryFormatter is insecure and its implementation was removed in .NET 9,
    /// so it now serializes JSON. Property names are kept as declared (PascalCase), matching the old API output.
    /// </summary>
    public class Serializing
    {
        private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = null };

        public Stream SerializeJson<T>(T input)
        {
            var stream = new MemoryStream();
            JsonSerializer.Serialize(stream, input, Options);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        public T DeserializeJson<T>(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            return JsonSerializer.Deserialize<T>(stream, Options);
        }
    }
}
