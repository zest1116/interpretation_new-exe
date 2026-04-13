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

        /// <summary>
        /// 로컬 파일이 없을 때만 기본값으로 생성
        /// </summary>
        public T LoadOrCreate(T seed)
        {
            if (!File.Exists(FilePath))
            {
                Save(seed);
                return seed;
            }

            // 기존 사용자 값 읽기
            var json = File.ReadAllText(FilePath);
            var current = JsonSerializer.Deserialize<T>(json) ?? new T();

            // 시드 값으로 빈 항목만 채우기 (사용자가 변경한 값은 유지)
            MergeDefaults(source: seed, target: current);

            Save(current);
            return current;
        }

        /// <summary>
        /// source(시드)의 값을 target에 병합
        /// target이 null/empty이면 source 값으로 채움
        /// </summary>
        private static void MergeDefaults(T source, T target)
        {
            foreach (var prop in typeof(T).GetProperties())
            {
                if (!prop.CanRead || !prop.CanWrite) continue;

                var targetVal = prop.GetValue(target);
                var sourceVal = prop.GetValue(source);

                //var isEmpty = targetVal is null
                //           || (targetVal is string s && string.IsNullOrEmpty(s));

                if (sourceVal is not null)
                {
                    prop.SetValue(target, sourceVal);
                }
            }
        }
    }
}
