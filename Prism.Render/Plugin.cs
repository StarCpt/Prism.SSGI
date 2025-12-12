using HarmonyLib;
using Prism.Common;
using Prism.Render.Config;
using Prism.Render.Gui;
using Prism.Render.Patches;
using Prism.Render.Pipeline;
using Prism.Render.Pipeline.New;
using Prism.Render.Pipeline.Old;
using Prism.Render.SSGI;
using Sandbox.Graphics.GUI;
using System.IO;
using System.Reflection;
using VRage.FileSystem;
using VRage.Input;
using VRage.Plugins;
using VRage.Render11.Common;
using VRage.Render11.GeometryStage2.Instancing;
using VRage.Render11.GeometryStage2.PreparePass;
using VRageRender;

namespace Prism.Render;

public class Plugin : IPlugin
{
    public static string? ShaderDirectory { get; private set; }
    public static SSGIConfig SSGIConfig { get; private set; } = null!;
    public static FileShaderCompiler ShaderCompiler { get; private set; }

    public static bool IsCameraLcdDrawing;
    public static bool IsTargetCameraDrawing;

    public void Init(object gameInstance)
    {
#if DEV
        ShaderDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Shaders");
#endif
        string storageDirectory = Path.Combine(MyFileSystem.UserDataPath, "Storage", "Prism");
        SSGIConfig = SSGIConfig.LoadOrCreate(Path.Combine(storageDirectory, "ssgi2.json"));
        ShaderCompiler = new FileShaderCompiler("", MyShaderCompiler.ShadersPath, Path.Combine(storageDirectory, "ShaderCache"));

        RegisterTypes();
        GBufferVelocity.Init();
        Patch_MyRenderScheduler.Init();
        SSGIPass.Init();

        new Harmony(GetType().FullName).PatchAll(Assembly.GetExecutingAssembly());
    }

    private static void RegisterTypes()
    {
        MyManagers.Instances.m_instances.ChangeObjectType<MyInstance, PrismInstance>();
        MyObjectPoolManager.m_poolsByType[typeof(MyRenderableProxy)].ChangeObjectType<MyRenderableProxy, PrismRenderableProxy>();

        MyObjectPoolManager.RegisterPool(typeof(PrismGBufferPass));
        MyObjectPoolManager.RegisterPool(typeof(PrismGBufferRenderPass));
        MyObjectPoolManager.RegisterPool(typeof(MyPreparePass<PrismColorPreparePass0, MyColorPreparePass1>));

        MyVertexInputLayout.MapComponent.Add((MyVertexInputComponentType)PrismVertexInputComponentType.SIMPLE_INSTANCE_PREVMATRIX, new MySimpleInstancePrevMatrixComponent());
    }

    public void LoadAssets(string path)
    {
        ShaderDirectory = path;
    }

    public void OpenConfigDialog() => MyGuiSandbox.AddScreen(new GuiScreenSSGIConfig(SSGIConfig));

    public void Update()
    {
#if DEV
        if (MyInput.Static.IsAnyShiftKeyPressed() && MyInput.Static.IsNewKeyPressed(MyKeys.OemPipe))
        {
            MyRender11.EnqueueUpdate(() =>
            {
                SSGIPass.ReloadShaders();
                //MyMaterialShaders.Recompile();
                //MyVertexShaders.Recompile();
                //MyPixelShaders.Recompile();
            });
        }
#endif
    }

    public void Dispose()
    {
    }
}
