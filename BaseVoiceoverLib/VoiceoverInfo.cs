using RoR2;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BaseVoiceoverLib
{
    public class VoiceoverInfo
    {
        public static List<VoiceoverInfo> voiceoverList = new List<VoiceoverInfo>();

        //This gets run after OnLoad to properly assign the BodyIndices if there's any VO that got added before the BodyCatalog was initialized.
        public static void ValidateVoiceoverList()
        {
            foreach (VoiceoverInfo vo in voiceoverList)
            {
                vo.bodyIndex = BodyCatalog.FindBodyIndex(vo.bodyName);
            }
        }

        public delegate void LobbySelectActions(GameObject mannequinObject);
        public LobbySelectActions selectActions;

        public Type type;
        public List<SkinDef> validSkins;
        public BodyIndex bodyIndex = BodyIndex.None;
        public string bodyName;

        public VoiceoverInfo(Type type, SkinDef skinDef, string bodyName)
        {
            if (typeof(BaseVoiceoverComponent).IsAssignableFrom(type))
            {
                this.type = type;
            }
            else
            {
                Debug.LogError("Created VoiceoverInfo with a type that isn't assignable from BaseVoiceoverComponent.");
            }

            this.validSkins = new List<SkinDef>();
            this.validSkins.Add(skinDef);

            this.bodyIndex = BodyCatalog.FindBodyIndex(bodyName);
            this.bodyName = bodyName;

            voiceoverList.Add(this);
        }

        public VoiceoverInfo(Type type, List<SkinDef> validSkinsList, string bodyName)
        {
            if (typeof(BaseVoiceoverComponent).IsAssignableFrom(type))
            {
                this.type = type;
            }
            else
            {
                Debug.LogError("Created VoiceoverInfo with a type that isn't assignable from BaseVoiceoverComponent.");
            }

            this.validSkins = validSkinsList;

            this.bodyIndex = BodyCatalog.FindBodyIndex(bodyName);
            this.bodyName = bodyName;

            voiceoverList.Add(this);
        }
    }
}
