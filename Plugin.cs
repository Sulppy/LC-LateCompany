using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using GameNetcodeStuff;


using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;

using HarmonyLib;
using LateCompany.Patches;

public static class PluginInfo {
	public const string GUID = "twig.latecompany";
	public const string PrintName = "Late Company";
	public const string Version = "1.0.15";
}


namespace LateCompany
{
	[BepInPlugin(PluginInfo.GUID, PluginInfo.PrintName, PluginInfo.Version)]
	internal class LateCompanyPlugin : BaseUnityPlugin
	{
		private ConfigEntry<bool> configLateJoinOrbitOnly;

		internal static ManualLogSource logger;

		private static LateCompanyPlugin Instance;

		public static bool AllowJoiningWhileLanded = false;

		private static Harmony Patcher;
		
		private void Awake()
		{
			if (Instance == null)
			{
				Instance = this;
			}
			configLateJoinOrbitOnly = Config.Bind("General", "Allow joining while landed", false,
				"Allow players to join while the ship is landed. (Will probably break some things)");
			AllowJoiningWhileLanded = configLateJoinOrbitOnly.Value;
			
			logger = BepInEx.Logging.Logger.CreateLogSource("LateCompany.LateCompany");
			Patcher = new Harmony(PluginInfo.GUID);
			AllowJoiningWhileLanded = configLateJoinOrbitOnly.Value;
			
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
			public static PJoin Instance;

			public static bool LobbyJoinable = true;
			
			private void Awake()
			{
				if (Instance == null)
				{
					Instance = this;
				}
			}
			
			public static void SetLobbyJoinable(bool joinable)
			{
				if (GameNetworkManager.Instance != null)
				{
					LobbyJoinable = joinable;
					GameNetworkManager.Instance.SetLobbyJoinable(joinable);
					LateCompanyPlugin.logger.LogMessage(joinable ? "Lobby set to joinable." : "Lobby set to not joinable");

					QuickMenuManager quickMenu = FindObjectOfType<QuickMenuManager>();
					if (quickMenu) quickMenu.inviteFriendsTextAlpha.alpha = joinable ? 1f : 0.2f;
				}
				else LateCompanyPlugin.logger.LogError("Lobby is null");
			}
			
			public static void ServerSync(ulong clientId)
			{
				PlayerControllerB connectedplayer = GetThisPlayer(clientId);
				if (connectedplayer.IsSpawned)
				{
					List<PlayerControllerB> allplayers = GetAllPlayers();
					for (int index = 0; index < allplayers.Count; index++)
					{
						if (allplayers[index].playerClientId == connectedplayer.playerClientId)
						{
							LateCompanyPlugin.logger.LogInfo("Starting sync players");
							StartOfRound.Instance.allPlayerScripts[index].playerUsername = connectedplayer.playerUsername;
							StartOfRound.Instance.SyncSuitsServerRpc();
							StartOfRound.Instance.SyncSuitsClientRpc();
							StartOfRound.Instance.PlayerLoadedServerRpc(connectedplayer.playerClientId);
							StartOfRound.Instance.PlayerLoadedClientRpc(connectedplayer.playerClientId);
							StartOfRound.Instance.StartTrackingAllPlayerVoices();
							LateCompanyPlugin.logger.LogInfo("Sync successful");
							break;
						}
					}
				}
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
			
			private static PlayerControllerB GetThisPlayer(ulong clientId)
			{
				int index;
				List<PlayerControllerB> allPlayers = GetAllPlayers();
				for (index = 0; index < GetAllPlayers().Count; index++)
				{
					if (allPlayers[index].playerClientId == clientId)
					{
						return StartOfRound.Instance.allPlayerScripts[index];
					}
				}
			
				LateCompanyPlugin.logger.LogMessage("Player do not found!");
				return StartOfRound.Instance.allPlayerScripts[0];
			}
			
			public static List<PlayerControllerB> GetAlivePlayers()
			{
				return (from player in GetAllPlayers()
					where !player.isPlayerDead
					select player).ToList();
			}
		}
	}


