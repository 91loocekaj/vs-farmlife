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
    public class EntityBehaviorFoodMultiply : EntityBehavior
    {
        ITreeAttribute multiplyTree;
        JsonObject attributes;
        long callbackId;

        internal float PregnancyDays
        {
            get { return attributes["pregnancyDays"].AsFloat(3f); }
        }

        internal AssetLocation SpawnEntityCode
        {
            get { return new AssetLocation(attributes["spawnEntityCode"].AsString("")); }
        }

        internal string RequiresNearbyEntityCode
        {
            get { return attributes["requiresNearbyEntityCode"].AsString(""); }
        }

        internal float RequiresNearbyEntityRange
        {
            get { return attributes["requiresNearbyEntityRange"].AsFloat(5); }
        }

        /*internal int GrowthCapQuantity
        {
            get { return attributes["growthCapQuantity"].AsInt(10); }
        }
        internal float GrowthCapRange
        {
            get { return attributes["growthCapRange"].AsFloat(10); }
        }
        internal AssetLocation[] GrowthCapEntityCodes
        {
            get { return AssetLocation.toLocations(attributes["growthCapEntityCodes"].AsStringArray(new string[0])); }
        }*/

        //What saturation she needs to get pregnant
        public float reproduceSaturation
        {
            get { return attributes["reproduceSaturation"].AsFloat(0.5f); }
        }

        //What health the mother will lose the baby
        public float healthMiscarriage
        {
            get { return attributes["healthMiscarriage"].AsFloat(0.25f); }
        }

        //What health the mother will lose the baby
        public float foodMiscarriage
        {
            get { return attributes["foodMiscarriage"].AsFloat(0.25f); }
        }

        public float forTwoRate
        {
            get { return attributes["forTwoRate"].AsFloat(0.5f); }
        }

        public double MultiplyCooldownDaysMin
        {
            get { return attributes["multiplyCooldownDaysMin"].AsFloat(6); }
        }

        public double MultiplyCooldownDaysMax
        {
            get { return attributes["multiplyCooldownDaysMax"].AsFloat(12); }
        }

        /*public float PortionsEatenForMultiply
        {
            get { return attributes["portionsEatenForMultiply"].AsFloat(3); }
        }*/

        public float SpawnQuantityMin
        {
            get { return attributes["spawnQuantityMin"].AsFloat(1); }
        }
        public float SpawnQuantityMax
        {
            get { return attributes["spawnQuantityMax"].AsFloat(2); }
        }


        public double TotalDaysLastBirth
        {
            get { return multiplyTree.GetDouble("totalDaysLastBirth"); }
            set { multiplyTree.SetDouble("totalDaysLastBirth", value); }
        }

        public double TotalDaysPregnancyStart
        {
            get { return multiplyTree.GetDouble("totalDaysPregnancyStart"); }
            set { multiplyTree.SetDouble("totalDaysPregnancyStart", value); }
        }

        public double TotalDaysCooldownUntil
        {
            get { return multiplyTree.GetDouble("totalDaysCooldownUntil"); }
            set { multiplyTree.SetDouble("totalDaysCooldownUntil", value); }
        }

        public bool IsPregnant
        {
            get { return multiplyTree.GetBool("isPregnant"); }
            set { multiplyTree.SetBool("isPregnant", value); }
        }


        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            this.attributes = attributes;




            multiplyTree = entity.WatchedAttributes.GetTreeAttribute("multiply");

            if (entity.World.Side == EnumAppSide.Server)
            {
                if (multiplyTree == null)
                {
                    entity.WatchedAttributes.SetAttribute("multiply", multiplyTree = new TreeAttribute());
                    TotalDaysLastBirth = -9999;

                    double daysNow = entity.World.Calendar.TotalHours / 24f;
                    TotalDaysCooldownUntil = daysNow + (MultiplyCooldownDaysMin + entity.World.Rand.NextDouble() * (MultiplyCooldownDaysMax - MultiplyCooldownDaysMin));
                }

                callbackId = entity.World.RegisterCallback(CheckMultiply, 3000);
            }
        }


        private void CheckMultiply(float dt)
        {
            if (!entity.Alive) return;

            callbackId = entity.World.RegisterCallback(CheckMultiply, 3000);

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

            //System.Diagnostics.Debug.WriteLine(GetSaturation() < (foodMiscarriage * (entity.Properties?.Attributes?["maxSaturation"].AsFloat(20f) ?? 20f)));
            if (GetSaturation() < (foodMiscarriage * (entity.Properties?.Attributes?["maxSaturation"].AsFloat(20f) ?? 20f)))
            {
                LoseBaby();
                return;
            }

            ITreeAttribute health = entity.WatchedAttributes.GetTreeAttribute("health");
            if (health != null && (health.GetFloat("currenthealth")/health.GetFloat("maxhealth")) < healthMiscarriage)
            {
                LoseBaby();
                return;
            }

            /*if (GrowthCapQuantity > 0 && IsGrowthCapped())
            {
                TimeLastMultiply = entity.World.Calendar.TotalHours;
                return;
            }*/


            if (daysNow - TotalDaysPregnancyStart > PregnancyDays)
            {
                Random rand = entity.World.Rand;

                float q = SpawnQuantityMin + (float)rand.NextDouble() * (SpawnQuantityMax - SpawnQuantityMin);
                TotalDaysLastBirth = daysNow;
                TotalDaysCooldownUntil = daysNow + (MultiplyCooldownDaysMin + rand.NextDouble() * (MultiplyCooldownDaysMax - MultiplyCooldownDaysMin));
                IsPregnant = false;
                entity.Stats.Set("hungerrate", "pregnacy", 0, true);
                entity.WatchedAttributes.MarkPathDirty("multiply");
                EntityProperties childType = entity.World.GetEntityType(SpawnEntityCode);

                int generation = entity.WatchedAttributes.GetInt("generation", 0);

                while (q > 1 || rand.NextDouble() < q)
                {
                    q--;
                    Entity childEntity = entity.World.ClassRegistry.CreateEntity(childType);

                    childEntity.ServerPos.SetFrom(entity.ServerPos);
                    childEntity.ServerPos.Motion.X += (rand.NextDouble() - 0.5f) / 20f;
                    childEntity.ServerPos.Motion.Z += (rand.NextDouble() - 0.5f) / 20f;

                    childEntity.Pos.SetFrom(childEntity.ServerPos);
                    entity.World.SpawnEntity(childEntity);
                    entity.Attributes.SetString("origin", "reproduction");
                    childEntity.WatchedAttributes.SetInt("generation", generation + 1);
                }

            }

            entity.World.FrameProfiler.Mark("entity-multiply");
        }

        private bool TryGetPregnant()
        {
            if (GetSaturation() < (reproduceSaturation * (entity.Properties?.Attributes?["maxSaturation"].AsFloat(20f) ?? 20f))) return false;
            if (TotalDaysCooldownUntil > entity.World.Calendar.TotalDays) return false;

            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute("hunger");
            if (tree == null) return false;

            float saturation = tree.GetFloat("saturation", 0);

            Entity maleentity = null;
            if (RequiresNearbyEntityCode != null && (maleentity = GetRequiredEntityNearby()) == null) return false;

            entity.Stats.Set("hungerrate", "pregnacy", forTwoRate, true);

            if (maleentity != null)
            {
                maleentity.WatchedAttributes.SetFloat("saturation", Math.Max(0, maleentity.WatchedAttributes.GetFloat("saturation") - 1));
            }

            IsPregnant = true;
            TotalDaysPregnancyStart = entity.World.Calendar.TotalDays;
            entity.WatchedAttributes.MarkPathDirty("multiply");

            return true;
        }

        public void LoseBaby()
        {
            //We lost the baby ;_:
            double daysNow = entity.World.Calendar.TotalDays;
            TotalDaysLastBirth = daysNow;
            TotalDaysCooldownUntil = daysNow + (MultiplyCooldownDaysMin + entity.World.Rand.NextDouble() * (MultiplyCooldownDaysMax - MultiplyCooldownDaysMin));
            IsPregnant = false;
            entity.Stats.Set("hungerrate", "pregnacy", 0, true);
            entity.WatchedAttributes.MarkPathDirty("multiply");
        }

        float GetSaturation()
        {
            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute("hunger");
            if (tree == null) return 0;

            return tree.GetFloat("saturation", 0);
        }

        private Entity GetRequiredEntityNearby()
        {
            if (RequiresNearbyEntityCode == null) return null;

            return entity.World.GetNearestEntity(entity.ServerPos.XYZ, RequiresNearbyEntityRange, RequiresNearbyEntityRange, (e) =>
            {
                if (e.WildCardMatch(new AssetLocation(RequiresNearbyEntityCode)))
                {
                    if (!e.WatchedAttributes.GetBool("doesEat") || (e.WatchedAttributes["hunger"] as ITreeAttribute)?.GetFloat("saturation") >= (e.Properties.Attributes?["maxSaturation"].AsFloat(20f) ?? 20f) * reproduceSaturation)
                    {
                        return true;
                    }
                }

                return false;

            });
        }

        public override void OnEntityDespawn(EntityDespawnReason despawn)
        {
            entity.World.UnregisterCallback(callbackId);
        }


        public override void GetInfoText(StringBuilder infotext)
        {
            multiplyTree = entity.WatchedAttributes.GetTreeAttribute("multiply");

            if (IsPregnant)
            {
                infotext.AppendLine(Lang.Get("Is pregnant"));
                ITreeAttribute health = entity.WatchedAttributes.GetTreeAttribute("health");
                if (GetSaturation() < ((foodMiscarriage + 0.1f) * (entity.Properties?.Attributes?["maxSaturation"].AsFloat(20f) ?? 20f)) ||
                    (health != null && (health.GetFloat("currenthealth") / health.GetFloat("maxhealth")) < (healthMiscarriage + 0.1f)))
                    infotext.AppendLine(Lang.Get("farmlife:miscarriage"));
            }
            else
            {
                if (entity.Alive)
                {
                    /*ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute("hunger");
                    if (tree != null)
                    {
                        float saturation = tree.GetFloat("saturation", 0);
                        infotext.AppendLine(Lang.Get("Portions eaten: {0}", saturation));
                    }*/

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
                        if (GetSaturation() < (reproduceSaturation * (entity.Properties?.Attributes?["maxSaturation"].AsFloat(20f) ?? 20f))) infotext.AppendLine(Lang.Get("farmlife:mate"));
                        else infotext.AppendLine(Lang.Get("Ready to mate"));
                    }
                }
            }

            base.GetInfoText(infotext);
        }
        public EntityBehaviorFoodMultiply(Entity entity) : base(entity)
        {
        }

        public override string PropertyName()
        {
            return "foodmultiply";
        }
    }

}
