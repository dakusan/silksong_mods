using UnityEngine;
using HarmonyLib;

namespace NoClip;

//Keep gravity off
[HarmonyPatch(typeof(Rigidbody2D), nameof(Rigidbody2D.gravityScale), MethodType.Setter)]
internal static class Patch_Rigidbody2D_Set_gravityScale
{
	private static bool Prefix(Rigidbody2D __instance, ref float value)
	{
		//Only set gravity to 0 if the player is ready and this is the hero controller rigid body
		if(NCActivate.Self.IsActive && __instance==NCActivate.Self.GetPlayerRigidBody)
			value=0f;
		return true;
	}
}