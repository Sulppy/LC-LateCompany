using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;

using LateCompany.Core;

using Unity.Netcode;
using GameNetcodeStuff;

using HarmonyLib;
using Steamworks.Data;
using UnityEngine;

namespace LateCompany.Patches;


[HarmonyPatch(typeof(StartOfRound))]
internal class StartOfRoundPatch
{

	private static readonly MethodInfo BeginSendClientRpc =
		typeof(NetworkBehaviour).GetMethod("__beginSendClientRpc", BindingFlags.NonPublic | BindingFlags.Instance);

	private static readonly MethodInfo EndSendClientRpc =
		typeof(NetworkBehaviour).GetMethod("__endSendClientRpc", BindingFlags.NonPublic | BindingFlags.Instance);
	
	private static readonly MethodInfo BeginSendServerRpc =
		typeof(NetworkBehaviour).GetMethod("__beginSendServerRpc", BindingFlags.NonPublic | BindingFlags.Instance);

	private static readonly MethodInfo EndSendServerRpc =
		typeof(NetworkBehaviour).GetMethod("__endSendServerRpc", BindingFlags.NonPublic | BindingFlags.Instance);

	// Best guess at getting new players to load into the map after the game starts.
	[HarmonyPatch("OnPlayerConnectedClientRpc")]
	[HarmonyPostfix]
	private static void OnPlayerConnectedClientRpcPostfix(ulong clientId, int assignedPlayerObjectId, int levelID, int randomSeed)
	{
		StartOfRound sor = StartOfRound.Instance;
		RoundManager rm = RoundManager.Instance;
		
		ClientRpcParams clientRpcParams = new()
		{
			Send = new ClientRpcSendParams()
			{
				TargetClientIds = new List<ulong> { clientId },
			},
		};

		ServerRpcParams serverRpcParams = new()
		{
			Send = new ServerRpcSendParams()
		};
		
		List<PlayerControllerB> allplayers = PJoin.GetAllPlayers();
		PlayerControllerB ply = allplayers[assignedPlayerObjectId];
		PlayerControllerB connectedplayer = StartOfRound.Instance.allPlayerScripts[assignedPlayerObjectId];
		
		if (allplayers.Count + 1 > StartOfRound.Instance.allPlayerScripts.Length && PJoin.LobbyJoinable) PJoin.SetLobbyJoinable(false);
		
		// Make their player model visible.
			
		if (connectedplayer.isPlayerControlled)
		{
			LateCompanyPlugin.logger.LogMessage("Starting sync players");

			try
			{
				StartOfRound.Instance.allPlayerObjects[assignedPlayerObjectId]
					.GetComponentInChildren<PlayerControllerB>().playerUsername = connectedplayer.playerUsername;
				
				ply.DisablePlayerModel(sor.allPlayerObjects[assignedPlayerObjectId], true, true);
				
				PlayerSync.SyncUnlockables(clientRpcParams);

				StartOfRound.Instance.StartTrackingAllPlayerVoices();

				if (sor.IsServer && !sor.inShipPhase)
				{
					GameNetworkManager.Instance.gameHasStarted = true;
					// Tell the new client to generate the level.
					{
						FastBufferWriter fastBufferWriter =
							(FastBufferWriter)BeginSendClientRpc.Invoke(rm,
								new object[] { 1193916134U, clientRpcParams, 0 });
						BytePacker.WriteValueBitPacked(fastBufferWriter, randomSeed);
						BytePacker.WriteValueBitPacked(fastBufferWriter, levelID);
						BytePacker.WriteValueBitPacked(fastBufferWriter,
							(int)rm.currentLevel.currentWeather + 0xFF);
						EndSendClientRpc.Invoke(rm,
							new object[] { fastBufferWriter, 1193916134U, clientRpcParams, 0 });

					}

					// And also tell them that everyone is done generating it.
					{
						FastBufferWriter fastBufferWriter =
							(FastBufferWriter)BeginSendClientRpc.Invoke(rm,
								new object[] { 2729232387U, clientRpcParams, 0 });
						EndSendClientRpc.Invoke(rm,
							new object[] { fastBufferWriter, 2729232387U, clientRpcParams, 0 });
					}
				}

				
				LateCompanyPlugin.logger.LogMessage("Sync successful");
			}
			catch (Exception ex)
			{
				LateCompanyPlugin.logger.LogError($"Sync error: {ex}");
			}
		}
		sor.livingPlayers = PJoin.GetAlivePlayers().Count;
	}

	[HarmonyPatch("OnPlayerDC")]
	[HarmonyWrapSafe]
	[HarmonyPostfix]
	private static void OnPlayerDCPatch()
	{
		if ((StartOfRound.Instance.inShipPhase ||
		    (LateCompanyPlugin.AllowJoiningWhileLanded && StartOfRound.Instance.shipHasLanded)) && !PJoin.LobbyJoinable)
			PJoin.SetLobbyJoinable();
	}

	[HarmonyPatch("SetShipReadyToLand")]
	[HarmonyWrapSafe]
	[HarmonyPostfix]
	private static void SetShipReadyToLandPatch()
	{
		if (StartOfRound.Instance.connectedPlayersAmount + 1 < StartOfRound.Instance.allPlayerScripts.Length && !PJoin.LobbyJoinable)
			PJoin.SetLobbyJoinable();
	}

	[HarmonyPatch("StartGame")]
	[HarmonyWrapSafe]
	[HarmonyPostfix]
	private static void StartGamePatch()
	{
		if(PJoin.LobbyJoinable) PJoin.SetLobbyJoinable(false);
	}

	[HarmonyPatch("OnShipLandedMiscEvents")]
	[HarmonyWrapSafe]
	[HarmonyPostfix]
	private static void OnShipLandedMiscEventsPostfix()
	{
		if (LateCompanyPlugin.AllowJoiningWhileLanded && StartOfRound.Instance.connectedPlayersAmount + 1 <
		    StartOfRound.Instance.allPlayerScripts.Length && !PJoin.LobbyJoinable)
			PJoin.SetLobbyJoinable();
	}
	
	[HarmonyPatch("ShipLeave")]
	[HarmonyWrapSafe]
	[HarmonyPrefix]
	private static void ShipLeavePatch()
	{
		if(PJoin.LobbyJoinable) PJoin.SetLobbyJoinable(false);
	}
	
	[HarmonyPatch("Start")]
	[HarmonyWrapSafe]
	[HarmonyPostfix]
	private static void StartPostfix()
	{
		if(GameNetworkManager.Instance.currentLobby.HasValue) PJoin.Currectlobby = (Lobby)GameNetworkManager.Instance.currentLobby;
	}
}
