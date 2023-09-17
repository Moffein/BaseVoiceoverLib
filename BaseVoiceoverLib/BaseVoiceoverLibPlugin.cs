using BepInEx;
using BepInEx.Configuration;
using RoR2;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace BaseVoiceoverLib
{
    [BepInPlugin("com.Moffein.BaseVoiceoverLib", "BaseVoiceoverLib", "1.0.0")]
    public class BaseVoiceoverLibPlugin : BaseUnityPlugin
    {
        public void Awake()
        {
            BaseVoiceoverComponent.Init();
            RoR2.RoR2Application.onLoad += OnLoad;
            On.RoR2.CharacterBody.Start += AttachVoiceoverComponent;
            On.RoR2.SurvivorMannequins.SurvivorMannequinSlotController.RebuildMannequinInstance += LobbyVoicelines;
        }

        private void LobbyVoicelines(On.RoR2.SurvivorMannequins.SurvivorMannequinSlotController.orig_RebuildMannequinInstance orig, RoR2.SurvivorMannequins.SurvivorMannequinSlotController self)
        {
            orig(self);

            foreach (VoiceoverInfo vo in VoiceoverInfo.voiceoverList)
            {
                if (self.currentSurvivorDef.survivorIndex == SurvivorCatalog.GetSurvivorIndexFromBodyIndex(vo.bodyIndex))
                {
                    //Loadout isn't loaded first time this is called, so we need to manually get it.
                    //Probably not the most elegant way to resolve this.
                    if (self.loadoutDirty)
                    {
                        if (self.networkUser)
                        {
                            self.networkUser.networkLoadout.CopyLoadout(self.currentLoadout);
                        }
                    }

                    BodyIndex bodyIndexFromSurvivorIndex = SurvivorCatalog.GetBodyIndexFromSurvivorIndex(self.currentSurvivorDef.survivorIndex);
                    int skinIndex = (int)self.currentLoadout.bodyLoadoutManager.GetSkinIndex(bodyIndexFromSurvivorIndex);
                    SkinDef safe = HG.ArrayUtils.GetSafe<SkinDef>(BodyCatalog.GetBodySkins(bodyIndexFromSurvivorIndex), skinIndex);

                    if (vo.validSkins.Contains(safe))
                    {
                        vo.selectActions?.Invoke(self.mannequinInstanceTransform.gameObject);
                        break;
                    }
                }
            }
        }

        private void OnLoad()
        {
            BaseVoiceoverComponent.scepterIndex = ItemCatalog.FindItemIndex("ITEM_ANCIENT_SCEPTER");
            VoiceoverInfo.ValidateVoiceoverList();
        }

        private void AttachVoiceoverComponent(On.RoR2.CharacterBody.orig_Start orig, CharacterBody self)
        {
            orig(self);
            if (self)
            {
                foreach (VoiceoverInfo vo in VoiceoverInfo.voiceoverList)
                {
                    if (self.bodyIndex == vo.bodyIndex && vo.validSkins.Contains(SkinCatalog.GetBodySkinDef(self.bodyIndex, (int)self.skinIndex)))
                    {
                        BaseVoiceoverComponent existingVoiceoverComponent = self.GetComponent<BaseVoiceoverComponent>();
                        if (!existingVoiceoverComponent) self.gameObject.AddComponent(vo.type);
                        break;
                    }
                }
            }
        }
    }
}

namespace R2API.Utils
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class ManualNetworkRegistrationAttribute : Attribute
    {
    }
}
