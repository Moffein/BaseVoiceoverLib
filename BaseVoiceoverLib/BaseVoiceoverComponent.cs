using UnityEngine.Networking;
using UnityEngine;
using RoR2;
using RoR2.Audio;

namespace BaseVoiceoverLib
{
    //Decisions on how to play sounds/handle cooldowns wil lbe left to the specific voiceover implementation.
    public class BaseVoiceoverComponent : MonoBehaviour
    {
        protected bool playedSpawnVoiceline = false;

        protected float voiceCooldown = 0f;
        protected float spawnVoicelineDelay = 0f;
        protected CharacterBody body;

        protected Inventory inventory;
        private bool addedInventoryHook = false;

        protected HealthComponent healthComponent;
        private bool playedDeathSound = false;
        private float prevHP = 0f;

        private int prevLevel = 0;

        protected SkillLocator skillLocator;

        private static bool initializedHooks = false;
        public static void Init()
        {
            if (initializedHooks) return;
            initializedHooks = true;

            On.RoR2.CharacterMotor.Jump += Hooks.CharacterMotor_Jump;
            On.RoR2.TeleporterInteraction.ChargingState.OnEnter += Hooks.ChargingState_OnEnter;
            On.RoR2.TeleporterInteraction.ChargedState.OnEnter += Hooks.ChargedState_OnEnter;
            On.RoR2.HealthComponent.TakeDamage += Hooks.HealthComponent_TakeDamage;
            On.EntityStates.Missions.BrotherEncounter.EncounterFinished.OnEnter += Hooks.EncounterFinished_OnEnter;
        }

        private static class Hooks
        {
            public static void CharacterMotor_Jump(On.RoR2.CharacterMotor.orig_Jump orig, CharacterMotor self, float horizontalMultiplier, float verticalMultiplier, bool vault)
            {
                orig(self, horizontalMultiplier, verticalMultiplier, vault);

                if (self)
                {
                    BaseVoiceoverComponent bvc = self.GetComponent<BaseVoiceoverComponent>();
                    if (bvc)
                    {
                        bvc.PlayJump();
                    }
                }
            }

            public static void ChargingState_OnEnter(On.RoR2.TeleporterInteraction.ChargingState.orig_OnEnter orig, EntityStates.BaseState self)
            {
                orig(self);

                foreach (CharacterMaster cm in CharacterMaster.readOnlyInstancesList)
                {
                    if (cm)
                    {
                        GameObject bodyObject = cm.GetBodyObject();
                        if (bodyObject)
                        {
                            BaseVoiceoverComponent bvc = bodyObject.GetComponent<BaseVoiceoverComponent>();
                            if (bvc)
                            {
                                bvc.PlayTeleporterStart();
                            }
                        }
                    }
                }
            }

            public static void ChargedState_OnEnter(On.RoR2.TeleporterInteraction.ChargedState.orig_OnEnter orig, EntityStates.BaseState self)
            {
                orig(self);

                foreach (CharacterMaster cm in CharacterMaster.readOnlyInstancesList)
                {
                    if (cm)
                    {
                        GameObject bodyObject = cm.GetBodyObject();
                        if (bodyObject)
                        {
                            BaseVoiceoverComponent bvc = bodyObject.GetComponent<BaseVoiceoverComponent>();
                            if (bvc)
                            {
                                bvc.PlayTeleporterFinish();
                            }
                        }
                    }
                }
            }

            public static void EncounterFinished_OnEnter(On.EntityStates.Missions.BrotherEncounter.EncounterFinished.orig_OnEnter orig, EntityStates.Missions.BrotherEncounter.EncounterFinished self)
            {
                orig(self);

                foreach (CharacterMaster cm in CharacterMaster.readOnlyInstancesList)
                {
                    if (cm)
                    {
                        GameObject bodyObject = cm.GetBodyObject();
                        if (bodyObject)
                        {
                            BaseVoiceoverComponent bvc = bodyObject.GetComponent<BaseVoiceoverComponent>();
                            if (bvc)
                            {
                                bvc.PlayVictory();
                            }
                        }
                    }
                }
            }

            public static void HealthComponent_TakeDamage(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
            {
                orig(self, damageInfo);
                if (damageInfo.rejected)
                {
                    BaseVoiceoverComponent bvc = self.GetComponent<BaseVoiceoverComponent>();
                    if (bvc) bvc.PlayDamageBlockedServer();
                }
            }
        }
        public bool TryPlayNetworkSound(string soundName, float cooldown, bool forcePlay)
        {
            NetworkSoundEventIndex index = NetworkSoundEventCatalog.FindNetworkSoundEventIndex(soundName);
            return TryPlayNetworkSound(index, cooldown, forcePlay);
        }

        public bool TryPlayNetworkSound(NetworkSoundEventDef nse, float cooldown, bool forcePlay)
        {
            return TryPlayNetworkSound(nse.index, cooldown, forcePlay);
        }

        public bool TryPlayNetworkSound(NetworkSoundEventIndex networkSoundIndex, float cooldown, bool forcePlay)
        {
            bool playedSound = false;

            if (ComponentEnableVoicelines() && (CanPlayVoiceline() || forcePlay))
            {
                if (NetworkServer.active)
                {
                    EntitySoundManager.EmitSoundServer(networkSoundIndex, base.gameObject);
                }
                else
                {
                    EffectManager.SimpleSoundEffect(networkSoundIndex, base.gameObject.transform.position, true);
                }
                playedSound = true;

                SetVoiceCooldown(cooldown);
            }

            return playedSound;
        }

        public bool TryPlaySound(string soundName, float cooldown, bool forcePlay)
        {
            bool playedSound = false;

            if (ComponentEnableVoicelines() && (CanPlayVoiceline() || forcePlay))
            {
                RoR2.Util.PlaySound(soundName, base.gameObject);
                playedSound = true;

                SetVoiceCooldown(cooldown);
            }

            return playedSound;
        }

        protected virtual void Awake()
        {
            //Get components separately to be extra safe
            body = base.GetComponent<CharacterBody>();
            skillLocator = base.GetComponent<SkillLocator>();
            healthComponent = base.GetComponent<HealthComponent>();

            if (body)
            {
                if (skillLocator) body.onSkillActivatedAuthority += Body_onSkillActivatedAuthority;
            }
            SetSpawnVoicelineDelay();
        }


        //This runs during Awake().
        protected virtual void SetSpawnVoicelineDelay()
        {
            spawnVoicelineDelay = 3f;

            //Delay on first stage due to drop pod. (There's nothing to actually check whether you've left it or not).
            if (Run.instance && Run.instance.stageClearCount == 0)
            {
                spawnVoicelineDelay = 6.5f;
            }
            voiceCooldown = spawnVoicelineDelay;
        }

        protected virtual void Start()
        {
            if (body && body.inventory)
            {
                inventory = body.inventory;
                inventory.onItemAddedClient += Inventory_onItemAddedClient;
                addedInventoryHook = true;
            }
        }

        protected virtual void OnDestroy()
        {
            if (addedInventoryHook)
            {
                if (inventory) inventory.onItemAddedClient -= Inventory_onItemAddedClient;
            }
        }

        protected virtual void Inventory_onItemAddedClient(ItemIndex itemIndex) { }

        protected virtual void Body_onSkillActivatedAuthority(GenericSkill skill)
        {
            if (skill == skillLocator.primary)
            {
                PlayPrimaryAuthority();
            }
            else if (skill == skillLocator.secondary)
            {
                PlaySecondaryAuthority();
            }
            else if (skill == skillLocator.utility)
            {
                PlayUtilityAuthority();
            }
            else if (skill == skillLocator.special)
            {
                PlaySpecialAuthority();
            }
        }

        protected virtual void FixedUpdate()
        {
            if (voiceCooldown > 0f)
            {
                voiceCooldown -= Time.fixedDeltaTime;
                if (voiceCooldown < 0f) voiceCooldown = 0f;
            }
            
            if (!playedSpawnVoiceline)
            {
                if (spawnVoicelineDelay > 0f)
                {
                    spawnVoicelineDelay -= Time.fixedDeltaTime;
                }
                if (spawnVoicelineDelay <= 0f)
                {
                    playedSpawnVoiceline = true;
                    PlaySpawn();
                }
            }

            HandleHealthComponent();
            HandleBody();
        }

        //Do it like this so that less hooks are needed.
        protected virtual void HandleHealthComponent()
        {
            if (healthComponent)
            {
                if (!healthComponent.alive)
                {
                    if (!playedDeathSound)
                    {
                        playedDeathSound = true;
                        PlayDeath();
                    }
                }

                float currentHP = healthComponent.health;
                if (healthComponent.combinedHealthFraction <= 0.25f)
                {
                    PlayLowHealth();
                }
                else
                {
                    //Only count actual HP damage for pain sounds.
                    if (currentHP < prevHP)
                    {
                        PlayHurt((prevHP - currentHP) / healthComponent.fullHealth);
                    }
                    prevHP = currentHP;
                }
            }
        }

        //Do it like this so that less hooks are needed.
        protected virtual void HandleBody()
        {
            if (body)
            {
                int currentLevel = Mathf.FloorToInt(body.level);
                if (currentLevel > prevLevel && prevLevel != 0)
                {
                    PlayLevelUp();
                }
                prevLevel = currentLevel;
            }
        }

        public bool CanPlayVoiceline()
        {
            return voiceCooldown <= 0f && !(healthComponent && !healthComponent.alive);
        }

        public void SetVoiceCooldown(float newCooldown)
        {
            if (this.voiceCooldown < newCooldown)
            {
                this.voiceCooldown = newCooldown;
            }
        }

        //Override this and have it return your plugin's Voiceline On/Off setting instead.
        public virtual bool ComponentEnableVoicelines()
        {
            return true;
        }

        //Naming Scheme:
        //Authority = Runs on client, requires NetworkSoundEventDef and TryPlayNetworkSound
        //Server = Runs on server, requires NetworkSoundEventDef and TryPlayNetworkSound
        //No tag = Runs on everyone, can just use TryPlaySound
        public virtual void PlaySpawn() { } //Plays after the body spawns
        public virtual void PlayPrimaryAuthority() { }
        public virtual void PlaySecondaryAuthority() { }
        public virtual void PlayUtilityAuthority() { }
        public virtual void PlaySpecialAuthority() { }
        public virtual void PlayDamageBlockedServer() { }
        public virtual void PlayHurt(float percentHPLost) { }
        public virtual void PlayJump() { }
        public virtual void PlayDeath() { }
        public virtual void PlayTeleporterStart() { }
        public virtual void PlayTeleporterFinish() { }
        public virtual void PlayVictory() { }
        public virtual void PlayLowHealth() { }
        public virtual void PlayLevelUp() { }
    }
}
