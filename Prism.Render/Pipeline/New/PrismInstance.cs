using HarmonyLib;
using System.Runtime.CompilerServices;
using VRage.Library.Collections;
using VRage.Render11.GeometryStage2.Instancing;
using VRage.Render11.GeometryStage2.PreparePass;

namespace Prism.Render.Pipeline.New;

public class PrismInstance : MyInstance
{
    [HarmonyPatch]
    static class Patches
    {
        [HarmonyPatch(typeof(MyPreparePass<PrismColorPreparePass0, MyColorPreparePass1>), nameof(MyPreparePass<PrismColorPreparePass0, MyColorPreparePass1>.Perform))]
        [HarmonyPostfix]
        static unsafe void MyPreparePass_PrismColorPreparePass0_MyColorPreparePass1_Perform_Postfix(MyList<MyInstance> ___m_visibleInstances)
        {
            if (Plugin.IsCameraLcdDrawing || Plugin.IsTargetCameraDrawing)
                return;

            // all MyInstance objects's real type *should* be PrismInstance but this is still sketchy
            PrismInstance[] visibleInstances = Unsafe.As<PrismInstance[]>(___m_visibleInstances.GetInternalArray());
            int instanceCount = ___m_visibleInstances.Count;
            for (int i = 0; i < instanceCount; i++)
            {
                visibleInstances[i].UpdatePrevMatrix();
            }
        }
    }

    public RowMatrix PrevRowMatrix;

    public void UpdatePrevMatrix()
    {
        // TODO: ensure this is only called once per frame
        // currently there's only 1 gbuffer pass per frame but who knows if it'll change in the future
        PrevRowMatrix = TransformStrategy.RowMatrix;
    }
}
