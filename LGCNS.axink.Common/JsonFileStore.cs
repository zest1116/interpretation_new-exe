using System.IO;
using System.Text.Json;

namespace LGCNS.axink.Common
{
    public sealed class JsonFileStore<T> where T : class, new()
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true
        };

        public string FilePath { get; }

        public JsonFileStore(string appName, string fileName)
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Consts.APP_COMPANY,
                appName
            );
            Directory.CreateDirectory(dir);

            FilePath = Path.Combine(dir, fileName);
        }

        public T LoadOrCreate()
        {
            if (!File.Exists(FilePath))
            {
                var created = new T();
                Save(created);
                return created;
            }

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<T>(json) ?? new T();
        }

        public void Save(T data)
        {
            var json = JsonSerializer.Serialize(data, Options);
            File.WriteAllText(FilePath, json);
        }
    }
}
