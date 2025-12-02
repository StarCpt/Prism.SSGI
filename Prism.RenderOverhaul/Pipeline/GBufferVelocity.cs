using HarmonyLib;
using SharpDX.DXGI;
using System.Collections.Concurrent;
using VRage;
using VRage.Render11.Common;
using VRage.Render11.RenderContext;
using VRage.Render11.Resources;

namespace Prism.Render.Pipeline;

[HarmonyPatch]
public static class GBufferVelocity
{
    private static readonly ConcurrentDictionary<MyGBuffer, IRtvTexture> _textures = [];

    public static void Init()
    {
        MyGBuffer gbuffer = MyGBuffer.Main;
        if (!_textures.ContainsKey(gbuffer))
        {
            _textures[gbuffer] = Create(gbuffer.LBuffer.Size.X, gbuffer.LBuffer.Size.Y, gbuffer.SamplesCount, gbuffer.SamplesQuality);
        }
    }

    [HarmonyPatch(typeof(MyGBuffer), nameof(MyGBuffer.Release))]
    [HarmonyPostfix]
    static void MyGBuffer_Release_Postfix(MyGBuffer __instance)
    {
        if (_textures.TryRemove(__instance, out var texture))
        {
            MyManagers.RwTextures.DisposeTex(ref texture);
        }
    }

    [HarmonyPatch(typeof(MyGBuffer), nameof(MyGBuffer.Resize))]
    [HarmonyPostfix]
    static void MyGBuffer_Resize_Postfix(MyGBuffer __instance, int width, int height, int samplesNum, int samplesQuality)
    {
        _textures[__instance] = Create(width, height, samplesNum, samplesQuality);
    }

    [HarmonyPatch(typeof(MyGBuffer), nameof(MyGBuffer.Clear))]
    [HarmonyPostfix]
    static void MyGBuffer_Clear_Postfix(MyGBuffer __instance, MyRenderContext rc)
    {
        if (MyVRage.Platform.Render.ForceClearGBuffer)
        {
            rc.ClearRtv(_textures[__instance], default);
        }
    }

    public static IRtvTexture Get(MyGBuffer gbuffer) => _textures[gbuffer];
    
    private static IRtvTexture Create(int width, int height, int samplesNum, int samplesQuality)
    {
        return MyManagers.RwTextures.CreateRtv("Prism.GBufferVelocity", width, height, Format.R16G16B16A16_Float, samplesNum, samplesQuality);
    }
}
