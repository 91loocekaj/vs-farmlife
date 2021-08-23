using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory.API.Common.Entities;
using System.Collections.Generic;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using System.Text;
using Vintagestory.API.Config;

namespace Farmlife
{
    public class EntityBehaviorPetCommand : EntityBehavior
    {
        ITreeAttribute commandTree;
        List<Entity> hitList = new List<Entity>();
        EntityBehaviorConsume hunger;
        float updateOn;

        //These orders determine what the pet will do

        public EnumPetOrder CurrentOrder
        {
            get { return (EnumPetOrder)(hunger?.IsHungry != false ? 0 : commandTree.GetInt("currentorder", 0)); }
            set { commandTree.SetInt("currentorder", (int)value); entity.WatchedAttributes.MarkPathDirty("command"); }
        }

        //This determines whether the pet will attack or not

        public EnumPetAggro CurrentAggro
        {
            get { return (EnumPetAggro)(hunger?.IsHungry != false ? 0 : commandTree.GetInt("currentaggro", 0)); }
            set { commandTree.SetInt("currentaggro", (int)value); entity.WatchedAttributes.MarkPathDirty("command"); }
        }

        public IPlayer master
        {
            get { return entity.World.PlayerByUid(commandTree.GetString("masterUID")); }
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            commandTree = entity.WatchedAttributes.GetTreeAttribute("command");

            if (commandTree == null)
            {
                commandTree = entity.WatchedAttributes.GetOrAddTreeAttribute("command");
                commandTree.SetString("masterUID", "");
            }
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            updateOn += deltaTime;

            if (updateOn >= 30)
            {
                //Refresh the list every 30 seconds
                updateOn = 0;

                List<Entity> refreshList = new List<Entity>();

                foreach (Entity threat in hitList)
                {
                    if (threat != null && threat.Alive && entity.SidedPos.DistanceTo(threat.SidedPos.XYZ) <= 30) refreshList.Add(threat);
                }

                hitList = refreshList;
            }
        }

        public void AddThreat(Entity threat, EnumPetAggro level)
        {
            if ((int)level > commandTree.GetInt("currentaggro")) return;
            if (entity.Api.Side != EnumAppSide.Server || threat.WatchedAttributes.GetInt("generation") >= 3) return;
            if (threat == null || !threat.Alive || hitList.Contains(threat) || threat == entity || master?.HasPrivilege("attackcreatures") == false) return;
            if (threat is EntityPlayer && master != null)
            {
                if (master.Entity == threat) return;
                if (!master.HasPrivilege("attackplayers")) return;

                ICoreServerAPI sapi = entity.Api as ICoreServerAPI;

                if (sapi != null && !sapi.Server.Config.AllowPvP) return;
            }

            hitList.Add(threat);
        }

        public bool GetThreat(Entity sus)
        {
            if (hitList.Contains(sus)) return true;
            return false;
        }

        public void RemoveThreat(Entity threat)
        {
            hitList.Remove(threat);
        }

        public void ClearHitList()
        {
            hitList = new List<Entity>();
            AiTaskManager tmg = entity.GetBehavior<EntityBehaviorTaskAI>()?.taskManager;

            tmg?.StopTask(typeof(AiTaskPetMeleeAttack));
            tmg?.StopTask(typeof(AiTaskPetSeekEntity));
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, float damage)
        {
            if (damageSource?.SourceEntity != null && damageSource.SourceEntity.HasBehavior<EntityBehaviorHealth>()) AddThreat(damageSource.SourceEntity, EnumPetAggro.Defend);

            base.OnEntityReceiveDamage(damageSource, damage);
        }

        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
            hunger = entity.GetBehavior<EntityBehaviorConsume>();
        }
        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();
            hunger = entity.GetBehavior<EntityBehaviorConsume>();
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            base.GetInfoText(infotext);

            commandTree = entity.WatchedAttributes.GetTreeAttribute("command");

            if (master != null)
            {
                infotext.AppendLine(Lang.Get("farmlife:owner", master.PlayerName));
                infotext.AppendLine(Lang.Get("farmlife:commandsdisplay", Lang.Get("farmlife:order-" + (int)CurrentOrder), Lang.Get("farmlife:aggro-" + (int)CurrentAggro)));
            }
        }

        public EntityBehaviorPetCommand(Entity entity) : base(entity)
        {
        }

        public override string PropertyName()
        {
            return "petcommand";
        }
    }

    public enum EnumPetOrder
    {
        Idle,
        Wander,
        Follow
    }

    public enum EnumPetAggro
    {
        Passive,
        Defend,
        Protect,
        Hunt
    }
}
