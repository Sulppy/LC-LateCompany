using System;
using System.Reflection;
using System.Collections.Generic;

using LateCompany.Core;

using Unity.Netcode;
using GameNetcodeStuff;

using HarmonyLib;
using Steamworks.Data;

namespace LateCompany.Patches;


[HarmonyPatch(typeof(StartOfRound))]
internal class StartOfRoundPatch
{
	private static readonly MethodInfo BeginSendClientRpc =
		typeof(NetworkBehaviour).GetMethod("__beginSendClientRpc", BindingFlags.NonPublic | BindingFlags.Instance);

	private static readonly MethodInfo EndSendClientRpc =
		typeof(NetworkBehaviour).GetMethod("__endSendClientRpc", BindingFlags.NonPublic | BindingFlags.Instance);

	// Best guess at getting new players to load into the map after the game starts.
	[HarmonyPatch("OnPlayerConnectedClientRpc")]
	[HarmonyPostfix]
	private static void OnPlayerConnectedClientRpcPostfix(ulong clientId, int assignedPlayerObjectId, int levelID, int randomSeed)
	{
		if (!NetworkManager.Singleton.IsHost) return;
		StartOfRound sor = StartOfRound.Instance;
		RoundManager rm = RoundManager.Instance;

		ClientRpcParams clientRpcParams = new()
		{
			Send = new ClientRpcSendParams()
			{
				TargetClientIds = new List<ulong> { clientId },
			},
		};

		List<PlayerControllerB> allplayers = PJoin.GetAllPlayers();
		PlayerControllerB ply = allplayers[assignedPlayerObjectId];
		PlayerControllerB connectedplayer = StartOfRound.Instance.allPlayerScripts[assignedPlayerObjectId];

		if (allplayers.Count + 1 > StartOfRound.Instance.allPlayerScripts.Length && PJoin.LobbyJoinable)
			PJoin.SetLobbyJoinable(false);

		LateCompanyPlugin.logger.LogMessage("Starting sync player");

		try
		{

			StartOfRound.Instance.allPlayerObjects[assignedPlayerObjectId]
				.GetComponentInChildren<PlayerControllerB>().playerUsername = connectedplayer.playerUsername;

			// Make their player model visible.
			ply.DisablePlayerModel(sor.allPlayerObjects[assignedPlayerObjectId], true, true);

			// Tell new client sync suits
			{
				FastBufferWriter fastBufferWriter =
					(FastBufferWriter)BeginSendClientRpc.Invoke(sor,
						[2369901769U, clientRpcParams, 0]);
				EndSendClientRpc.Invoke(sor,
					[fastBufferWriter, 2369901769U, clientRpcParams, 0]);
			}

			PlayerSync.SyncUnlockables(clientRpcParams);

			StartOfRound.Instance.StartTrackingAllPlayerVoices();

			if (sor.IsServer && !sor.inShipPhase)
			{
				GameNetworkManager.Instance.gameHasStarted = true;
				// Tell the new client to generate the level.
				{
					FastBufferWriter fastBufferWriter =
						(FastBufferWriter)BeginSendClientRpc.Invoke(rm,
							[1193916134U, clientRpcParams, 0]);
					BytePacker.WriteValueBitPacked(fastBufferWriter, randomSeed);
					BytePacker.WriteValueBitPacked(fastBufferWriter, levelID);
					BytePacker.WriteValueBitPacked(fastBufferWriter,
						(int)rm.currentLevel.currentWeather + 0xFF);
					EndSendClientRpc.Invoke(rm,
						[fastBufferWriter, 1193916134U, clientRpcParams, 0]);

				}

				// And also tell them that everyone is done generating it.
				{
					FastBufferWriter fastBufferWriter =
						(FastBufferWriter)BeginSendClientRpc.Invoke(rm,
							[2729232387U, clientRpcParams, 0]);
					EndSendClientRpc.Invoke(rm,
						[fastBufferWriter, 2729232387U, clientRpcParams, 0]);
				}
			}


			LateCompanyPlugin.logger.LogMessage("Sync successful");
		}
		catch (Exception ex)
		{
			LateCompanyPlugin.logger.LogError($"Sync error: {ex}");
		}

		sor.livingPlayers = PJoin.GetAlivePlayers().Count;
	}


	[HarmonyPatch("OnPlayerDC")]
	[HarmonyPostfix]
	private static void OnPlayerDCPatch()
	{
		if ((StartOfRound.Instance.inShipPhase ||
		    (LateCompanyPlugin.AllowJoiningWhileLanded && StartOfRound.Instance.shipHasLanded)) && !PJoin.LobbyJoinable)
			PJoin.SetLobbyJoinable();
	}

	[HarmonyPatch("SetShipReadyToLand")]
	[HarmonyPostfix]
	private static void SetShipReadyToLandPatch()
	{
		if (StartOfRound.Instance.connectedPlayersAmount + 1 < StartOfRound.Instance.allPlayerScripts.Length && !PJoin.LobbyJoinable)
			PJoin.SetLobbyJoinable();
	}

	[HarmonyPatch("StartGame")]
	[HarmonyPostfix]
	private static void StartGamePatch()
	{
		if(PJoin.LobbyJoinable) PJoin.SetLobbyJoinable(false);
	}

	[HarmonyPatch("OnShipLandedMiscEvents")]
	[HarmonyPostfix]
	private static void OnShipLandedMiscEventsPostfix()
	{
		if (LateCompanyPlugin.AllowJoiningWhileLanded && StartOfRound.Instance.connectedPlayersAmount + 1 <
		    StartOfRound.Instance.allPlayerScripts.Length && !PJoin.LobbyJoinable)
			PJoin.SetLobbyJoinable();
	}
	
	[HarmonyPatch("ShipLeave")]
	[HarmonyPrefix]
	private static void ShipLeavePatch()
	{
		if(PJoin.LobbyJoinable) PJoin.SetLobbyJoinable(false);
	}
	
	[HarmonyPatch("Start")]
	[HarmonyPostfix]
	private static void StartPostfix()
	{
		if (!NetworkManager.Singleton.IsHost) return;
		if(GameNetworkManager.Instance.currentLobby.HasValue) PJoin.Currectlobby = (Lobby)GameNetworkManager.Instance.currentLobby;
	}
}
