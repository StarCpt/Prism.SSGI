using Newtonsoft.Json;
using System;
using System.IO;
using VRage.Utils;

namespace Prism.Render.Config;

public class SSGIConfig
{
    [ConfigProperty("Enable SSGI")]
    public bool Enabled { get; set; } = true;

    [FloatConfigProperty("GI Intensity", 0, 10, 2)]
    public float GIIntensity { get; set; } = 2;

    [IntConfigProperty("Slices", 1, 32, 2)]
    public int SliceCount { get; set; } = 2;

    [IntConfigProperty("Steps", 1, 64, 16)]
    public int StepCount { get; set; } = 16;

    [FloatConfigProperty("Radius", 0, 20, 10)]
    public float Radius { get; set; } = 10;

    [FloatConfigProperty("ExpFactor", 1, 2, 1.5f)]
    public float ExpFactor { get; set; } = 1.5f;

    [FloatConfigProperty("Thickness", 0, 10, 1)]
    public float Thickness { get; set; } = 1;

    [FloatConfigProperty("Denoiser Temporal History", 0, 50, 20)]
    public float DenoiserMaxHistory { get; set; } = 20;

    [FloatConfigProperty("Denoiser Blur Radius", 0, 32, 16)]
    public float DenoiserBlurRadius { get; set; } = 16;

    private readonly string _filePath;

    public SSGIConfig(string filePath)
    {
        _filePath = filePath;
    }

    public static SSGIConfig LoadOrCreate(string filePath)
    {
        if (!File.Exists(filePath))
        {
            MyLog.Default.Info($"{typeof(SSGIConfig).FullName}: Config not found, initializing default values. path={filePath}.");
            return CreateDefaultAndSave(filePath);
        }

        try
        {
            JsonSerializer serializer = new JsonSerializer();
            using (var sr = new StreamReader(filePath))
            using (var jr = new JsonTextReader(sr))
            {
                var config = new SSGIConfig(filePath);
                serializer.Populate(jr, config);
                return config;
            }
        }
        catch (Exception e)
        {
            MyLog.Default.Info($"{typeof(SSGIConfig).FullName}: Could not load config, initializing default values. path={filePath}, {e}");
            return CreateDefaultAndSave(filePath);
        }
    }

    private static SSGIConfig CreateDefaultAndSave(string filePath)
    {
        var conf = new SSGIConfig(filePath);
        conf.Save();
        return conf;
    }

    public void Save()
    {
        try
        {
            JsonSerializer serializer = new JsonSerializer
            {
                Formatting = Formatting.Indented,
            };

            Directory.CreateDirectory(Path.GetDirectoryName(_filePath));

            using (var sw = new StreamWriter(_filePath, false))
            {
                serializer.Serialize(sw, this);
            }
        }
        catch (Exception e)
        {
            MyLog.Default.Info($"{typeof(SSGIConfig).FullName}: Could not save config. {e}");
        }
    }
}
