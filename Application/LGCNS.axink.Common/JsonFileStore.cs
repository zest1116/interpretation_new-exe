using System.IO;
using System.Linq.Expressions;
using System.Reflection;
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
        /// source(시드)의 값을 target의 "비어있는" 항목에만 병합
        /// 사용자가 설정한 값(non-default)은 보존
        /// </summary>
        private static void MergeDefaults(T source, T target)
        {
            var blank = new T();  // 비교 기준점

            foreach (var prop in typeof(T).GetProperties())
            {
                if (!prop.CanRead || !prop.CanWrite) continue;

                var targetVal = prop.GetValue(target);
                var sourceVal = prop.GetValue(source);
                var blankVal = prop.GetValue(blank);

                // target이 "기본값(blank)"과 같을 때만 source로 채움
                bool targetIsBlank = Equals(targetVal, blankVal)
                                  || (targetVal is string s && string.IsNullOrEmpty(s));

                if (targetIsBlank && sourceVal is not null)
                {
                    prop.SetValue(target, sourceVal);
                }
            }
        }

        public void UpdateProperty<TProp>(
            Expression<Func<T, TProp>> selector,
            TProp value)
        {
            if (selector.Body is not MemberExpression memberExpr)
                throw new ArgumentException("Property selector만 지원됩니다.");

            if (memberExpr.Member is not PropertyInfo prop)
                throw new ArgumentException("Property만 지정해야 합니다.");

            if (!prop.CanWrite)
                throw new InvalidOperationException($"속성 {prop.Name}은(는) 쓰기 불가입니다.");

            // 현재 데이터 로드
            var current = LoadOrCreate();

            // 값 설정
            prop.SetValue(current, value);

            // 저장
            Save(current);
        }
    }
}
