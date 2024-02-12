using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Unity.Netcode;
using GameNetcodeStuff;


using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;

using HarmonyLib;
using LateCompany.Patches;
using Steamworks.Data;

public static class PluginInfo {
	public const string GUID = "LateCompany.latecompany";
	public const string PrintName = "Late Company";
	public const string Version = "1.0.19";
}


namespace LateCompany
{
	[BepInPlugin(PluginInfo.GUID, PluginInfo.PrintName, PluginInfo.Version)]
	internal class LateCompanyPlugin : BaseUnityPlugin
	{
		private ConfigEntry<bool> configLateJoinOrbitOnly;

		internal static ManualLogSource logger;

		public static bool AllowJoiningWhileLanded = false;

		private static Harmony Patcher;
		
		private void Awake()
		{
			configLateJoinOrbitOnly = Config.Bind("General", "Allow joining while landed", false,
				"Allow players to join while the ship is landed. (Will probably break some things)");
			AllowJoiningWhileLanded = configLateJoinOrbitOnly.Value;
			
			logger = BepInEx.Logging.Logger.CreateLogSource("LateCompany.LateCompany");
			Patcher = new Harmony(PluginInfo.GUID);
			AllowJoiningWhileLanded = configLateJoinOrbitOnly.Value;
			logger.LogInfo($"AllowJoiningWhileLanded is {AllowJoiningWhileLanded}");
			logger.LogInfo("LateCompany loaded. Patching...");
			PatchAll();
			logger.LogInfo("Completed patching.");
		}

		private static void PatchAll()
		{
			Patcher.PatchAll(typeof(LateCompanyPlugin).Assembly);
			Patcher.PatchAll(typeof(StartOfRoundPatch));
			//Patcher.PatchAll(typeof(QuickMenuManagerPatch)); // We don't need this patch, if the lobby is joinable, then the invite friend button turns on itself
			Patcher.PatchAll(typeof(GameNetworkManagerPatch));
			Patcher.PatchAll(typeof(RoundManagerPatch));
		}
		
	}
}

namespace LateCompany.Core
	{

		internal class PJoin : MonoBehaviour
		{
			public static bool LobbyJoinable = true;

			public static Lobby Currectlobby;

			private static bool repeat = false;

			public static void SetLobbyJoinable(bool joinable = true)
			{
				if (GameNetworkManager.Instance.currentLobby.HasValue)
				{
					LobbyJoinable = joinable;
					GameNetworkManager.Instance.SetLobbyJoinable(joinable);
					LateCompanyPlugin.logger.LogMessage(joinable
						? "Lobby set to joinable."
						: "Lobby set to not joinable");
					QuickMenuManager quickMenu = FindObjectOfType<QuickMenuManager>();
					if (quickMenu) quickMenu.inviteFriendsTextAlpha.alpha = joinable ? 1f : 0.2f;
				}
				else if (repeat) LateCompanyPlugin.logger.LogError("Can't set lobby joinable/not joinable");
				else
				{
					GameNetworkManager.Instance.currentLobby = Currectlobby;
					repeat = true;
					SetLobbyJoinable(joinable);
				}

				repeat = false;
			}

			public static List<PlayerControllerB> GetAllPlayers()
			{
				List<PlayerControllerB> list = new List<PlayerControllerB>();
				PlayerControllerB[] allplayerscripts = StartOfRound.Instance.allPlayerScripts;
				foreach (PlayerControllerB playerscripts in allplayerscripts)
				{
					if (playerscripts != null && playerscripts.isPlayerControlled)
					{
						list.Add(playerscripts);
					}
				}

				return list;
			}

			public static List<PlayerControllerB> GetAlivePlayers()
			{
				return (from player in GetAllPlayers()
					where !player.isPlayerDead
					select player).ToList();
			}
		}

		internal class PlayerSync : StartOfRound
		{
			
			private static readonly MethodInfo BeginSendClientRpc =
				typeof(NetworkBehaviour).GetMethod("__beginSendClientRpc", BindingFlags.NonPublic | BindingFlags.Instance);

			private static readonly MethodInfo EndSendClientRpc =
				typeof(NetworkBehaviour).GetMethod("__endSendClientRpc", BindingFlags.NonPublic | BindingFlags.Instance);

			private static StartOfRound sor = StartOfRound.Instance;

			public static PlayerSync Instance;

			public void Awake()
			{
				Instance = this;
			}
			
			public static void SyncUnlockables(ClientRpcParams clientRpcParams)
			{
				try
				{
					int[] playerSuitIDs = new int[4];
					for (int index = 0; index < 4; ++index)
						playerSuitIDs[index] = sor.allPlayerScripts[index].currentSuitID;
					List<int> intList1 = new List<int>();
					List<Vector3> vector3List1 = new List<Vector3>();
					List<Vector3> vector3List2 = new List<Vector3>();
					List<int> intList2 = new List<int>();
					PlaceableShipObject[] array1 = FindObjectsOfType<PlaceableShipObject>().OrderBy(x => x.unlockableID)
						.ToArray();
					for (int index = 0; index < array1.Length; ++index)
					{
						if (index > 175)
						{
							LateCompanyPlugin.logger.LogWarning("Attempted to sync more than 175 unlockables which is not allowed");
							break;
						}
						intList1.Add(array1[index].unlockableID);
						vector3List1.Add(sor.unlockablesList.unlockables[array1[index].unlockableID]
							.placedPosition);
						vector3List2.Add(sor.unlockablesList.unlockables[array1[index].unlockableID]
							.placedRotation);
						if (sor.unlockablesList.unlockables[array1[index].unlockableID].inStorage)
							intList2.Add(array1[index].unlockableID);
					}

					GrabbableObject[] array2 = FindObjectsByType<GrabbableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).OrderBy(x => Vector3.Distance(x.transform.position, Vector3.zero)).ToArray();
					List<int> intList3 = new List<int>();
					List<int> intList4 = new List<int>();
					for (int index = 0; index < array2.Length; ++index)
					{
						if (index > 250)
						{
							LateCompanyPlugin.logger.LogWarning("Attempted to sync more than 250 scrap values which is not allowed");
							break;
						}

						if (array2[index].itemProperties.saveItemVariable)
							intList4.Add(array2[index].GetItemDataToSave());
						if (array2[index].itemProperties.isScrap)
							intList3.Add(array2[index].scrapValue);
					}

					sor.SyncShipUnlockablesClientRpc(playerSuitIDs,
						sor.shipRoomLights.areLightsOn,
						vector3List1.ToArray(), vector3List2.ToArray(), intList1.ToArray(), intList2.ToArray(),
						intList3.ToArray(), intList4.ToArray());
					
					{
						FastBufferWriter bufferWriter =
							(FastBufferWriter)BeginSendClientRpc.Invoke(sor, new object[]{ 4156335180U, clientRpcParams, 0});
						bool flag1 = playerSuitIDs != null;
						bufferWriter.WriteValueSafe(in flag1);
						if (flag1)
							bufferWriter.WriteValueSafe(playerSuitIDs);
						bufferWriter.WriteValueSafe(in sor.shipRoomLights.areLightsOn);
						bool flag2 = vector3List1.ToArray() != null;
						bufferWriter.WriteValueSafe(in flag2);
						if (flag2)
							bufferWriter.WriteValueSafe(vector3List1.ToArray());
						bool flag3 = vector3List2.ToArray() != null;
						bufferWriter.WriteValueSafe(in flag3);
						if (flag3)
							bufferWriter.WriteValueSafe(vector3List2.ToArray());
						bool flag4 = intList1.ToArray() != null;
						bufferWriter.WriteValueSafe(in flag4);
						if (flag4)
							bufferWriter.WriteValueSafe(intList1.ToArray());
						bool flag5 = intList2.ToArray() != null;
						bufferWriter.WriteValueSafe(in flag5);
						if (flag5)
							bufferWriter.WriteValueSafe(intList2.ToArray());
						bool flag6 = intList3.ToArray() != null;
						bufferWriter.WriteValueSafe(in flag6);
						if (flag6)
							bufferWriter.WriteValueSafe(intList3.ToArray());
						bool flag7 = intList4.ToArray() != null;
						bufferWriter.WriteValueSafe(in flag7);
						if (flag7)
							bufferWriter.WriteValueSafe(intList4.ToArray());
						EndSendClientRpc.Invoke(sor, new object[]{bufferWriter, 4156335180U, clientRpcParams, RpcDelivery.Reliable});
					}
				}
				catch (Exception ex)
				{
					Debug.LogError($"Error while syncing unlockables in server. Quitting server: {ex}");
					GameNetworkManager.Instance.disconnectionReasonMessage =
						"An error occured while syncing ship objects! The file may be corrupted. Please report the glitch!";
					GameNetworkManager.Instance.Disconnect();
				}
			}
		}
	}


