using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Farmlife
{
    public class BlockEntityBehaviorFoodSource : BlockEntityBehavior, IAnimalFoodSource
    {
        bool eaten = false;
        string food = "Generic";
        double xOffset, yOffset, zOffset;
        float saturation;
        POIRegistry registrySystem;

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            if (properties != null)
            {
                food = properties["foodType"].AsString("Generic");
                xOffset = properties["xOffset"].AsFloat(0.5f);
                yOffset = properties["yOffset"].AsFloat(0.5f);
                zOffset = properties["zOffset"].AsFloat(0.5f);
                saturation = properties["saturation"].AsFloat(1f);
            }

            registrySystem = api.ModLoader.GetModSystem<POIRegistry>();
            
            registrySystem?.AddPOI(this);
        }

        public BlockEntityBehaviorFoodSource(BlockEntity blockentity) : base(blockentity)
        {
        }

        public Vec3d Position => Blockentity.Pos.ToVec3d().Add(xOffset, yOffset, zOffset);

        public string Type => "food";

        public float ConsumeOnePortion()
        {
            Blockentity.Api.World.BlockAccessor.SetBlock(0, Blockentity.Pos);
            eaten = true;
            return saturation;
        }

        public bool IsSuitableFor(Entity entity)
        {
            if (eaten) return false;
            string[] diet = entity.Properties.Attributes?["blockDiet"]?.AsArray<string>();
            if (diet == null) return false;
            
            return diet.Contains(food);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            registrySystem?.RemovePOI(this);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            registrySystem?.RemovePOI(this);
        }

    }

}
