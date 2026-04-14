using MelonLoader;
using HarmonyLib;
using UnityEngine;
using GHPC.Weapons;
using GHPC.UI.Hud;
using GHPC.Player;
using GHPC.Crew;
using GHPC.State;
using System.Reflection;
using System.Text;
using GHPC;
[assembly: MelonInfo(typeof(AmmoDisplayMod.AmmoDisplayModClass), "Ammo UI Extended", "1.2.1", "Qwertyryo")]
[assembly: MelonGame("Radian Simulations LLC", "GHPC")]


namespace AmmoDisplayMod
{
    public class AmmoDisplayModClass : MelonMod
    {
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("AmmoDisplayMod initialized - patching ammo count display.");


            var harmony = new HarmonyLib.Harmony("AmmoDisplayMod");
            harmony.PatchAll();


        }
    }


    [HarmonyPatch(typeof(GHPC.UI.Hud.WeaponHud), "Update")]
    public static class MainGunAmmoCountPatch
    {
        static bool Prefix(GHPC.UI.Hud.WeaponHud __instance)
        {
            


            if (!__instance.IsInitialized)
            {
                return true;
            }
            __instance._timeLeft -= Time.deltaTime;
            if (__instance._timeLeft > 0f)
            {
                return false;
            }
            __instance._timeLeft = __instance.updateInterval;
            if (__instance.CurrentPlayerWeapon == null)
            {
                return false;
            }
           
            WeaponSystem weapon = __instance.CurrentPlayerWeapon.Weapon;
            bool flag = weapon != null;
            string text = (flag && weapon.WeaponData != null) ? weapon.WeaponData.FriendlyName : __instance.CurrentPlayerWeapon.Name;
            AmmoFeed feed = weapon.Feed;
            bool flag2 = feed != null;
            bool flag3 = __instance.CurrentPlayerWeapon.FCS != null;
            bool flag4 = flag3 && (flag ? weapon.FCS.RegisteredRangeLimits.y : 0f) > 0f;
            string text2 = ((flag2 && feed.LoadedClipType != null) ? feed.LoadedClipType.Name : "[no ammunition loaded]");
            string text3 = "";
            bool flag5 = false;
            if (flag3)
            {
                switch (__instance.CurrentPlayerWeapon.FCS.CurrentStabMode)
                {
                case StabilizationMode.None:
                    text3 = "Unstabilized";
                    break;
                case StabilizationMode.Vector:
                    text3 = "Direction stabilized";
                    flag5 = true;
                    break;
                case StabilizationMode.WorldPoint:
                    text3 = "Point stabilized";
                    flag5 = true;
                    break;
                case StabilizationMode.Target:
                    text3 = "Target tracking";
                    flag5 = true;
                    break;
                }
            }
            string text4 = "";
            if (flag3 && __instance.CurrentPlayerWeapon.FCS.UseDeltaD)
            {
                text4 = (__instance.CurrentPlayerWeapon.FCS.ActiveDeltaD ? "Delta-D enabled\n" : "Delta-D disabled\n");
            }
            int storageCount = 0;
            if (flag2 && feed.LoadedClipType != null)
            {
                FieldInfo loadoutManagerField = typeof(AmmoFeed).GetField("_loadoutManager", BindingFlags.NonPublic | BindingFlags.Instance);
                LoadoutManager loadoutManager = loadoutManagerField?.GetValue(feed) as LoadoutManager;
                if (loadoutManager != null)
                {
                    int totalAmmoOfType = loadoutManager.GetCurrentAmmoCount(feed.LoadedClipType);
                    storageCount = totalAmmoOfType - weapon.ReadyRackReserveCount;
                    if (storageCount < 0) storageCount = 0;
                }
            }
           
            __instance._sb.Clear();
            __instance._sb.AppendLine(string.Format("{0}\n{1} ({2} + {3} + {4})\n{5}", new object[] { text, text2, weapon.CurrentClipRemainingCount, weapon.ReadyRackReserveCount, storageCount, text3 }));
            if (flag5)
            {
                __instance._sb.AppendLine("Stabilizers " + (__instance.CurrentPlayerWeapon.FCS.StabsActive ? "on" : "off"));
            }
            __instance._sb.Append(text4);
            if (flag4)
            {
                __instance._sb.Append(__instance.CurrentPlayerWeapon.FCS.DisplayRange);
            }
            if (flag2)
            {   
                AmmoType.AmmoClip queuedClipType = feed.QueuedClipType;
                if (queuedClipType != null && !queuedClipType.Equals(feed.LoadedClipType))
                {
                    FieldInfo loadoutManagerField = typeof(AmmoFeed).GetField("_loadoutManager", BindingFlags.NonPublic | BindingFlags.Instance);
                    //For some reason, C# doesn't like me pre-defining this instance in its own if(flag2) earlier.
                    LoadoutManager loadoutManager = loadoutManagerField?.GetValue(feed) as LoadoutManager;
                    int totalAmmoOfQueuedType = loadoutManager.GetCurrentAmmoCount(queuedClipType);
                    int queuedReadyRack = 0;
                    foreach (AmmoType.AmmoClip clip in feed.ReadyRack.StoredClips)
                    {
                        if (clip.Equals(queuedClipType)) queuedReadyRack++;
                    }
                    queuedReadyRack *= queuedClipType.Capacity;
                    int queuedStorage = totalAmmoOfQueuedType - queuedReadyRack;
                    __instance._sb.Append("\nNext: " + queuedClipType.Name + " (" + queuedReadyRack + " + " + queuedStorage + ")\n");
                }
                if (feed.Reloading)
                {
                    __instance._sb.Append(feed.WaitingOnRestock ? "\nRESTOCKING..." : "\nRELOADING...");
                }
                else if (weapon.CurrentClipRemainingCount == 0)
                {
                    __instance._sb.Append(feed.WaitingOnMissile ? "\nGUIDING MISSILE..." : "\nBREECH EMPTY");
                }
            }
            var hudTextField = typeof(WeaponHud).GetField("_hudText",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var hudText = hudTextField?.GetValue(__instance);
            hudText?.GetType().GetProperty("text")?.SetValue(hudText, __instance._sb.ToString());
            hudText?.GetType().GetProperty("color")?.SetValue(hudText,
                (weapon.CurrentClipRemainingCount > 0) ? Color.white : Color.red);
            return false;
        }
    }
    [HarmonyPatch(typeof(GHPC.Player.PlayerInput), "Update")]
    public static class RestockMessageAmmoPatch
    {
        static void Postfix(GHPC.Player.PlayerInput __instance)
        {
            if (!__instance.IsInitialized) return;
            CrewBrainWeaponsModule weaponsModule = __instance.CurrentPlayerCrewBrain.WeaponsModule;
            if (__instance.CurrentPlayerCrewBrain == null) return;
            if (weaponsModule?.ActiveWeapon == null) return;
            if(AarController.InAar) return;
            if (__instance._loadoutManager != null && __instance._loadoutManager.Weapon == weaponsModule.ActiveWeapon.Weapon && !__instance.CurrentPlayerUnit.Neutralized && weaponsModule.ActiveWeapon.Weapon.CurrentClipRemainingCount == 0 && !weaponsModule.ActiveWeapon.Weapon.Feed.Reloading && !__instance._loadoutManager.IsRestocking)
            {
                if (weaponsModule.ActiveWeapon.Weapon.Feed.ReadyToReload)
                {
                    int ammoCountsLength = __instance._loadoutManager.TotalAmmoCounts.Length;
                    string[] ammoNames = new string[ammoCountsLength];
                    int[] readyRackCounts = new int[ammoCountsLength];
                    int[] totalCounts = new int[ammoCountsLength];
                    for (int i = 0; i < ammoCountsLength; i++)
                    {
                        AmmoType.AmmoClip clip = __instance._loadoutManager.GetAmmoClipTypeByIndex(i);
                        ammoNames[i] = clip.Name;
                        readyRackCounts[i] = __instance._loadoutManager.RackLoadouts[0].Rack.GetCurrentStoredAmmoCount(clip);
                        totalCounts[i] = __instance._loadoutManager.GetCurrentAmmoCount(i);
                        totalCounts[i] -= readyRackCounts[i];
                    }
                    StringBuilder sb = new StringBuilder("Reload or select a new ammunition type\n\n");
                    for (int i = 0; i < ammoCountsLength; i++)
                        sb.AppendLine(string.Format("{0}: {1} + {2}\n", ammoNames[i], readyRackCounts[i], totalCounts[i]));
                    __instance.VehicleHudText.AddAlertMessage(sb.ToString(), 0.1f);

                }
                else if (__instance._loadoutManager.CanRestock)
                {
                    int ammoCountsLength = __instance._loadoutManager.TotalAmmoCounts.Length;
                    string[] ammoNames = new string[ammoCountsLength];
                    int[] readyRackCounts = new int[ammoCountsLength];
                    int[] totalCounts = new int[ammoCountsLength];
                    for (int i = 0; i < ammoCountsLength; i++)
                    {
                        AmmoType.AmmoClip clip = __instance._loadoutManager.GetAmmoClipTypeByIndex(i);
                        ammoNames[i] = clip.Name;
                        readyRackCounts[i] = __instance._loadoutManager.RackLoadouts[0].Rack.GetCurrentStoredAmmoCount(clip);
                        totalCounts[i] = __instance._loadoutManager.GetCurrentAmmoCount(i);
                    }
                    StringBuilder sb = new StringBuilder("Select ammo (type 1-4) or Q to restock\n\n");
                    for (int i = 0; i < ammoCountsLength; i++)
                        sb.AppendLine(string.Format("{0}: {1} + {2}\n", ammoNames[i], readyRackCounts[i], totalCounts[i]));
                    __instance.VehicleHudText.AddAlertMessage(sb.ToString(), 0.1f);
                }
                else if (!__instance._loadoutManager.AllRacksEmpty)
                {
                    __instance.VehicleHudText.AddAlertMessage("No ammunition available", 0.1f);
                }
            }
        }
    }
}

