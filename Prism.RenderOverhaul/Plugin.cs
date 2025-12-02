using HarmonyLib;
using Prism.Render.Patches;
using Prism.Render.Pipeline;
using Prism.Render.Pipeline.New;
using Prism.Render.Pipeline.Old;
using System.IO;
using System.Reflection;
using VRage.Plugins;
using VRage.Render11.Common;
using VRage.Render11.GeometryStage2.Instancing;
using VRage.Render11.GeometryStage2.PreparePass;
using VRageRender;

[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]

namespace Prism.Render;

public class Plugin : IPlugin
{
    public static string? ShaderDirectory { get; private set; }

    public Plugin()
    {
#if DEV
        ShaderDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Shaders");
#endif
        RegisterTypes();
        GBufferVelocity.Init();
        Patch_MyRenderScheduler.Init();

        new Harmony(GetType().FullName).PatchAll(Assembly.GetExecutingAssembly());
    }

    public void Init(object gameInstance)
    {
    }

    private static void RegisterTypes()
    {
        MyManagers.Instances.m_instances.ChangeObjectType<MyInstance, PrismInstance>();

        MyObjectPoolManager.RegisterPool(typeof(PrismGBufferPass));
        MyObjectPoolManager.RegisterPool(typeof(PrismGBufferRenderPass));
        MyObjectPoolManager.RegisterPool(typeof(MyPreparePass<PrismColorPreparePass0, MyColorPreparePass1>));

        MyVertexInputLayout.MapComponent.Add((MyVertexInputComponentType)PrismVertexInputComponentType.SIMPLE_INSTANCE_PREVMATRIX, new MySimpleInstancePrevMatrixComponent());
    }

    public void LoadAssets(string path)
    {
        ShaderDirectory = path;
    }

    public void Update()
    {
    }

    public void Dispose()
    {
    }
}
