using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API;
using Vintagestory.GameContent;

namespace Farmlife
{
    public class EntityBehaviorFoodGrow : EntityBehavior
    {
        ITreeAttribute growTree;
        JsonObject typeAttributes;

        internal float HoursToGrow { get; set; }

        internal AssetLocation[] AdultEntityCodes
        {
            get { return AssetLocation.toLocations(typeAttributes["adultEntityCodes"].AsArray<string>(new string[0])); }
        }

        internal double TimeSpawned
        {
            get { return growTree.GetDouble("timeSpawned"); }
            set { growTree.SetDouble("timeSpawned", value); }
        }

        internal double TimeKeeper
        {
            get { return growTree.GetDouble("timeKeeper"); }
            set { growTree.SetDouble("timeKeeper", value); }
        }

        internal double Age
        {
            get { return growTree.GetDouble("hungerGrowth"); }
            set { growTree.SetDouble("hungerGrowth", value); }
        }



        public EntityBehaviorFoodGrow(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);

            this.typeAttributes = typeAttributes;
            HoursToGrow = typeAttributes["hoursToGrow"].AsFloat(96);
            

            growTree = entity.WatchedAttributes.GetTreeAttribute("grow");

            if (growTree == null)
            {
                entity.WatchedAttributes.SetAttribute("grow", growTree = new TreeAttribute());
                TimeSpawned = entity.World.Calendar.TotalHours;
                TimeKeeper = entity.World.Calendar.TotalHours;
                
                Age = 0;
            }
        }

        public override void OnGameTick(float deltaTime)
        {
            if (!entity.Alive || entity.World.Calendar.TotalHours - TimeKeeper < 1) return;
            TimeKeeper++;
            
            EntityBehaviorConsume bc = entity.GetBehavior<EntityBehaviorConsume>();

            if (bc != null && !bc.IsHungry) Age++;

            if (Age >= HoursToGrow)
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

                    return;
                }

                Entity adult = entity.World.ClassRegistry.CreateEntity(adultType);

                ITreeAttribute genes = entity.WatchedAttributes.GetTreeAttribute("genome");
                adult.WatchedAttributes.GetOrAddTreeAttribute("hunger").SetFloat("saturation", bc.CurrentSat);
                if (genes != null) adult.WatchedAttributes["genome"] = genes.Clone();

                adult.ServerPos.SetFrom(entity.ServerPos);
                adult.Pos.SetFrom(adult.ServerPos);

                entity.Die(EnumDespawnReason.Expire, null);
                entity.World.SpawnEntity(adult);

                adult.WatchedAttributes.SetInt("generation", entity.WatchedAttributes.GetInt("generation", 0));
            }
            else
            {
                if (Age >= 0.1 * HoursToGrow)
                {
                    float newAge = (float)(Age / HoursToGrow - 0.1);
                    if (newAge >= 1.01f * growTree.GetFloat("age"))
                    {
                        growTree.SetFloat("age", newAge);
                        entity.WatchedAttributes.MarkPathDirty("grow");
                    }
                }
            }

            entity.World.FrameProfiler.Mark("entity-checkgrowth");
        }


        public override string PropertyName()
        {
            return "foodgrow";
        }
    }

}
