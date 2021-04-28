using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API;

namespace Farmlife
{
    public class EntityBehaviorFoodGrow : EntityBehavior
    {
        ITreeAttribute growTree;
        JsonObject typeAttributes;
        long callbackId;

        internal float HoursToGrow
        {
            get { return typeAttributes["hoursToGrow"].AsFloat(96); }
        }

        internal float nutritionReq
        {
            get { return typeAttributes["nutritionReq"].AsFloat(1); }
        }

        internal AssetLocation[] AdultEntityCodes
        {
            get { return AssetLocation.toLocations(typeAttributes["adultEntityCodes"].AsArray<string>(new string[0])); }
        }

        internal double TimeSpawned
        {
            get { return growTree.GetDouble("timeSpawned"); }
            set { growTree.SetDouble("timeSpawned", value); }
        }

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);

            this.typeAttributes = typeAttributes;

            growTree = entity.WatchedAttributes.GetTreeAttribute("grow");

            if (growTree == null)
            {
                entity.WatchedAttributes.SetAttribute("grow", growTree = new TreeAttribute());
                TimeSpawned = entity.World.Calendar.TotalHours;
            }

            callbackId = entity.World.RegisterCallback(CheckGrowth, 3000);
        }


        private void CheckGrowth(float dt)
        {
            if (!entity.Alive) return;

            if (entity.World.Calendar.TotalHours >= TimeSpawned + HoursToGrow && !CheckSaturation())
            {
                entity.WatchedAttributes.SetBool("babyneedfood", true);
                entity.WatchedAttributes.MarkPathDirty("babyneedfood");
            }

            if (entity.World.Calendar.TotalHours >= TimeSpawned + HoursToGrow && CheckSaturation())
            {
                AssetLocation[] entityCodes = AdultEntityCodes;
                if (entityCodes.Length == 0) return;
                AssetLocation code = entityCodes[entity.World.Rand.Next(entityCodes.Length)];

                EntityProperties adultType = entity.World.GetEntityType(code);

                if (adultType == null)
                {
                    entity.World.Logger.Error("Misconfigured entity. Entity with code '{0}' is configured (via Grow behavior) to grow into '{1}', but no such entity type was registered.", entity.Code, code);
                    return;
                }

                Cuboidf collisionBox = adultType.SpawnCollisionBox;

                // Delay adult spawning if we're colliding
                if (entity.World.CollisionTester.IsColliding(entity.World.BlockAccessor, collisionBox, entity.ServerPos.XYZ, false))
                {
                    callbackId = entity.World.RegisterCallback(CheckGrowth, 3000);
                    return;
                }

                Entity adult = entity.World.ClassRegistry.CreateEntity(adultType);

                adult.ServerPos.SetFrom(entity.ServerPos);
                adult.Pos.SetFrom(adult.ServerPos);

                entity.Die(EnumDespawnReason.Expire, null);
                entity.World.SpawnEntity(adult);

                adult.WatchedAttributes.SetInt("generation", entity.WatchedAttributes.GetInt("generation", 0));
                adult.WatchedAttributes["hunger"] = entity.WatchedAttributes.GetTreeAttribute("hunger").Clone();
            }
            else
            {
                callbackId = entity.World.RegisterCallback(CheckGrowth, 3000);
            }

            entity.World.FrameProfiler.Mark("entity-checkgrowth");
        }

        bool CheckSaturation()
        {
            //Child will only grow if they have at least 75% saturation
            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute("hunger");
            if (tree == null) return false;

            return tree.GetFloat("saturation", 0) >= (nutritionReq * (entity.Properties?.Attributes?["maxSaturation"].AsFloat(20f) ?? 20f));
        }

        public override void OnEntityDespawn(EntityDespawnReason despawn)
        {
            entity.World.UnregisterCallback(callbackId);
        }

        public EntityBehaviorFoodGrow(Entity entity) : base(entity)
        {
        }

        public override string PropertyName()
        {
            return "foodgrow";
        }
    }

}
