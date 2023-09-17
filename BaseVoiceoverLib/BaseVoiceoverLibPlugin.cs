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
