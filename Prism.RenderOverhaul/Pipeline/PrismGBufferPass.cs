using VRage.Render11.Resources;
using VRageRender;

namespace Prism.Render.Pipeline;

public class PrismGBufferPass : MyRenderingPass
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public MyGBuffer GBuffer;
    public IRtvTexture VelocityBuffer;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
