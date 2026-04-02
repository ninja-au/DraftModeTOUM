using AmongUs.GameOptions;
using DraftModeTOUM.Managers;
using HarmonyLib;
using MiraAPI.GameOptions;
using Reactor.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using TownOfUs.Utilities;
using UnityEngine;

namespace DraftModeTOUM.Patches
{
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
    internal static class ForceStartCommand
    {
        private const string Command = "/fs";

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First + 109)]
        private static bool Prefix(ChatController __instance)
        {
            string msg = __instance.freeChatField.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(msg)) return true;

            if (IsCommand(msg))
            {
                string feedback = Handle(msg);
                if (!string.IsNullOrWhiteSpace(feedback))
                    MiscUtils.AddFakeChat(PlayerControl.LocalPlayer.Data, "<color=#8BFDFD>System</color>", feedback);
                ClearChat(__instance);
                return false;
            }

            return true;
        }

        private static string Handle(string msg)
        {
            if (!AmongUsClient.Instance.AmHost)
                return "<color=red>Only the host can use /fs.</color>";

            if (DraftManager.IsDraftActive)
                return "<color=red>Draft is currently running. Cancel it first with the Cancel button.</color>";

            if (!OptionGroupSingleton<DraftModeOptions>.Instance.EnableDraft)
                return "<color=red>Draft mode is not enabled in options.</color>";

            if (GameStartManager.Instance == null)
                return "<color=red>Cannot start game right now.</color>";

            if (AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Joined)
                return "<color=red>Game is not in lobby state.</color>";

            int playerCount = GameData.Instance?.AllPlayers.ToArray().Count(p => p != null && !p.Disconnected) ?? 0;
            if (playerCount == 0)
                return "<color=red>No players found.</color>";

            DraftModePlugin.Logger.LogInfo($"[ForceStartCommand] Force starting game with {playerCount} players, skipping draft.");

            Coroutines.Start(CoForceStartWithRoles());

            return "<color=#FFD700>Force starting game with draft roles...</color>";
        }

        private static System.Collections.IEnumerator CoForceStartWithRoles()
        {
            DraftModePlugin.Logger.LogInfo("[ForceStartCommand] Building role pool...");

            var pool = RolePoolBuilder.BuildPool();
            if (pool.RoleIds.Count == 0)
            {
                DraftModePlugin.Logger.LogError("[ForceStartCommand] No roles available in pool!");
                MiscUtils.AddFakeChat(PlayerControl.LocalPlayer.Data, "System", "<color=red>Error: No roles available!</color>");
                yield break;
            }

            var players = PlayerControl.AllPlayerControls.ToArray()
                .Where(p => p != null && p.Data != null && !p.Data.Disconnected)
                .ToList();

            var availableRoles = new List<ushort>(pool.RoleIds);
            var roleCounts = new Dictionary<ushort, int>();

            foreach (var roleId in pool.RoleIds)
                roleCounts[roleId] = 0;

            int impostorsAssigned = 0;
            int maxImpostors = Mathf.Clamp(Mathf.RoundToInt(OptionGroupSingleton<DraftModeOptions>.Instance.MaxImpostors), 1, 3);

            DraftManager.PendingRoleAssignments.Clear();

            foreach (var player in players)
            {
                if (availableRoles.Count == 0)
                {
                    DraftModePlugin.Logger.LogWarning($"[ForceStartCommand] Ran out of roles for player {player.Data.PlayerName}");
                    break;
                }

                ushort selectedRoleId = PickRoleForPlayer(availableRoles, pool, roleCounts, impostorsAssigned, maxImpostors);
                
                if (pool.Factions.TryGetValue(selectedRoleId, out var faction))
                {
                    if (faction == RoleFaction.Impostor && impostorsAssigned >= maxImpostors)
                    {
                        var nonImpostorRoles = availableRoles
                            .Where(r => !pool.Factions.TryGetValue(r, out var f) || f != RoleFaction.Impostor)
                            .ToList();
                        
                        if (nonImpostorRoles.Count > 0)
                        {
                            selectedRoleId = PickWeighted(nonImpostorRoles, pool);
                            faction = pool.Factions.TryGetValue(selectedRoleId, out var newFaction) ? newFaction : RoleFaction.Crewmate;
                        }
                    }
                    
                    if (faction == RoleFaction.Impostor)
                        impostorsAssigned++;
                }

                int maxCount = pool.MaxCounts.TryGetValue(selectedRoleId, out var mc) ? mc : 1;
                roleCounts[selectedRoleId]++;
                if (roleCounts[selectedRoleId] >= maxCount)
                    availableRoles.Remove(selectedRoleId);

                DraftManager.PendingRoleAssignments[player.PlayerId] = (RoleTypes)selectedRoleId;
                DraftModePlugin.Logger.LogInfo($"[ForceStartCommand] Assigned {(RoleTypes)selectedRoleId} to {player.Data.PlayerName}");
            }

            DraftModePlugin.Logger.LogInfo($"[ForceStartCommand] Assigned {DraftManager.PendingRoleAssignments.Count} roles. Starting game...");

            MiscUtils.AddFakeChat(PlayerControl.LocalPlayer.Data, "System", 
                $"<color=#FFD700>Draft roles assigned to {DraftManager.PendingRoleAssignments.Count} players. Starting game...</color>");

            yield return new WaitForSeconds(0.5f);

            DraftManager.SkipCountdown = true;
            int origMinPlayers = GameStartManager.Instance.MinPlayers;
            GameStartManager.Instance.MinPlayers = 1;
            GameStartManager.Instance.BeginGame();
            GameStartManager.Instance.MinPlayers = origMinPlayers;
        }

        private static ushort PickRoleForPlayer(List<ushort> availableRoles, DraftRolePool pool, Dictionary<ushort, int> roleCounts, int impostorsAssigned, int maxImpostors)
        {
            var candidates = new List<ushort>(availableRoles);
            
            if (impostorsAssigned >= maxImpostors)
            {
                var nonImpostorCandidates = candidates
                    .Where(r => !pool.Factions.TryGetValue(r, out var f) || f != RoleFaction.Impostor)
                    .ToList();
                
                if (nonImpostorCandidates.Count > 0)
                    candidates = nonImpostorCandidates;
            }

            if (candidates.Count == 0)
                candidates = new List<ushort>(availableRoles);

            return PickWeighted(candidates, pool);
        }

        private static ushort PickWeighted(List<ushort> candidates, DraftRolePool pool)
        {
            bool useChances = OptionGroupSingleton<DraftModeOptions>.Instance.UseRoleChances;
            
            if (!useChances)
                return candidates[UnityEngine.Random.Range(0, candidates.Count)];

            int total = candidates.Sum(r => pool.Weights.TryGetValue(r, out var w) ? Math.Max(1, w) : 1);
            if (total <= 0) 
                return candidates[UnityEngine.Random.Range(0, candidates.Count)];

            int roll = UnityEngine.Random.Range(1, total + 1);
            int acc = 0;
            foreach (var id in candidates)
            {
                acc += pool.Weights.TryGetValue(id, out var w) ? Math.Max(1, w) : 1;
                if (roll <= acc) return id;
            }
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private static bool IsCommand(string msg)
        {
            return msg.Equals(Command, StringComparison.OrdinalIgnoreCase);
        }

        private static void ClearChat(ChatController chat)
        {
            chat.freeChatField.Clear();
            chat.quickChatMenu.Clear();
            chat.quickChatField.Clear();
            chat.UpdateChatMode();
        }
    }
}
