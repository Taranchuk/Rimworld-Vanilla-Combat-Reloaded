﻿using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace VCR
{
    public class Parrying
    {
        public static float front = 1;
        public static float side = 1;
        public static bool active = false;
        public static float parryChance(Verb_MeleeAttack verb, float angle)
        {
            if (!active)
            {
                return 1;
            }
            //Log.Message("parry?");
            Pawn caster = verb.CasterPawn;
            Pawn target = verb.CurrentTarget.Thing as Pawn;
            if (target == null)
            {
                return 0;
            }
            if (!target.equipment?.HasAnything() ?? false)//check if there is something to block with
            {
                return 0;
            }
            if (target.stances.curStance is Stance_Busy stance_Busy && stance_Busy.verb != null && !stance_Busy.verb.verbProps.IsMeleeAttack)//if not in melee, cant parry
            {
                return 0;
            }
            RotationDirection rot = Flanking.getdirection(angle, target);
            float d = (rot.Equals(RotationDirection.None) || target.Downed) ? 0 : rot.Equals(RotationDirection.Opposite) ? front : side;//assign setting based direction and settings
            if (d == 0)//if back hit, cant parry, if downed, cant parry
            {
                return 0;
            }
            float c = StatDefOf.MeleeHitChance.Worker.GetValue(StatRequest.For(caster));//caster gets chances to hit
            ideologyOffset(ref c, caster);
            float t = StatDefOf.MeleeHitChance.Worker.GetValue(StatRequest.For(target));//target gets chance to block each hit
            ideologyOffset(ref t, target);
            return Mathf.Pow(t, (1 / d) / (1 - Mathf.Min(c,0.999f)));//chance of parry, higher d means higher hit chance (root of <1), lower d means lower chance (power <1), NOT LINEAR
        }
        public static void ideologyOffset(ref float num, Pawn pawn)
        {
            if (ModsConfig.IdeologyActive)
            {
                if (DarknessCombatUtility.IsOutdoorsAndLit(pawn))
                {
                    num += pawn.GetStatValue(StatDefOf.MeleeHitChanceOutdoorsLitOffset);
                }
                else if (DarknessCombatUtility.IsOutdoorsAndDark(pawn))
                {
                    num += pawn.GetStatValue(StatDefOf.MeleeHitChanceOutdoorsDarkOffset);
                }
                else if (DarknessCombatUtility.IsIndoorsAndDark(pawn))
                {
                    num += pawn.GetStatValue(StatDefOf.MeleeHitChanceIndoorsDarkOffset);
                }
                else if (DarknessCombatUtility.IsIndoorsAndLit(pawn))
                {
                    num += pawn.GetStatValue(StatDefOf.MeleeHitChanceIndoorsLitOffset);
                }
            }
        }

        public delegate bool surpriseAttack(Verb_MeleeAttack verb_MeleeAttack);
        public static readonly surpriseAttack surpriseattack =
            AccessTools.MethodDelegate<surpriseAttack>(AccessTools.PropertyGetter(typeof(Verb_MeleeAttack), "surpriseAttack"));

        public delegate bool IsTargetImmobile(Verb_MeleeAttack verb_MeleeAttack, LocalTargetInfo target);
        public static readonly IsTargetImmobile istargetimmobile =
            AccessTools.MethodDelegate<IsTargetImmobile>(AccessTools.Method(typeof(Verb_MeleeAttack), "IsTargetImmobile"));

        public static bool Parry(ref Verb_MeleeAttack verb, ref bool result, ref SoundDef soundDef)
        {
            if (!active)
            {
                return false;
            }
            //Log.Message("parry?");
            Pawn caster = verb.CasterPawn;
            Pawn target = verb.CurrentTarget.Thing as Pawn;
            if (target == null)
            {
                return false;
            }
            if (surpriseattack(verb))
            {
                return false;
            }
            if (istargetimmobile(verb, verb.CurrentTarget))
            {
                return false;
            }
            if (!target.equipment?.HasAnything() ?? false)//check if there is something to block with
            {
                return false;
            }
            if (target.stances.curStance is Stance_Busy stance_Busy && stance_Busy.verb != null && !stance_Busy.verb.verbProps.IsMeleeAttack)//if not in melee, cant parry
            {
                return false;
            }
            RotationDirection rot = Flanking.getdirection(caster, target);
            float d = (rot.Equals(RotationDirection.None) || target.Downed) ? 0 : rot.Equals(RotationDirection.Opposite) ? front : side;//assign setting based direction and settings
            if (d == 0)//if back hit, cant parry, if downed, cant parry
            {
                return false;
            }
            float c = StatDefOf.MeleeHitChance.Worker.GetValue(StatRequest.For(caster));//caster gets chances to hit
            ideologyOffset(ref c, caster);
            float t = StatDefOf.MeleeHitChance.Worker.GetValue(StatRequest.For(target));//target gets chance to block each hit
            ideologyOffset(ref t, target);
            if (!Rand.Chance(Mathf.Pow(t, (1 / d) / (1 - c))))//chance of parry, higher d means higher hit chance (root of <1), lower d means lower chance (power <1), NOT LINEAR
            {
                return false;
            }
            //Log.Message("parry success");
            result = false;
            soundDef = SoundParry(verb);
            MoteMaker.ThrowText(target.DrawPos, target.Map, "VCR.TextMote_Parry".Translate(), 1.9f);
            verb.CreateCombatLog((ManeuverDef maneuver) => maneuver.combatLogRulesDeflect, alwaysShow: false);
            return true;
        }
        public static SoundDef SoundParry(Verb verb)
        {
            if (verb.EquipmentSource != null && !verb.EquipmentSource.def.meleeHitSound.NullOrUndefined())
            {
                return verb.EquipmentSource.def.meleeHitSound;
            }
            if (verb.tool != null && !verb.tool.soundMeleeHit.NullOrUndefined())
            {
                return verb.tool.soundMeleeHit;
            }
            if (verb.EquipmentSource != null && verb.EquipmentSource.Stuff != null)
            {
                if (verb.verbProps.meleeDamageDef.armorCategory == DamageArmorCategoryDefOf.Sharp)
                {
                    if (!verb.EquipmentSource.Stuff.stuffProps.soundMeleeHitSharp.NullOrUndefined())
                    {
                        return verb.EquipmentSource.Stuff.stuffProps.soundMeleeHitSharp;
                    }
                }
                else if (!verb.EquipmentSource.Stuff.stuffProps.soundMeleeHitBlunt.NullOrUndefined())
                {
                    return verb.EquipmentSource.Stuff.stuffProps.soundMeleeHitBlunt;
                }
            }
            if (verb.CasterPawn != null && !verb.CasterPawn.def.race.soundMeleeHitPawn.NullOrUndefined())
            {
                return verb.CasterPawn.def.race.soundMeleeHitPawn;
            }
            return SoundDefOf.Pawn_Melee_Punch_HitPawn;
        }

    }
    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Verb_MeleeAttack_TryCastShot_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            MethodBase from = AccessTools.Method(typeof(Verb_MeleeAttack), "GetDodgeChance");
            MethodBase to = AccessTools.Method(typeof(Parrying), "Parry");

            int a = -1;
            Label label = il.DefineLabel();

            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.operand as MethodBase == from)//find method to replace
                {
                    a = 2;
                    yield return instruction;//add dodge chance instruction back
                    continue;
                }
                if (a > 0)
                {
                    a--;
                    yield return instruction;//rand calc,if true skip to dodge
                    continue;
                }
                if (a == 0)
                {
                    a = -1;
                    yield return new CodeInstruction(OpCodes.Ldarga, 0);
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 0);
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 6);
                    yield return new CodeInstruction(OpCodes.Call, to);//load in arguments and execute parry, check for rand block chance
                    yield return new CodeInstruction(OpCodes.Brtrue_S, label);//if parry succeeds, we skip to end
                    yield return instruction;
                    continue;
                }
                if (instruction.opcode == OpCodes.Ldloc_S && instruction.operand is LocalBuilder lb && lb.LocalIndex == 6)
                {
                    instruction.labels.Add(label);
                    yield return instruction;
                    continue;
                }
                else
                {
                    yield return instruction;
                }
            }
        }
    }
}