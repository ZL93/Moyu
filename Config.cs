using Moyu.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Moyu
{
    public class Config
    {
        private static readonly Lazy<Config> _instance = new Lazy<Config>(() => new Config());
        public static Config Instance => _instance.Value;
       
        public bool ShowHelpInfo { get; set; } = true;
        public List<BookInfo> bookInfos { get; set; } = new List<BookInfo>();

        // 保存配置
        public bool SaveConfig()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.json");
                string jsonConfigStr = JsonConvert.SerializeObject(_instance.Value, Formatting.Indented);
                File.WriteAllText(configPath, jsonConfigStr);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveConfig Err\n{ex.Message}");
                return false;
            }
        }

        // 加载配置
        public bool LoadConfig()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.json");

                if (!File.Exists(configPath))
                {
                    // 如果没有配置文件，可以加载默认配置
                    return SaveConfig();  // 这里也可以选择初始化为默认配置并返回 true
                }

                string configJsonStr = File.ReadAllText(configPath);
                var deserializedConfig = JsonConvert.DeserializeObject<Config>(configJsonStr);
                if (deserializedConfig != null)
                {
                    // 使用反射自动赋值
                    foreach (var property in typeof(Config).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var value = property.GetValue(deserializedConfig);
                        property.SetValue(_instance.Value, value);
                    }
                    return true;
                }
                else 
                {
                    Console.WriteLine("LoadConfig Err");
                    return false; 
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadConfig Err\n{ex.Message}");
                return false;
            }
        }
    }
}
