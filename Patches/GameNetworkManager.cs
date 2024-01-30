using Unity.Netcode;

using HarmonyLib;
using LateCompany.Core;

namespace LateCompany.Patches;

[HarmonyPatch(typeof(GameNetworkManager))]
[HarmonyWrapSafe]
internal class GameNetworkManagerPatch {
	[HarmonyPatch("LeaveLobbyAtGameStart")]
	[HarmonyPrefix]
	private static bool LeaveLobbyAtGameStartPrefix() { return false; }
	
	[HarmonyPatch("ConnectionApproval")]
	[HarmonyPostfix]
	private static void ConnectionApprovalPostfix(ref NetworkManager.ConnectionApprovalRequest request, ref NetworkManager.ConnectionApprovalResponse response) {
		if (request.ClientNetworkId == NetworkManager.Singleton.LocalClientId)
			return;
		if (PJoin.LobbyJoinable && response.Reason == "Game has already started!") {
			response.Reason = "";
			response.Approved = true;
		}
	}
}