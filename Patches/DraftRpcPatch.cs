using DraftModeTOUM.Managers;
using DraftModeTOUM.DraftTypes;
using HarmonyLib;
using Hazel;
using System.Collections.Generic;
using System.Linq;

namespace DraftModeTOUM.Patches
{
    public enum DraftRpc : byte
    {
        SubmitPick   = 220,
        AnnounceTurn = 221,
        StartDraft   = 223,
        Recap        = 224,
        SlotNotify   = 225,
        PickerReady  = 226,
        PickConfirmed = 227,
        ForceRole    = 228,
        CancelDraft  = 229,
        EndDraft     = 230,
        BanStart     = 231,
        BanTurn      = 232,
        BanPick      = 233,
        BanEnd       = 234,
        TeamPickStart = 235,
        TeamPickTurn  = 236,
        TeamPickPick  = 237,
        TeamPickEnd   = 238,
        TeamModeStart = 239,
        TeamRoundState = 240,
        TeamRoundEnd = 241,
        TeamScoreUpdate = 242,
        TeamMatchEnd = 243
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
    public static class DraftRpcPatch
    {
        public static bool Prefix(PlayerControl __instance, byte callId, MessageReader reader)
        {
            switch ((DraftRpc)callId)
            {
                case DraftRpc.SubmitPick:
                    if (AmongUsClient.Instance.AmHost)
                        DraftManager.SubmitPick(__instance.PlayerId, reader.ReadByte());
                    return false;

                case DraftRpc.StartDraft:
                    if (!AmongUsClient.Instance.AmHost) HandleStartDraft(reader);
                    else                                ConsumeStartDraftPacket(reader);
                    return false;

                case DraftRpc.AnnounceTurn:
                    if (!AmongUsClient.Instance.AmHost) HandleAnnounceTurn(reader);
                    else                                ConsumeAnnounceTurnPacket(reader);
                    return false;

                case DraftRpc.Recap:
                    if (!AmongUsClient.Instance.AmHost)
                    {
                        bool show = reader.ReadBoolean();
                        if (show)
                        {
                            int count   = reader.ReadInt32();
                            var entries = new List<RecapEntry>();
                            for (int i = 0; i < count; i++)
                            {
                                int    slot = reader.ReadInt32();
                                string role = reader.ReadString(); 
                                entries.Add(new RecapEntry(slot, role));
                            }
                            DraftRecapOverlay.Show(entries);
                        }
                        DraftStatusOverlay.SetState(OverlayState.BackgroundOnly);
                        DraftManager.Reset(cancelledBeforeCompletion: false);
                        DraftManager.TriggerEndDraftSequence();
                    }
                    else
                    {
                        bool show = reader.ReadBoolean();
                        if (show)
                        {
                            int count = reader.ReadInt32();
                            for (int i = 0; i < count; i++) { reader.ReadInt32(); reader.ReadString(); }
                        }
                    }
                    return false;

                case DraftRpc.SlotNotify:
                    if (!AmongUsClient.Instance.AmHost)
                    {
                        int count = reader.ReadInt32();
                        var pids  = new List<byte>();
                        var slots = new List<int>();
                        for (int i = 0; i < count; i++) { pids.Add(reader.ReadByte()); slots.Add(reader.ReadInt32()); }
                        DraftManager.SetDraftStateFromHost(count, pids, slots);
                        DraftUiManager.RefreshTurnList();
                    }
                    else
                    {
                        int count = reader.ReadInt32();
                        for (int i = 0; i < count; i++) { reader.ReadByte(); reader.ReadInt32(); }
                    }
                    return false;

                case DraftRpc.PickConfirmed:
                    if (!AmongUsClient.Instance.AmHost)
                    {
                        int  slot   = reader.ReadInt32();
                        var  roleId = reader.ReadUInt16();
                        var  state  = DraftManager.GetStateForSlot(slot);
                        if (state != null)
                        {
                            state.ChosenRoleId = roleId;
                            state.HasPicked    = true;
                            
                            if (state.PlayerId == PlayerControl.LocalPlayer.PlayerId)
                                DraftStatusOverlay.NotifyLocalPlayerPicked(roleId);
                        }
                    }
                    else
                    {
                        reader.ReadInt32(); reader.ReadUInt16(); 
                    }
                    return false;
                case DraftRpc.PickerReady:
                    
                    if (AmongUsClient.Instance.AmHost)
                        DraftManager.NotifyPickerReady(__instance.PlayerId);
                    return false;

                case DraftRpc.ForceRole:
                    
                    if (AmongUsClient.Instance.AmHost)
                    {
                        string roleName = reader.ReadString();
                        byte targetId   = reader.ReadByte();
                        DraftManager.SetForcedDraftRole(roleName, targetId);
                        LoggingSystem.Debug($"[DraftRpcPatch] Host received ForceRole '{roleName}' for player {targetId}");
                    }
                    return false;

                case DraftRpc.CancelDraft:
                    
                    if (!AmongUsClient.Instance.AmHost)
                    {
                        DraftUiManager.CloseAll();
                        DraftStatusOverlay.SetState(OverlayState.Hidden);
                        DraftManager.Reset(cancelledBeforeCompletion: true);
                    }
                    return false;
                case DraftRpc.EndDraft:
                    
                    DraftManager.Reset(cancelledBeforeCompletion: true);
                    DraftManager.SendChatLocal("<color=#FFD700>Draft has been cancelled by the host.</color>");
                    return false;
                case DraftRpc.BanStart:
                    if (!AmongUsClient.Instance.AmHost) HandleBanStart(reader);
                    else                                ConsumeBanStart(reader);
                    return false;
                case DraftRpc.BanTurn:
                    if (!AmongUsClient.Instance.AmHost) HandleBanTurn(reader);
                    else                                ConsumeBanTurn(reader);
                    return false;
                case DraftRpc.BanPick:
                    if (AmongUsClient.Instance.AmHost)
                    {
                        reader.ReadByte();
                        ushort roleId = reader.ReadUInt16();
                        if (__instance.PlayerId == PlayerControl.LocalPlayer.PlayerId)
                            return false;
                        BanDraftType.HandleBanPickHost(__instance.PlayerId, roleId);
                    }
                    else
                    {
                        byte pickerId = reader.ReadByte();
                        ushort roleId = reader.ReadUInt16();
                        BanDraftType.HandleBanPickLocal(pickerId, roleId);
                    }
                    return false;
                case DraftRpc.BanEnd:
                    BanDraftType.EndBanPhaseLocal();
                    return false;
                case DraftRpc.TeamPickStart:
                    if (!AmongUsClient.Instance.AmHost) HandleTeamPickStart(reader);
                    else ConsumeTeamPickStart(reader);
                    return false;
                case DraftRpc.TeamPickTurn:
                    if (!AmongUsClient.Instance.AmHost) HandleTeamPickTurn(reader);
                    else ConsumeTeamPickTurn(reader);
                    return false;
                case DraftRpc.TeamPickPick:
                    if (AmongUsClient.Instance.AmHost)
                    {
                        byte capId = reader.ReadByte();
                        byte picked = reader.ReadByte();
                        DraftTypes.TeamCaptainDraftType.HandleTeamPickHost(capId, picked);
                    }
                    else
                    {
                        byte capId = reader.ReadByte();
                        byte picked = reader.ReadByte();
                        byte teamId = reader.ReadByte();
                        DraftTypes.TeamCaptainDraftType.HandleTeamPickLocal(capId, picked, teamId);
                    }
                    return false;
                case DraftRpc.TeamPickEnd:
                    DraftTypes.TeamCaptainDraftType.EndTeamPickLocal();
                    return false;
                case DraftRpc.TeamModeStart:
                    if (!AmongUsClient.Instance.AmHost) HandleTeamModeStart(reader);
                    else ConsumeTeamModeStart(reader);
                    return false;
                case DraftRpc.TeamRoundState:
                    if (!AmongUsClient.Instance.AmHost) HandleTeamRoundState(reader);
                    else ConsumeTeamRoundState(reader);
                    return false;
                case DraftRpc.TeamRoundEnd:
                    if (!AmongUsClient.Instance.AmHost) HandleTeamRoundEnd(reader);
                    else ConsumeTeamRoundEnd(reader);
                    return false;
                case DraftRpc.TeamScoreUpdate:
                    if (!AmongUsClient.Instance.AmHost) HandleTeamScoreUpdate(reader);
                    else ConsumeTeamScoreUpdate(reader);
                    return false;
                case DraftRpc.TeamMatchEnd:
                    if (!AmongUsClient.Instance.AmHost) HandleTeamMatchEnd(reader);
                    else ConsumeTeamMatchEnd(reader);
                    return false;

                default:
                    return true;
            }
        }

        private static void ConsumeStartDraftPacket(MessageReader reader)
        {
            int total = reader.ReadInt32();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++) { reader.ReadByte(); reader.ReadInt32(); }
        }

        private static void ConsumeAnnounceTurnPacket(MessageReader reader)
        {
            reader.ReadInt32(); 
            reader.ReadInt32(); 
            reader.ReadByte();  
            int roleCount = reader.ReadInt32();
            for (int i = 0; i < roleCount; i++) reader.ReadUInt16(); 
        }

        private static void HandleStartDraft(MessageReader reader)
        {
            int total = reader.ReadInt32();
            int count = reader.ReadInt32();
            var pids  = new List<byte>();
            var slots = new List<int>();
            for (int i = 0; i < count; i++) { pids.Add(reader.ReadByte()); slots.Add(reader.ReadInt32()); }
            BanDraftType.EndBanPhaseLocal();
            DraftManager.SetDraftStateFromHost(total, pids, slots);
            DraftUiManager.CloseAll();
        }

        private static void HandleAnnounceTurn(MessageReader reader)
        {
            int    turnNumber = reader.ReadInt32();
            int    slot       = reader.ReadInt32();
            byte   pickerId   = reader.ReadByte();
            int    roleCount  = reader.ReadInt32();
            var    roleIds    = new ushort[roleCount];
            for (int i = 0; i < roleCount; i++) roleIds[i] = reader.ReadUInt16();

            DraftManager.SetClientTurn(turnNumber, slot);
            DisplayTurnAnnouncement(slot, pickerId, roleIds);
        }

        private static void HandleBanStart(MessageReader reader)
        {
            bool showBanned = reader.ReadBoolean();
            bool anonymous  = reader.ReadBoolean();
            int count       = reader.ReadInt32();
            var pids = new List<byte>();
            for (int i = 0; i < count; i++) pids.Add(reader.ReadByte());
            BanDraftType.HandleBanStartLocal(pids, showBanned, anonymous);
        }

        private static void ConsumeBanStart(MessageReader reader)
        {
            reader.ReadBoolean();
            reader.ReadBoolean();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++) reader.ReadByte();
        }

        private static void HandleBanTurn(MessageReader reader)
        {
            byte pickerId  = reader.ReadByte();
            int  index     = reader.ReadInt32();
            int  total     = reader.ReadInt32();
            int  roleCount = reader.ReadInt32();
            var roles = new List<ushort>();
            for (int i = 0; i < roleCount; i++) roles.Add(reader.ReadUInt16());
            BanDraftType.HandleBanTurnLocal(pickerId, roles, index, total);
        }

        private static void HandleTeamPickStart(MessageReader reader)
        {
            int teamCount = reader.ReadInt32();
            var captains = new List<byte>();
            for (int i = 0; i < teamCount; i++) captains.Add(reader.ReadByte());

            int memberCount = reader.ReadInt32();
            var teams = new Dictionary<byte, List<byte>>();
            for (int i = 0; i < memberCount; i++)
            {
                byte pid = reader.ReadByte();
                byte teamId = reader.ReadByte();
                if (!teams.ContainsKey(teamId)) teams[teamId] = new List<byte>();
                teams[teamId].Add(pid);
            }

            int availCount = reader.ReadInt32();
            var available = new List<byte>();
            for (int i = 0; i < availCount; i++) available.Add(reader.ReadByte());

            DraftTypes.TeamCaptainDraftType.HandleTeamPickStartLocal(captains, teams, available);
        }

        private static void ConsumeTeamPickStart(MessageReader reader)
        {
            int teamCount = reader.ReadInt32();
            for (int i = 0; i < teamCount; i++) reader.ReadByte();
            int memberCount = reader.ReadInt32();
            for (int i = 0; i < memberCount; i++) { reader.ReadByte(); reader.ReadByte(); }
            int availCount = reader.ReadInt32();
            for (int i = 0; i < availCount; i++) reader.ReadByte();
        }

        private static void HandleTeamPickTurn(MessageReader reader)
        {
            byte captainId = reader.ReadByte();
            int availCount = reader.ReadInt32();
            var available = new List<byte>();
            for (int i = 0; i < availCount; i++) available.Add(reader.ReadByte());

            TeamCaptainPickOverlay.UpdateTeams(
                DraftTypes.TeamCaptainDraftType.TeamMembers.ToDictionary(k => k.Key, v => v.Value.ToList()),
                available);
            TeamCaptainPickOverlay.SetCurrentCaptain(captainId);
        }

        private static void ConsumeTeamPickTurn(MessageReader reader)
        {
            reader.ReadByte();
            int availCount = reader.ReadInt32();
            for (int i = 0; i < availCount; i++) reader.ReadByte();
        }

        private static void HandleTeamModeStart(MessageReader reader)
        {
            int count = reader.ReadInt32();
            var teamMap = new Dictionary<byte, byte>();
            for (int i = 0; i < count; i++)
            {
                byte pid = reader.ReadByte();
                byte team = reader.ReadByte();
                teamMap[pid] = team;
            }

            int totalCount = reader.ReadInt32();
            var totalScores = new Dictionary<byte, int>();
            for (int i = 0; i < totalCount; i++)
            {
                byte team = reader.ReadByte();
                int score = reader.ReadInt32();
                totalScores[team] = score;
            }

            int roundCount = reader.ReadInt32();
            var roundScores = new Dictionary<byte, int>();
            for (int i = 0; i < roundCount; i++)
            {
                byte team = reader.ReadByte();
                int score = reader.ReadInt32();
                roundScores[team] = score;
            }

            int round = reader.ReadInt32();
            int totalRounds = reader.ReadInt32();
            int prep = reader.ReadInt32();
            int roundMinutes = reader.ReadInt32();

            DraftTypes.TeamCaptainDraftType.HandleTeamModeStartLocal(teamMap, totalScores, roundScores, round, totalRounds, prep, roundMinutes);
        }

        private static void ConsumeTeamModeStart(MessageReader reader)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++) { reader.ReadByte(); reader.ReadByte(); }
            int totalCount = reader.ReadInt32();
            for (int i = 0; i < totalCount; i++) { reader.ReadByte(); reader.ReadInt32(); }
            int roundCount = reader.ReadInt32();
            for (int i = 0; i < roundCount; i++) { reader.ReadByte(); reader.ReadInt32(); }
            reader.ReadInt32();
            reader.ReadInt32();
            reader.ReadInt32();
            reader.ReadInt32();
        }

        private static void HandleTeamRoundState(MessageReader reader)
        {
            var state = (DraftTypes.TeamRoundState)reader.ReadByte();
            int round = reader.ReadInt32();
            float roundTimer = reader.ReadSingle();
            float prepTimer = reader.ReadSingle();

            int totalCount = reader.ReadInt32();
            var totalScores = new Dictionary<byte, int>();
            for (int i = 0; i < totalCount; i++)
            {
                byte team = reader.ReadByte();
                int score = reader.ReadInt32();
                totalScores[team] = score;
            }

            int roundCount = reader.ReadInt32();
            var roundScores = new Dictionary<byte, int>();
            for (int i = 0; i < roundCount; i++)
            {
                byte team = reader.ReadByte();
                int score = reader.ReadInt32();
                roundScores[team] = score;
            }

            DraftTypes.TeamCaptainDraftType.HandleTeamRoundStateLocal(state, round, roundTimer, prepTimer, totalScores, roundScores);
        }

        private static void ConsumeTeamRoundState(MessageReader reader)
        {
            reader.ReadByte();
            reader.ReadInt32();
            reader.ReadSingle();
            reader.ReadSingle();
            int totalCount = reader.ReadInt32();
            for (int i = 0; i < totalCount; i++) { reader.ReadByte(); reader.ReadInt32(); }
            int roundCount = reader.ReadInt32();
            for (int i = 0; i < roundCount; i++) { reader.ReadByte(); reader.ReadInt32(); }
        }

        private static void HandleTeamRoundEnd(MessageReader reader)
        {
            byte winner = reader.ReadByte();
            int round = reader.ReadInt32();

            int totalCount = reader.ReadInt32();
            var totalScores = new Dictionary<byte, int>();
            for (int i = 0; i < totalCount; i++)
            {
                byte team = reader.ReadByte();
                int score = reader.ReadInt32();
                totalScores[team] = score;
            }

            int roundCount = reader.ReadInt32();
            var roundScores = new Dictionary<byte, int>();
            for (int i = 0; i < roundCount; i++)
            {
                byte team = reader.ReadByte();
                int score = reader.ReadInt32();
                roundScores[team] = score;
            }

            DraftTypes.TeamCaptainDraftType.HandleTeamRoundEndLocal(winner, round, totalScores, roundScores);
        }

        private static void ConsumeTeamRoundEnd(MessageReader reader)
        {
            reader.ReadByte();
            reader.ReadInt32();
            int totalCount = reader.ReadInt32();
            for (int i = 0; i < totalCount; i++) { reader.ReadByte(); reader.ReadInt32(); }
            int roundCount = reader.ReadInt32();
            for (int i = 0; i < roundCount; i++) { reader.ReadByte(); reader.ReadInt32(); }
        }

        private static void HandleTeamScoreUpdate(MessageReader reader)
        {
            int totalCount = reader.ReadInt32();
            var totalScores = new Dictionary<byte, int>();
            for (int i = 0; i < totalCount; i++)
            {
                byte team = reader.ReadByte();
                int score = reader.ReadInt32();
                totalScores[team] = score;
            }

            int roundCount = reader.ReadInt32();
            var roundScores = new Dictionary<byte, int>();
            for (int i = 0; i < roundCount; i++)
            {
                byte team = reader.ReadByte();
                int score = reader.ReadInt32();
                roundScores[team] = score;
            }

            DraftTypes.TeamCaptainDraftType.HandleTeamScoreUpdateLocal(totalScores, roundScores);
        }

        private static void ConsumeTeamScoreUpdate(MessageReader reader)
        {
            int totalCount = reader.ReadInt32();
            for (int i = 0; i < totalCount; i++) { reader.ReadByte(); reader.ReadInt32(); }
            int roundCount = reader.ReadInt32();
            for (int i = 0; i < roundCount; i++) { reader.ReadByte(); reader.ReadInt32(); }
        }

        private static void HandleTeamMatchEnd(MessageReader reader)
        {
            byte winner = reader.ReadByte();
            int totalCount = reader.ReadInt32();
            var totalScores = new Dictionary<byte, int>();
            for (int i = 0; i < totalCount; i++)
            {
                byte team = reader.ReadByte();
                int score = reader.ReadInt32();
                totalScores[team] = score;
            }
            DraftTypes.TeamCaptainDraftType.HandleTeamMatchEndLocal(winner, totalScores);
        }

        private static void ConsumeTeamMatchEnd(MessageReader reader)
        {
            reader.ReadByte();
            int totalCount = reader.ReadInt32();
            for (int i = 0; i < totalCount; i++) { reader.ReadByte(); reader.ReadInt32(); }
        }

        private static void ConsumeBanTurn(MessageReader reader)
        {
            reader.ReadByte();
            reader.ReadInt32();
            reader.ReadInt32();
            int roleCount = reader.ReadInt32();
            for (int i = 0; i < roleCount; i++) reader.ReadUInt16();
        }

        public static void HandleAnnounceTurnLocal(int slot, byte pickerId, List<ushort> roleIds)
        {
            DisplayTurnAnnouncement(slot, pickerId, roleIds.ToArray());
        }

        private static void DisplayTurnAnnouncement(int slot, byte pickerId, ushort[] roleIds)
        {
            byte localId = PlayerControl.LocalPlayer.PlayerId;
            if (localId == pickerId)
            {
                DraftUiManager.ShowPicker(roleIds.ToList());
            }
            else
            {
                var localState = DraftManager.GetStateForPlayer(localId);
                if (localState == null || !localState.IsPickingNow)
                    DraftUiManager.CloseAll();
            }
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerLeft))]
    public static class PlayerLeftDraftPatch
    {
        public static void Postfix(AmongUsClient __instance, InnerNet.ClientData data)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (!DraftManager.IsDraftActive) return;
            if (data?.Character == null) return;
            byte dcPlayerId = data.Character.PlayerId;
            DraftModePlugin.Logger.LogInfo($"[DraftManager] Player {dcPlayerId} disconnected during draft");

            DraftManager.HandlePlayerDisconnected(dcPlayerId);
        }
    }

    public static class DraftNetworkHelper
    {
        public static void SendPickToHost(int index)
        {
            DraftUiManager.CloseAll();
            if (AmongUsClient.Instance.AmHost)
            {
                DraftManager.SubmitPick(PlayerControl.LocalPlayer.PlayerId, index);
            }
            else
            {
                var writer = AmongUsClient.Instance.StartRpcImmediately(
                    PlayerControl.LocalPlayer.NetId,
                    (byte)DraftRpc.SubmitPick,
                    Hazel.SendOption.Reliable,
                    AmongUsClient.Instance.HostId);
                writer.Write((byte)index);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }
        }

        public static void BroadcastDraftStart(int totalSlots, List<byte> pids, List<int> slots)
        {
            DraftManager.SetDraftStateFromHost(totalSlots, pids, slots);
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.StartDraft,
                Hazel.SendOption.Reliable, -1);
            writer.Write(totalSlots);
            writer.Write(pids.Count);
            for (int i = 0; i < pids.Count; i++) { writer.Write(pids[i]); writer.Write(slots[i]); }
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void SendTurnAnnouncement(int slot, byte playerId, List<ushort> roleIds, int turnNumber)
        {
            DraftRpcPatch.HandleAnnounceTurnLocal(slot, playerId, roleIds);

            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.AnnounceTurn,
                Hazel.SendOption.Reliable, -1);
            writer.Write(turnNumber);
            writer.Write(slot);
            writer.Write(playerId);
            writer.Write(roleIds.Count);
            foreach (var id in roleIds) writer.Write(id);  
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void BroadcastSlotNotifications(Dictionary<byte, int> pidToSlot)
        {
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.SlotNotify,
                Hazel.SendOption.Reliable, -1);
            writer.Write(pidToSlot.Count);
            foreach (var kvp in pidToSlot) { writer.Write(kvp.Key); writer.Write(kvp.Value); }
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void BroadcastPickConfirmed(int slot, ushort roleId)
        {
            
            var state = DraftManager.GetStateForSlot(slot);
            if (state != null)
            {
                state.ChosenRoleId = roleId;
                state.HasPicked    = true;
                
                if (state.PlayerId == PlayerControl.LocalPlayer.PlayerId)
                    DraftStatusOverlay.NotifyLocalPlayerPicked(roleId);
            }

            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.PickConfirmed,
                Hazel.SendOption.Reliable, -1);
            writer.Write(slot);
            writer.Write(roleId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void NotifyPickerReady()
        {
            if (AmongUsClient.Instance.AmHost)
            {
                
                DraftManager.NotifyPickerReady(PlayerControl.LocalPlayer.PlayerId);
            }
            else
            {
                var writer = AmongUsClient.Instance.StartRpcImmediately(
                    PlayerControl.LocalPlayer.NetId,
                    (byte)DraftRpc.PickerReady,
                    Hazel.SendOption.Reliable,
                    AmongUsClient.Instance.HostId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }
        }

        
        
        
        
        
        public static void BroadcastBanStart(List<byte> order, bool showBannedRoles, bool anonymousUsers)
        {
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.BanStart,
                Hazel.SendOption.Reliable, -1);
            writer.Write(showBannedRoles);
            writer.Write(anonymousUsers);
            writer.Write(order.Count);
            foreach (var pid in order) writer.Write(pid);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void BroadcastBanTurn(byte pickerId, List<ushort> roleIds, int index, int total)
        {
            BanDraftType.HandleBanTurnLocal(pickerId, roleIds, index, total);
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.BanTurn,
                Hazel.SendOption.Reliable, -1);
            writer.Write(pickerId);
            writer.Write(index);
            writer.Write(total);
            writer.Write(roleIds.Count);
            foreach (var id in roleIds) writer.Write(id);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void SendBanPickToHost(ushort roleId)
        {
            if (AmongUsClient.Instance.AmHost)
            {
                BanDraftType.HandleBanPickHost(PlayerControl.LocalPlayer.PlayerId, roleId);
            }
            else
            {
                var writer = AmongUsClient.Instance.StartRpcImmediately(
                    PlayerControl.LocalPlayer.NetId,
                    (byte)DraftRpc.BanPick,
                    Hazel.SendOption.Reliable,
                    AmongUsClient.Instance.HostId);
                writer.Write(PlayerControl.LocalPlayer.PlayerId);
                writer.Write(roleId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }
        }
        public static void SendTeamPickToHost(byte pickedPlayerId)
        {
            if (AmongUsClient.Instance.AmHost)
            {
                DraftTypes.TeamCaptainDraftType.HandleTeamPickHost(PlayerControl.LocalPlayer.PlayerId, pickedPlayerId);
            }
            else
            {
                var writer = AmongUsClient.Instance.StartRpcImmediately(
                    PlayerControl.LocalPlayer.NetId,
                    (byte)DraftRpc.TeamPickPick,
                    Hazel.SendOption.Reliable,
                    AmongUsClient.Instance.HostId);
                writer.Write(PlayerControl.LocalPlayer.PlayerId);
                writer.Write(pickedPlayerId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }
        }
        public static void BroadcastBanPick(byte pickerId, ushort roleId)
        {
            BanDraftType.HandleBanPickLocal(pickerId, roleId);
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.BanPick,
                Hazel.SendOption.Reliable, -1);
            writer.Write(pickerId);
            writer.Write(roleId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void BroadcastBanEnd()
        {
            BanDraftType.EndBanPhaseLocal();
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.BanEnd,
                Hazel.SendOption.Reliable, -1);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void BroadcastTeamPickStart(List<byte> captains, Dictionary<byte, List<byte>> teams, List<byte> available)
        {
            TeamCaptainPickOverlay.Show(captains, teams, available);
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.TeamPickStart,
                Hazel.SendOption.Reliable, -1);
            writer.Write(captains.Count);
            foreach (var c in captains) writer.Write(c);
            int memberCount = teams.Sum(k => k.Value.Count);
            writer.Write(memberCount);
            foreach (var kvp in teams)
                foreach (var pid in kvp.Value)
                {
                    writer.Write(pid);
                    writer.Write(kvp.Key);
                }
            writer.Write(available.Count);
            foreach (var pid in available) writer.Write(pid);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void BroadcastTeamPickTurn(byte captainId, List<byte> available)
        {
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.TeamPickTurn,
                Hazel.SendOption.Reliable, -1);
            writer.Write(captainId);
            writer.Write(available.Count);
            foreach (var pid in available) writer.Write(pid);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void BroadcastTeamPickMade(byte captainId, byte pickedPlayerId, byte teamId)
        {
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.TeamPickPick,
                Hazel.SendOption.Reliable, -1);
            writer.Write(captainId);
            writer.Write(pickedPlayerId);
            writer.Write(teamId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void BroadcastTeamPickEnd()
        {
            TeamCaptainPickOverlay.Hide();
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.TeamPickEnd,
                Hazel.SendOption.Reliable, -1);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void BroadcastTeamModeStart(Dictionary<byte, byte> playerTeams, Dictionary<byte, int> totalScores, Dictionary<byte, int> roundScores, int round, int totalRounds, int prepSeconds, int roundMinutes)
        {
            if (!AmongUsClient.Instance.AmHost)
                DraftTypes.TeamCaptainDraftType.HandleTeamModeStartLocal(playerTeams, totalScores, roundScores, round, totalRounds, prepSeconds, roundMinutes);
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.TeamModeStart,
                Hazel.SendOption.Reliable, -1);
            writer.Write(playerTeams.Count);
            foreach (var kvp in playerTeams) { writer.Write(kvp.Key); writer.Write(kvp.Value); }
            writer.Write(totalScores.Count);
            foreach (var kvp in totalScores) { writer.Write(kvp.Key); writer.Write(kvp.Value); }
            writer.Write(roundScores.Count);
            foreach (var kvp in roundScores) { writer.Write(kvp.Key); writer.Write(kvp.Value); }
            writer.Write(round);
            writer.Write(totalRounds);
            writer.Write(prepSeconds);
            writer.Write(roundMinutes);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void BroadcastTeamRoundState(DraftTypes.TeamRoundState state, int round, float roundTimer, float prepTimer, Dictionary<byte, int> totalScores, Dictionary<byte, int> roundScores)
        {
            DraftTypes.TeamCaptainDraftType.HandleTeamRoundStateLocal(state, round, roundTimer, prepTimer, totalScores, roundScores);
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.TeamRoundState,
                Hazel.SendOption.Reliable, -1);
            writer.Write((byte)state);
            writer.Write(round);
            writer.Write(roundTimer);
            writer.Write(prepTimer);
            writer.Write(totalScores.Count);
            foreach (var kvp in totalScores) { writer.Write(kvp.Key); writer.Write(kvp.Value); }
            writer.Write(roundScores.Count);
            foreach (var kvp in roundScores) { writer.Write(kvp.Key); writer.Write(kvp.Value); }
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void BroadcastTeamRoundEnd(byte winnerTeam, int round, Dictionary<byte, int> totalScores, Dictionary<byte, int> roundScores)
        {
            DraftTypes.TeamCaptainDraftType.HandleTeamRoundEndLocal(winnerTeam, round, totalScores, roundScores);
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.TeamRoundEnd,
                Hazel.SendOption.Reliable, -1);
            writer.Write(winnerTeam);
            writer.Write(round);
            writer.Write(totalScores.Count);
            foreach (var kvp in totalScores) { writer.Write(kvp.Key); writer.Write(kvp.Value); }
            writer.Write(roundScores.Count);
            foreach (var kvp in roundScores) { writer.Write(kvp.Key); writer.Write(kvp.Value); }
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void BroadcastTeamScoreUpdate(Dictionary<byte, int> totalScores, Dictionary<byte, int> roundScores)
        {
            DraftTypes.TeamCaptainDraftType.HandleTeamScoreUpdateLocal(totalScores, roundScores);
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.TeamScoreUpdate,
                Hazel.SendOption.Reliable, -1);
            writer.Write(totalScores.Count);
            foreach (var kvp in totalScores) { writer.Write(kvp.Key); writer.Write(kvp.Value); }
            writer.Write(roundScores.Count);
            foreach (var kvp in roundScores) { writer.Write(kvp.Key); writer.Write(kvp.Value); }
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void BroadcastTeamMatchEnd(byte winnerTeam, Dictionary<byte, int> totalScores)
        {
            DraftTypes.TeamCaptainDraftType.HandleTeamMatchEndLocal(winnerTeam, totalScores);
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.TeamMatchEnd,
                Hazel.SendOption.Reliable, -1);
            writer.Write(winnerTeam);
            writer.Write(totalScores.Count);
            foreach (var kvp in totalScores) { writer.Write(kvp.Key); writer.Write(kvp.Value); }
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void SendForceRoleToHost(string roleName, byte targetId)
        {
            byte myId = PlayerControl.LocalPlayer.PlayerId;
            if (AmongUsClient.Instance.AmHost)
            {
                DraftManager.SetForcedDraftRole(roleName, targetId);
            }
            else
            {
                var writer = AmongUsClient.Instance.StartRpcImmediately(
                    PlayerControl.LocalPlayer.NetId,
                    (byte)DraftRpc.ForceRole,
                    Hazel.SendOption.Reliable,
                    AmongUsClient.Instance.HostId);
                writer.Write(roleName);
                writer.Write(targetId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }
        }

        public static void BroadcastCancelDraft()
        {
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.CancelDraft,
                Hazel.SendOption.Reliable, -1);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void BroadcastRecap(List<RecapEntry> entries, bool showRecap)
        {
            if (showRecap) DraftRecapOverlay.Show(entries);
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.Recap,
                Hazel.SendOption.Reliable, -1);
            writer.Write(showRecap);
            if (showRecap)
            {
                writer.Write(entries.Count);
                foreach (var e in entries) { writer.Write(e.SlotNumber); writer.Write(e.RoleName); }
            }
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void BroadcastDraftEnd()
        {
            
            DraftManager.Reset(cancelledBeforeCompletion: true);
            DraftManager.SendChatLocal("<color=#FFD700>Draft has been cancelled by the host.</color>");

            
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)DraftRpc.EndDraft,
                Hazel.SendOption.Reliable, -1);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }
}








