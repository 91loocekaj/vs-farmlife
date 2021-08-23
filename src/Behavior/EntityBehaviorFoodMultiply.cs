using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using System;
using Vintagestory.API;
using Vintagestory.API.Config;
using System.Text;
using Vintagestory.GameContent;

namespace Farmlife
{
    public class EntityBehaviorFoodMultiply : EntityBehaviorMultiply
    {
        long callback;
        JsonObject typeAttributes;

        internal float PregnancyDays
        {
            get { return typeAttributes["pregnancyDays"].AsFloat(3f); }
        }

        internal AssetLocation SpawnEntityCode
        {
            get { return new AssetLocation(typeAttributes["spawnEntityCode"].AsString("")); }
        }

        internal string RequiresNearbyEntityCode
        {
            get { return typeAttributes["requiresNearbyEntityCode"].AsString(""); }
        }

        internal float RequiresNearbyEntityRange
        {
            get { return typeAttributes["requiresNearbyEntityRange"].AsFloat(5); }
        }

        public new float SpawnQuantityMin
        {
            get { return typeAttributes["spawnQuantityMin"].AsFloat(1); }
        }
        public new float SpawnQuantityMax
        {
            get { return typeAttributes["spawnQuantityMax"].AsFloat(2); }
        }

        internal ITreeAttribute Sperm
        {
            get { return multiplyTree.GetTreeAttribute("sperm"); }
            set { if (value != null) { multiplyTree["sperm"] = value; entity.WatchedAttributes.MarkPathDirty("multiply"); } else multiplyTree.RemoveAttribute("sperm"); }
        }



        public override bool ShouldEat
        {
            get
            {
                return true;
            }
        }

        public EntityBehaviorFoodMultiply(Entity entity) : base(entity)
        {

        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            this.typeAttributes = attributes;

            MultiplyCooldownDaysMin = attributes["multiplyCooldownDaysMin"].AsFloat(6);
            MultiplyCooldownDaysMax = attributes["multiplyCooldownDaysMax"].AsFloat(12);
            PortionsEatenForMultiply = attributes["portionsEatenForMultiply"].AsFloat(3);


            multiplyTree = entity.WatchedAttributes.GetTreeAttribute("multiply");

            if (entity.World.Side == EnumAppSide.Server)
            {
                if (multiplyTree == null)
                {
                    entity.WatchedAttributes.SetAttribute("multiply", multiplyTree = new TreeAttribute());

                    double daysNow = entity.World.Calendar.TotalHours / 24f;
                    TotalDaysCooldownUntil = daysNow + (MultiplyCooldownDaysMin + entity.World.Rand.NextDouble() * (MultiplyCooldownDaysMax - MultiplyCooldownDaysMin));
                    TotalDaysLastBirth = -9999;
                }

                callback = entity.World.RegisterCallback(CheckMultiply, 3000);
            }
        }

        private void CheckMultiply(float deltaTime)
        {
            EntityBehaviorConsume bc = entity.GetBehavior<EntityBehaviorConsume>();
            if (!entity.Alive || bc == null || multiplyTree.GetInt("birthingEvents") >= (entity.Properties.Attributes?["maxBirths"].AsInt(1) ?? 1)) return;

            callback = entity.World.RegisterCallback(CheckMultiply, 3000);

            if (entity.World.Calendar == null) return;

            double daysNow = entity.World.Calendar.TotalDays;

            if (!IsPregnant)
            {
                if (TryGetPregnant())
                {
                    IsPregnant = true;
                    TotalDaysPregnancyStart = daysNow;
                }

                return;
            }

            if (bc.SatPerc < 0.5f * entity.Stats.GetBlended("vulenrability"))
            {
                //Miscarriage due to lack of food
                TotalDaysLastBirth = daysNow;
                TotalDaysCooldownUntil = daysNow + (MultiplyCooldownDaysMin + entity.World.Rand.NextDouble() * (MultiplyCooldownDaysMax - MultiplyCooldownDaysMin));
                IsPregnant = false;
                entity.WatchedAttributes.MarkPathDirty("multiply");
                entity.Stats.Remove("hungerrate", "pregnant");
                return;
            }

            EntityBehaviorHealth bh = entity.GetBehavior<EntityBehaviorHealth>();

            if (bh != null && (bh.Health / bh.MaxHealth) < 0.25f * entity.Stats.GetBlended("vulenrability"))
            {
                //Miscarriage due to poor health
                TotalDaysLastBirth = daysNow;
                TotalDaysCooldownUntil = daysNow + (MultiplyCooldownDaysMin + entity.World.Rand.NextDouble() * (MultiplyCooldownDaysMax - MultiplyCooldownDaysMin));
                IsPregnant = false;
                entity.WatchedAttributes.MarkPathDirty("multiply");
                entity.Stats.Remove("hungerrate", "pregnant");
                return;
            }

            if (daysNow - TotalDaysPregnancyStart > PregnancyDays)
            {
                Random rand = entity.World.Rand;

                float q = SpawnQuantityMin + (float)rand.NextDouble() * (SpawnQuantityMax - SpawnQuantityMin);
                TotalDaysLastBirth = daysNow;
                TotalDaysCooldownUntil = daysNow + (MultiplyCooldownDaysMin + rand.NextDouble() * (MultiplyCooldownDaysMax - MultiplyCooldownDaysMin));
                IsPregnant = false;
                entity.WatchedAttributes.MarkPathDirty("multiply");
                entity.Stats.Remove("hungerrate", "pregnant");
                multiplyTree.SetInt("birthingEvents", multiplyTree.GetInt("birthingEvents") + 1);
                EntityProperties childType = entity.World.GetEntityType(SpawnEntityCode);

                int generation = entity.WatchedAttributes.GetInt("generation", 0);
                ITreeAttribute Egg = entity.WatchedAttributes.GetTreeAttribute("genome");

                int[] dadGenes = null;
                int[] momGenes = null;

                if (Sperm != null && Egg != null)
                {
                    dadGenes = (Sperm["sequence"] as IntArrayAttribute).value;
                    momGenes = (Egg["sequence"] as IntArrayAttribute).value;
                }

                string command = entity.WatchedAttributes.GetTreeAttribute("command")?.GetString("masterUID");

                while (q > 1 || rand.NextDouble() < q)
                {
                    q--;
                    Entity childEntity = entity.World.ClassRegistry.CreateEntity(childType);

                    if (momGenes != null && dadGenes != null)
                    {
                        ITreeAttribute childGenome = childEntity.WatchedAttributes.GetOrAddTreeAttribute("genome");
                        int[] childSequence = new int[Math.Min(momGenes.Length, dadGenes.Length)];

                        for (int i = 0; i < childSequence.Length; i++)
                        {
                            if (momGenes[i] != dadGenes[i]) childSequence[i] = entity.World.Rand.Next(Math.Min(momGenes[i], dadGenes[i]), Math.Max(momGenes[i], dadGenes[i]) + 1); else childSequence[i] = momGenes[i];
                        }

                        childGenome["sequence"] = new IntArrayAttribute(childSequence);
                    }

                    if (command != null) childEntity.WatchedAttributes.GetOrAddTreeAttribute("command").SetString("masterUID", command);

                    childEntity.ServerPos.SetFrom(entity.ServerPos);
                    childEntity.ServerPos.Motion.X += (rand.NextDouble() - 0.5f) / 20f;
                    childEntity.ServerPos.Motion.Z += (rand.NextDouble() - 0.5f) / 20f;

                    childEntity.Pos.SetFrom(childEntity.ServerPos);
                    entity.World.SpawnEntity(childEntity);
                    childEntity.Attributes.SetString("origin", "reproduction");
                    if (generation > 0 || entity.WatchedAttributes.GetBool("playerFed")) childEntity.WatchedAttributes.SetInt("generation", generation + 1);
                }

            }

            entity.World.FrameProfiler.Mark("entity-multiply");
        }

        private bool TryGetPregnant()
        {
            if (entity.World.Rand.NextDouble() > 0.03) return false;
            if (TotalDaysCooldownUntil > entity.World.Calendar.TotalDays) return false;

            EntityBehaviorConsume bc = entity.GetBehavior<EntityBehaviorConsume>();
            if (bc == null) return false;

            if (!bc.IsHungry)
            {
                Entity maleentity = null;
                if (RequiresNearbyEntityCode != null && (maleentity = GetRequiredEntityNearby()) == null) return false;

                entity.Stats.Set("hungerrate", "pregnant", 0.5f, true);

                if (maleentity != null)
                {
                    Sperm = entity.WatchedAttributes.GetTreeAttribute("genome");
                }

                IsPregnant = true;
                TotalDaysPregnancyStart = entity.World.Calendar.TotalDays;
                entity.WatchedAttributes.MarkPathDirty("multiply");

                return true;
            }

            return false;
        }

        private Entity GetRequiredEntityNearby()
        {
            if (RequiresNearbyEntityCode == null) return null;

            return entity.World.GetNearestEntity(entity.ServerPos.XYZ, RequiresNearbyEntityRange, RequiresNearbyEntityRange, (e) =>
            {
                if (e.WildCardMatch(new AssetLocation(RequiresNearbyEntityCode)))
                {
                    EntityBehaviorConsume bc = e.GetBehavior<EntityBehaviorConsume>();

                    if (!e.WatchedAttributes.GetBool("doesEat") || bc?.IsHungry == false)
                    {
                        return true;
                    }
                }

                return false;

            });
        }

        public override string PropertyName()
        {
            return "foodmultiply";
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            if (!entity.Alive) return;
            multiplyTree = entity.WatchedAttributes.GetTreeAttribute("multiply");
            if (multiplyTree == null) return;

            if (multiplyTree.GetInt("birthingEvents") >= (entity.Properties.Attributes?["maxBirths"].AsInt(1) ?? 1))
            {
                infotext.AppendLine(Lang.Get("farmlife:infertile"));
                return;
            }

            ITreeAttribute food = entity.WatchedAttributes.GetTreeAttribute("hunger");
            ITreeAttribute health = entity.WatchedAttributes.GetTreeAttribute("health");

            if (IsPregnant)
            {
                infotext.AppendLine(Lang.Get("Is pregnant"));

                if ((food != null && food.GetFloat("saturation") <= (entity.Properties?.Attributes?["maxSaturation"].AsFloat(20f) ?? 20f) * ((0.5f * entity.Stats.GetBlended("vulenrability")) + 0.1f))
                    || (health != null && health.GetFloat("currenthealth") <= health.GetFloat("maxhealth") * ((0.25f * entity.Stats.GetBlended("vulenrability")) + 0.1f)))
                {
                    infotext.AppendLine(Lang.Get("farmlife:miscarriage"));
                }
            }
            else
            {
                double daysLeft = TotalDaysCooldownUntil - entity.World.Calendar.TotalDays;

                if (daysLeft > 0)
                {
                    if (daysLeft > 3)
                    {
                        infotext.AppendLine(Lang.Get("Several days left before ready to mate"));
                    }
                    else
                    {
                        infotext.AppendLine(Lang.Get("Less than 3 days before ready to mate"));
                    }

                }
                else
                {
                    infotext.AppendLine(Lang.Get("Ready to mate"));
                }
            }
        }
    }
}
