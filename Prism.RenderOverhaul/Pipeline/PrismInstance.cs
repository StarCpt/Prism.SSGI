using VRage.Render11.GeometryStage2.Instancing;

namespace Prism.Render.Pipeline;

public class PrismInstance : MyInstance
{
    public RowMatrix PrevRowMatrix;

    public void UpdatePrevMatrix()
    {
        PrevRowMatrix = TransformStrategy.RowMatrix;
    }
}
