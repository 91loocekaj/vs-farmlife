using Vintagestory.GameContent;

namespace Farmlife
{
    public class BlockEntityCompostBin : BlockEntityGenericTypedContainer
    {
        public override float GetPerishRate()
        {
            return 500f;
        }
    }
}
