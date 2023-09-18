using UnityEngine.Networking;
using UnityEngine;
using RoR2;
using RoR2.Audio;
using System.Collections.Generic;
using System;

namespace BaseVoiceoverLib
{
    //Decisions on how to play sounds/handle cooldowns wil lbe left to the specific voiceover implementation.
    public class BaseVoiceoverComponent : MonoBehaviour
    {
        //Check this in Inventory_onItemAddedClient if you want to play a voiceline when acquiring the Ancient Scepter.
        //I usually add extra logic around it, but it's too specific to include in this class.
        public static ItemIndex scepterIndex;

        protected bool playedSpawnVoiceline = false;

        protected float voiceCooldown = 0f;
        protected float spawnVoicelineDelay = 0f;
        protected CharacterBody body;
        protected Inventory inventory;
        protected SkillLocator skillLocator;
        protected HealthComponent healthComponent;
        protected NetworkStateMachine networker;

        private bool playedDeathSound = false;
        private float prevHP = 0f;
        private int prevLevel = 0;
        private bool addedInventoryHook = false;


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
            On.RoR2.ShrineChanceBehavior.AddShrineStack += Hooks.ShrineChanceBehavior_AddShrineStack;
        }

        private static class Hooks
        {
            public static void ShrineChanceBehavior_AddShrineStack(On.RoR2.ShrineChanceBehavior.orig_AddShrineStack orig, ShrineChanceBehavior self, Interactor activator)
            {
                int successes = self.successfulPurchaseCount;
                orig(self, activator);

                //No change in successes = fail
                if (NetworkServer.active)
                {
                    if (activator)
                    {
                        BaseVoiceoverComponent voice = activator.GetComponent<BaseVoiceoverComponent>();
                        if (voice)
                        {
                            if (self.successfulPurchaseCount == successes)
                            {
                                voice.PlayShrineOfChanceFailServer();
                            }
                            else
                            {
                                voice.PlayShrineOfChanceSuccessServer();
                            }
                        }
                    }
                }
            }

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

        public bool isAuthority()
        {
            return networker && networker.hasAuthority;
        }

        protected virtual void Awake()
        {
            //Get components separately to be extra safe
            body = base.GetComponent<CharacterBody>();
            skillLocator = base.GetComponent<SkillLocator>();
            healthComponent = base.GetComponent<HealthComponent>();
            networker = base.GetComponent<NetworkStateMachine>();

            if (body)
            {
                if (skillLocator) body.onSkillActivatedAuthority += Body_onSkillActivatedAuthority;
            }
            SetSpawnVoicelineDelay();
        }

        //Runs in Awake. Sets delay for the spawn voicelien. Set playedSpawnVoiceline to true here if you want no spawn voiceline.
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
                PlayPrimaryAuthority(skillLocator.primary);
            }
            else if (skill == skillLocator.secondary)
            {
                PlaySecondaryAuthority(skillLocator.secondary);
            }
            else if (skill == skillLocator.utility)
            {
                PlayUtilityAuthority(skillLocator.utility);
            }
            else if (skill == skillLocator.special)
            {
                PlaySpecialAuthority(skillLocator.special);
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

            FixedUpdateHealth();
            FixedUpdateBody();
        }

        protected virtual void Update()
        {
            if (isAuthority()) CheckInputs();
        }

        //Allow players to spam voicelines?
        protected virtual void CheckInputs() { }

        //Checks for death, low health, and HP lost.
        protected virtual void FixedUpdateHealth()
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

        //Checks for levelup.
        protected virtual void FixedUpdateBody()
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
        public virtual void PlayPrimaryAuthority(GenericSkill skill) { }
        public virtual void PlaySecondaryAuthority(GenericSkill skill) { }
        public virtual void PlayUtilityAuthority(GenericSkill skill) { }
        public virtual void PlaySpecialAuthority(GenericSkill skill) { }
        public virtual void PlayDamageBlockedServer() { }
        public virtual void PlayShrineOfChanceSuccessServer() { }
        public virtual void PlayShrineOfChanceFailServer() { }
        public virtual void PlayHurt(float percentHPLost) { }
        public virtual void PlayJump() { }
        public virtual void PlayDeath() { }
        public virtual void PlayTeleporterStart() { }
        public virtual void PlayTeleporterFinish() { }
        public virtual void PlayVictory() { }
        public virtual void PlayLowHealth() { }
        public virtual void PlayLevelUp() { }

        //Played in lobby. 
        public virtual void PlayLobby() { }
    }
}
