using System.Linq;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using HarmonyLib;
using Vintagestory.GameContent;
using System.Reflection;

namespace Farmlife
{
    public class Farmlife : ModSystem
    {

        private Harmony harmony;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterEntityBehaviorClass("consume", typeof(EntityBehaviorConsume));
            api.RegisterEntityBehaviorClass("foodmultiply", typeof(EntityBehaviorFoodMultiply));
            api.RegisterEntityBehaviorClass("foodgrow", typeof(EntityBehaviorFoodGrow));
            api.RegisterEntityBehaviorClass("layegg", typeof(EntityBehaviorLayEgg));
            api.RegisterEntityBehaviorClass("foodmilk", typeof(EntityBehaviorFoodMilk));
            api.RegisterBlockBehaviorClass("FoodMilkContainer", typeof(BlockBehaviorFoodMilkContainer));
            api.RegisterEntityBehaviorClass("carry", typeof(EntityBehaviorCarry));

            api.RegisterMountable("carrier", EntityBehaviorCarry.GetMountable);

            api.RegisterItemClass("ItemHandlingGloves", typeof(ItemHandlingGloves));

            AiTaskRegistry.Register("seekfoodandeat", typeof(AiTaskTweakedSeekFoodAndEat));

            harmony = new Harmony("com.jakecool19.farmlife.animalhunger");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            try
            {
                FarmerConfig FromDisk;
                if ((FromDisk = api.LoadModConfig<FarmerConfig>("FarmLifeConfig.json")) == null)
                {
                    api.StoreModConfig<FarmerConfig>(FarmerConfig.Loaded, "FarmLifeConfig.json");
                }
                else FarmerConfig.Loaded = FromDisk;
            }
            catch
            {
                api.StoreModConfig<FarmerConfig>(FarmerConfig.Loaded, "FarmLifeConfig.json");
            }
        }

        public override void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            base.Dispose();
        }
    }

    public class FarmerConfig
    {
        public static FarmerConfig Loaded { get; set; } = new FarmerConfig();
        public bool RestrictPathfinding { get; set; } = false;
    }

    [HarmonyPatch(typeof(EntityBehaviorHunger), "OnEntityReceiveSaturation")]
    class SeekFoodPatch
    {
        [HarmonyPrepare]
        static bool Prepare()
        {
            return true;
        }

        [HarmonyPrefix]
        static void Prefix(ref float saturation, EntityBehaviorHunger __instance)
        {
            saturation *= __instance.entity.Stats.GetBlended("digestion");
        }
    }

    [HarmonyPatch(typeof(LooseItemFoodSource), "ConsumeOnePortion")]
    class OneAtATimePatch
    {
        [HarmonyPrepare]
        static bool Prepeare()
        {
            return true;
        }

        [HarmonyPrefix]
        static bool Prefix(LooseItemFoodSource __instance, ref EntityItem ___entity, ref float __result)
        {
            if (___entity.Itemstack.StackSize <= 0)
            {
                ___entity.Die();
                __result = 0f;
                return false;
            }

            return true;
        }
    }

}
