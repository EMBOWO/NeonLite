
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

#pragma warning disable CS0414

namespace NeonLite.Modules.Optimization
{
    [Module]
    internal static class MatPropBlocks
    {
        // experimental optimization: helps with tripwire-heavy
        // load times and memory but at a cost of more GPU drawcalls
        // will keep looking into

        const bool priority = true;
        const bool active = false;

        static readonly MaterialPropertyBlock tripwireBlock = new();
        static readonly MaterialPropertyBlock damageableProp = new();

        static void Activate(bool _)
        {
            Patching.AddPatch(typeof(TripwireBeam), "SetTripwireMaterials", TripbeamMats, Patching.PatchTarget.Prefix);
            Patching.AddPatch(typeof(TripwireBeam), "UpdateBeamDischarge", TripbeamDischarge, Patching.PatchTarget.Prefix);
            // Patching.AddPatch(typeof(BaseDamageable), "SetMaterialPropertyFloat", SetDamageableProp, Patching.PatchTarget.Prefix);
            // Patching.AddPatch(typeof(BaseDamageable), "SetReloadShaderFXAmount", SetReloadProp, Patching.PatchTarget.Prefix);
        }

        static readonly int t_beamColorID = Shader.PropertyToID("_BeamColor");
        static readonly int t_shellColorID = Shader.PropertyToID("_ShellColor");
        static readonly int t_blastColorID = Shader.PropertyToID("_Color1");
        static readonly int t_blastAltColorID = Shader.PropertyToID("_Color2");
        static readonly int t_dischargeID = Shader.PropertyToID("_DischargeAmount");

        static bool TripbeamMats(TripwireBeam __instance, Color beam, Color shell)
        {
            tripwireBlock.Clear();
            tripwireBlock.SetColor(t_beamColorID, beam);
            tripwireBlock.SetColor(t_shellColorID, shell);
            tripwireBlock.SetColor(t_blastColorID, beam);
            tripwireBlock.SetColor(t_blastAltColorID, shell);

            var shellM = __instance.rendererBeam.sharedMaterials[0];
            var beamM = __instance.rendererBeam.sharedMaterials[1];
            shellM.enableInstancing = true;
            beamM.enableInstancing = true;
            __instance.rendererBlast.sharedMaterial.enableInstancing = true;

            __instance.rendererBarb.material = beamM;
            __instance.rendererEnergyFlow.material = shellM;
            __instance.rendererHitPoint.materials = [shellM, beamM];
            __instance.rendererOrigin.material = beamM;

            __instance.rendererBarb.SetPropertyBlock(tripwireBlock);
            __instance.rendererBeam.SetPropertyBlock(tripwireBlock);
            __instance.rendererEnergyFlow.SetPropertyBlock(tripwireBlock);
            __instance.rendererHitPoint.SetPropertyBlock(tripwireBlock);
            __instance.rendererOrigin.SetPropertyBlock(tripwireBlock);
            __instance.rendererBlast.SetPropertyBlock(tripwireBlock);

            return false;
        }

        static bool TripbeamDischarge(TripwireBeam __instance, float dischargeAmount, ref float ____beamDischargeAmount)
        {
            if (Mathf.Approximately(dischargeAmount, ____beamDischargeAmount))
                return false;
            var d = Mathf.Clamp01(dischargeAmount);
            ____beamDischargeAmount = d;

            tripwireBlock.Clear();
            tripwireBlock.SetFloat(t_dischargeID, d);

            __instance.rendererOrigin.SetPropertyBlock(tripwireBlock);
            __instance.rendererBeam.SetPropertyBlock(tripwireBlock);
            __instance.rendererHitPoint.SetPropertyBlock(tripwireBlock);
            __instance.rendererEnergyFlow.SetPropertyBlock(tripwireBlock);

            return false;
        }


        // static bool SetTripwireType(EnemyTripwire __instance, EnemyTripwire.TripwireType tType, ref EnemyTripwire.TripwireType ___m_tripwireType)
        // {
        //     TripwireWeapon tripwireWeapon = (TripwireWeapon)__instance.weapons[0];
        //     ___m_tripwireType = tType;
        //     tripwireWeapon._tripwireBeam.SetTripwireBeamType(tType);

        //     if (tType <= EnemyTripwire.TripwireType.Big)
        //     {
        //         if (tType == EnemyTripwire.TripwireType.Standard)
        //         {
        //             __instance.transform.localScale = Vector3.one;
        //             return false;
        //         }
        //         if (tType != EnemyTripwire.TripwireType.Big)
        //             return false;
        //     }
        //     else
        //     {
        //         if (tType == EnemyTripwire.TripwireType.Prime)
        //         {
        //             tripwireBlock.SetColor("_Color", __instance.primeColorBase);
        //             tripwireBlockO.SetColor("_ColorBottom", __instance.primeColor);

        //             __instance.bodyHolder.localScale = Vector3.one * 1.333f;
        //             __instance.capCollider.height *= 1.333f;
        //             __instance.capCollider.radius *= 1.333f;
        //             tripwireWeapon.UpdateAttachPoint();
        //             for (int i = 0; i < __instance.TripOutlineMats.Length; i++)
        //             {
        //                 __instance.TripOutlineMats[i].SetColor("_ColorBottom", __instance.primeColor);
        //             }
        //             for (int j = 0; j < __instance.TripMats.Length; j++)
        //             {
        //                 __instance.TripMats[j].SetColor("_Color", __instance.primeColorBase);
        //             }
        //             return true;
        //         }
        //         if (tType != EnemyTripwire.TripwireType.Turbo)
        //         {
        //             if (tType != EnemyTripwire.TripwireType.Boss)
        //                 return true;
        //             __instance.transform.localScale = Vector3.one;
        //             for (int k = 0; k < __instance.TripOutlineMats.Length; k++)
        //             {
        //                 __instance.TripOutlineMats[k].SetColor("_ColorBottom", __instance.bossColor);
        //             }
        //             for (int l = 0; l < __instance.TripMats.Length; l++)
        //             {
        //                 __instance.TripMats[l].SetColor("_Color", __instance.bossColorBase);
        //             }
        //             return true;
        //         }
        //         else
        //             tripwireWeapon._tripwireBeam.SetBeamWidth(5f);
        //     }

        //     __instance.bodyHolder.localScale = Vector3.one * 1.333f;
        //     __instance.capCollider.height *= 1.333f;
        //     __instance.capCollider.radius *= 1.333f;
        //     tripwireWeapon.UpdateAttachPoint();

        //     return true;
        // }

        static bool SetDamageableProp(BaseDamageable __instance, int propertyID, float value)
        {
            var renderers = __instance.GetRenderers();
            damageableProp.Clear();
            damageableProp.SetFloat(propertyID, value);

            foreach (var r in renderers)
            {
                r.sharedMaterials.DoIf(x => x, x => x.enableInstancing = true);
                r.SetPropertyBlock(damageableProp);
            }

            return false;
        }

        static bool SetReloadProp(BaseDamageable __instance, float amount)
        {
            var renderers = __instance.GetRenderers();
            damageableProp.Clear();
            damageableProp.SetFloat(BaseDamageable.shaderIDReload, amount);
            damageableProp.SetFloat(BaseDamageable.shaderIDVendAmount, amount);

            foreach (var r in renderers)
            {
                r.sharedMaterials.DoIf(x => x, x => x.enableInstancing = true);
                r.SetPropertyBlock(damageableProp);
            }

            return false;
        }
    }

}
