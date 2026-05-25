using UnityEditor;
using UnityEngine;
using _Game.Config;

namespace _Game.Editor
{
    public static class CreateWeatherAssets
    {
        [MenuItem("Game/Create Default Weather Assets")]
        public static void Create()
        {
            string path = "Assets/_Game/Config/Weather/";
            if (!AssetDatabase.IsValidFolder(path.TrimEnd('/')))
            {
                System.IO.Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }

            CreateAsset(WeatherType.Sunny,  path);
            CreateAsset(WeatherType.Cloudy, path);
            CreateAsset(WeatherType.Rain,   path);
            CreateAsset(WeatherType.Storm,  path);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CreateWeatherAssets] 4 个天气 SO 已创建/更新");
        }

        static void CreateAsset(WeatherType type, string basePath)
        {
            string name = $"Weather_{type}.asset";
            string fullPath = basePath + name;

            var existing = AssetDatabase.LoadAssetAtPath<WeatherData>(fullPath);
            if (existing != null)
            {
                // 已存在，跳过
                return;
            }

            var data = WeatherData.CreateDefault(type);
            AssetDatabase.CreateAsset(data, fullPath);
        }
    }
}
