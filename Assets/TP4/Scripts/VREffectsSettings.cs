using System;
using System.Reflection;
using UnityEngine;

namespace TP4
{
    /// <summary>
    /// Réglages appliqués au lancement pour les effets Cartoon FX Remaster en VR :
    /// le "camera shake" des explosions secoue la caméra du casque, ce qui donne la nausée.
    /// On le coupe globalement (par réflexion, pour ne pas dépendre du pack s'il est supprimé).
    /// </summary>
    static class VREffectsSettings
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            Type cfxr = Type.GetType("CartoonFX.CFXR_Effect, CFXRRuntime");
            if (cfxr == null) return;
            FieldInfo shake = cfxr.GetField("GlobalDisableCameraShake", BindingFlags.Public | BindingFlags.Static);
            if (shake != null) shake.SetValue(null, true);
        }
    }
}
