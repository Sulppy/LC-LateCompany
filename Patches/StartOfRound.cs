using System;
using System.Reflection;
using System.Collections.Generic;

using Unity.Netcode;
using GameNetcodeStuff;

using HarmonyLib;
using LateCompany.Core;

namespace LateCompany.Patches;


[HarmonyPatch(typeof(StartOfRound))]
internal class StartOfRoundPatch
{

	private static readonly MethodInfo BeginSendClientRpc =
		typeof(RoundManager).GetMethod("__beginSendClientRpc", BindingFlags.NonPublic | BindingFlags.Instance);

	private static readonly MethodInfo EndSendClientRpc =
		typeof(RoundManager).GetMethod("__endSendClientRpc", BindingFlags.NonPublic | BindingFlags.Instance);

	// Best guess at getting new players to load into the map after the game starts.
	[HarmonyPatch("OnPlayerConnectedClientRpc")]
	[HarmonyPostfix]
	private static void OnPlayerConnectedClientRpcPatch(ulong clientId, int assignedPlayerObjectId, int levelID, int randomSeed)
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
		
		
		List<PlayerControllerB> allplayers = PJoin.GetAllPlayers();
		PlayerControllerB ply = allplayers[assignedPlayerObjectId];
		PlayerControllerB connectedplayer = StartOfRound.Instance.allPlayerScripts[assignedPlayerObjectId];
		
		if (allplayers.Count + 1 > sor.allPlayerScripts.Length && PJoin.LobbyJoinable)
			PJoin.SetLobbyJoinable(false);
		
		if (connectedplayer.IsSpawned)
		{
			// Make their player model visible.
			ply.DisablePlayerModel(sor.allPlayerObjects[assignedPlayerObjectId], true, true);
			LateCompanyPlugin.logger.LogInfo("Starting sync players");
			try
			{
				StartOfRound.Instance.allPlayerObjects[assignedPlayerObjectId]
					.GetComponentInChildren<PlayerControllerB>().playerUsername = connectedplayer.playerUsername;
				
				{ 
					FastBufferWriter fastBufferWriter =
						(FastBufferWriter)BeginSendClientRpc.Invoke(sor,
							new object[] { 682230258U, clientRpcParams, 0 });
					BytePacker.WriteValueBitPacked(fastBufferWriter, clientId);
					EndSendClientRpc.Invoke(sor, new object[] { fastBufferWriter, 682230258U, clientRpcParams, 0 });
				}
				
				{ 
					FastBufferWriter fastBufferWriter =
						(FastBufferWriter)BeginSendClientRpc.Invoke(sor,
							new object[] { 4249638645U, clientRpcParams, 0 });
					BytePacker.WriteValueBitPacked(fastBufferWriter, clientId);
					EndSendClientRpc.Invoke(sor, new object[] { fastBufferWriter, 4249638645U, clientRpcParams, 0 });
				}
				
				{ 
					FastBufferWriter fastBufferWriter =
						(FastBufferWriter)BeginSendClientRpc.Invoke(sor,
							new object[] { 744998938U, clientRpcParams, 0 });
					EndSendClientRpc.Invoke(sor, new object[] { fastBufferWriter, 744998938U, clientRpcParams, 0 });
				}
				
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
						BytePacker.WriteValueBitPacked(fastBufferWriter, (int)rm.currentLevel.currentWeather + 0xFF);
						EndSendClientRpc.Invoke(rm, new object[] { fastBufferWriter, 1193916134U, clientRpcParams, 0 });
				
					}

					// And also tell them that everyone is done generating it.
					{ 
						FastBufferWriter fastBufferWriter =
							(FastBufferWriter)BeginSendClientRpc.Invoke(rm,
								new object[] { 2729232387U, clientRpcParams, 0 });
						EndSendClientRpc.Invoke(rm, new object[] { fastBufferWriter, 2729232387U, clientRpcParams, 0 });
					}
				}
				/*sor.LoadUnlockables();
				sor.LoadShipGrabbableItems();*/
				sor.livingPlayers = PJoin.GetAlivePlayers().Count;
				
			}
			catch (Exception ex)
			{
				LateCompanyPlugin.logger.LogError($"Sync error: {ex}");

			}
			LateCompanyPlugin.logger.LogInfo("Sync successful");
		}
	}

	[HarmonyPatch("OnPlayerDC")]
	[HarmonyWrapSafe]
	[HarmonyPostfix]
	private static void OnPlayerDCPatch()
	{
		if ((StartOfRound.Instance.inShipPhase ||
		    (LateCompanyPlugin.AllowJoiningWhileLanded && StartOfRound.Instance.shipHasLanded)) && !PJoin.LobbyJoinable)
			PJoin.SetLobbyJoinable(true);
	}

	[HarmonyPatch("SetShipReadyToLand")]
	[HarmonyWrapSafe]
	[HarmonyPostfix]
	private static void SetShipReadyToLandPatch()
	{
		if (StartOfRound.Instance.connectedPlayersAmount + 1 < StartOfRound.Instance.allPlayerScripts.Length && !PJoin.LobbyJoinable)
			PJoin.SetLobbyJoinable(true);
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
			PJoin.SetLobbyJoinable(true);
	}
	
	[HarmonyPatch("ShipLeave")]
	[HarmonyWrapSafe]
	[HarmonyPrefix]
	private static void ShipLeavePatch()
	{
		if(PJoin.LobbyJoinable) PJoin.SetLobbyJoinable(false);
	}
}
