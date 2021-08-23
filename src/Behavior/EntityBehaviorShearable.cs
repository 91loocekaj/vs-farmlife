using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Config;
using System.Text;

namespace Farmlife
{
    public class EntityBehaviorShearable : EntityBehavior
    {
        ITreeAttribute shearTree;
        double shearableAt;
        int minWool;
        int maxWool;
        AssetLocation wool;
        int minGen;

        internal double TimeKeeper
        {
            get { return shearTree.GetDouble("timeKeeper"); }
            set { shearTree.SetDouble("timeKeeper", value); entity.WatchedAttributes.MarkPathDirty("shear"); }
        }

        internal double Growth
        {
            get { return shearTree.GetDouble("growth"); }
            set { shearTree.SetDouble("growth", value); entity.WatchedAttributes.MarkPathDirty("shear"); }
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            shearableAt = attributes["shearableAt"].AsInt(216);
            minWool = attributes["minQuantity"].AsInt(8);
            maxWool = attributes["maxQuantity"].AsInt(16);
            minGen = attributes["minGen"].AsInt(3);
            wool = new AssetLocation(attributes["woolItem"].AsString("farmlife:woolfibers"));

            if (shearTree == null)
            {
                entity.WatchedAttributes.SetAttribute("shear", shearTree = new TreeAttribute());
                TimeKeeper = entity.World.Calendar.TotalHours;

                Growth = 0;
            }
        }

        public override void OnGameTick(float deltaTime)
        {
            if (entity.WatchedAttributes.GetInt("generation") < minGen) return;
            if (!entity.Alive || entity.World.Calendar.TotalHours - TimeKeeper < 1) return;
            TimeKeeper++;

            EntityBehaviorConsume bc = entity.GetBehavior<EntityBehaviorConsume>();

            if (bc?.IsHungry == false) Growth++;

            base.OnGameTick(deltaTime);
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            //System.Diagnostics.Debug.WriteLine(Growth);
            if (itemslot.Itemstack?.Collectible.Tool == EnumTool.Shears && Growth >= shearableAt && entity.Api.Side == EnumAppSide.Server)
            {
                Growth = 0;
                ItemStack woolStack = new ItemStack(entity.World.GetItem(wool), entity.World.Rand.Next(minWool, maxWool + 1));
                entity.World.SpawnItemEntity(woolStack, entity.SidedPos.XYZ);
                if (entity.World.Rand.NextDouble() > 0.5) entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Entity, SourceEntity = byEntity, Type = EnumDamageType.SlashingAttack }, 1);
            }
            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            if (!entity.Alive || entity.WatchedAttributes.GetInt("generation") < minGen) return;
            shearTree = entity.WatchedAttributes.GetTreeAttribute("shear");
            if (shearTree == null || Growth < shearableAt) return;

            infotext.AppendLine(Lang.Get("farmlife:shearable"));

            base.GetInfoText(infotext);
        }

        public EntityBehaviorShearable(Entity entity) : base(entity)
        {
        }

        public override string PropertyName()
        {
            return "shearable";
        }
    }
}
