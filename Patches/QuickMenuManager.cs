using HarmonyLib;
using LateCompany.Core;

namespace LateCompany.Patches;

[HarmonyPatch(typeof(QuickMenuManager))]
internal class QuickMenuManagerPatch {
	[HarmonyPatch("DisableInviteFriendsButton")]
	[HarmonyPrefix]
	private static bool DisableInviteFriendsButtonPrefix()
	{
		return !PJoin.LobbyJoinable;
	}
	
	[HarmonyPatch("InviteFriendsButton")]
	[HarmonyPrefix]
	private static bool InviteFriendsButtonPrefix()
	{
		GameNetworkManager net = GameNetworkManager.Instance;
		if (PJoin.LobbyJoinable) net.InviteFriendsUI();
		return false;
	}
}
