using System.Diagnostics.CodeAnalysis;
using Comfort.Common;
using EFT.Ballistics;
using EFT.InventoryLogic;
using EFT.Trainer.Configuration;
using EFT.Trainer.Extensions;
using EFT.Trainer.Properties;
using EFT.Trainer.UI;
using JetBrains.Annotations;
using UnityEngine;

#nullable enable

namespace EFT.Trainer.Features;

internal enum AimTargetPart
{
	Head,
	Chest,
	Stomach,
	Arms,
	Legs
}

[UsedImplicitly]
internal class Aimbot : HoldFeature
{
	public override string Name => Strings.FeatureAimbotName;
	public override string Description => Strings.FeatureAimbotDescription;

	public override KeyCode Key { get; set; } = KeyCode.Slash;

	[ConfigurationProperty(Order = 10)]
	public float MaximumDistance { get; set; } = 200f;

	[ConfigurationProperty(Order = 11)]
	public float Smoothness { get; set; } = 0.35f;

	[ConfigurationProperty(Order = 12)]
	public AimTargetPart TargetPart { get; set; } = AimTargetPart.Head;

	[ConfigurationProperty(Order = 13)]
	public bool PreferHeadChest { get; set; } = true;

	[ConfigurationProperty(Order = 14)]
	public bool VisibleOnly { get; set; } = true;

	[ConfigurationProperty(Order = 15)]
	public bool ElevationAdjustment { get; set; } = true;

	[ConfigurationProperty(Order = 20)]
	public float FovRadius { get; set; } = 0f;

	[ConfigurationProperty(Order = 21)]
	public bool ShowFovCircle { get; set; } = false;

	[ConfigurationProperty(Order = 22)]
	public Color FovCircleColor { get; set; } = Color.white;

	[ConfigurationProperty(Order = 23)]
	public float FovCircleThickness { get; set; } = 1f;

	[ConfigurationProperty(Order = 30)]
	public bool SilentAim { get; set; } = false;

	[ConfigurationProperty(Order = 31)]
	public float SilentAimNextShotDelay { get; set; } = 0.25f;

	[ConfigurationProperty(Order = 32)]
	public float SilentAimSpeedFactor { get; set; } = 100f;

#pragma warning disable IDE0060
	[UsedImplicitly]
	protected static bool CreateShotPrefix(object ammo, Vector3 origin, ref Vector3 direction, int fireIndex, string player, Item weapon, ref float speedFactor, int fragmentIndex)
	{
		var feature = FeatureFactory.GetFeature<Aimbot>();
		if (feature == null || !feature.SilentAim || !feature._hasSilentAimTarget)
			return true; // keep using original code, we are not enabled

		var world = Singleton<GameWorld>.Instance;
		if (world == null)
			return true;

		var localPlayer = world.GetEverExistedBridgeByProfileID(player)?.iPlayer;
		if (localPlayer == null)
			return true;

		if (!localPlayer.IsYourPlayer)
			return true; // keep using original code, not our player

		direction = (feature._silentAimTargetPosition - origin).normalized;
		speedFactor = feature.SilentAimSpeedFactor;

		return true; // call the original code with updated direction and speedFactor
	}

	[UsedImplicitly]
	[SuppressMessage("ReSharper", "InconsistentNaming")]
	protected static bool ApplyShotPrefix(DamageInfoStruct damageInfo, EBodyPart bodyPartType, EBodyPartColliderType colliderType, EArmorPlateCollider armorPlateCollider, object shotId, Player? __instance)
	{
		var feature = FeatureFactory.GetFeature<Aimbot>();
		if (feature == null || !feature.SilentAim || !feature._hasSilentAimTarget)
			return true; // keep using original code, we are not enabled

		if (damageInfo.Player?.iPlayer is { IsYourPlayer: true } && __instance is { IsYourPlayer: true })
			return false; // prevent self damage !

		return true; // call the original code
	}

	[UsedImplicitly]
	[SuppressMessage("ReSharper", "InconsistentNaming")]
	protected static bool CreateOpticCalibrationDataPrefix(Weapon? __instance, ref AmmoTemplate ammoTemplate)
	{
		var feature = FeatureFactory.GetFeature<Aimbot>();
		if (feature == null || !feature.ElevationAdjustment)
			return true; // keep using original code, we are not enabled

		// Taken from SPT-BetterZeroing, credits to ehaugw
		// Use the loaded ammo for elevation adjustment calculations rather than a weapon's default ammo
		if (__instance?.GetCurrentMagazine() is { } mag && mag.FirstRealAmmo() is AmmoItemClass { AmmoTemplate: { } magazineTemplate })
		{
			ammoTemplate = magazineTemplate;
		}
		else if (__instance?.Chambers is { Length: > 0 } slots && slots[0]?.ContainedItem is AmmoItemClass { AmmoTemplate: { } chamberedTemplate })
		{
			ammoTemplate = chamberedTemplate;
		}

		return true; // call the original code with updated template
	}

#pragma warning restore IDE0060

	private bool _hasSilentAimTarget = false;
	private Vector3 _silentAimTargetPosition = Vector3.zero;
	private float _silentAimNextShotTime = 0f;
	protected override void Update()
	{
		base.Update();

		if (!SilentAim)
			return;

		HarmonyPatchOnce(harmony =>
		{
			HarmonyPrefix(harmony, typeof(BallisticsCalculator), nameof(BallisticsCalculator.CreateShot), nameof(CreateShotPrefix));
			HarmonyPrefix(harmony, typeof(Player), nameof(Player.ApplyShot), nameof(ApplyShotPrefix));
			HarmonyPrefix(harmony, typeof(Weapon), nameof(Weapon.CreateOpticCalibrationData), nameof(CreateOpticCalibrationDataPrefix));
		});

		_hasSilentAimTarget = false;

		if (!TryGetNearestTarget(out var player, out var camera, out var targetPosition, out _))
			return;

		if (player.IsInventoryOpened)
			return;

		if (player.HandsController is not Player.FirearmController controller)
			return;

		_hasSilentAimTarget = true;
		_silentAimTargetPosition = targetPosition;

		if (_silentAimNextShotTime > Time.time)
			return;

		controller.SetTriggerPressed(true);
		_silentAimNextShotTime = Time.time + SilentAimNextShotDelay;
		controller.SetTriggerPressed(false);
	}

	protected override void UpdateWhenHold()
	{
		if (!TryGetNearestTarget(out var player, out _, out var targetPosition, out _))
			return;

		AimAtPosition(player, targetPosition, Smoothness);
	}

	private bool TryGetNearestTarget(
		[NotNullWhen(true)] out Player? localPlayer,
		[NotNullWhen(true)] out Camera? camera,
		out Vector3 nearestTargetPosition,
		out AimTargetPart nearestTargetPart)
	{
		localPlayer = null;
		camera = null;
		nearestTargetPosition = Vector3.zero;
		nearestTargetPart = TargetPart;
		var nearestTargetDistance = float.MaxValue;
		var nearestTargetPriority = int.MaxValue;

		var state = GameState.Current;
		if (state == null)
			return false;

		camera = state.Camera;
		if (camera == null)
			return false;

		localPlayer = state.LocalPlayer;
		if (localPlayer == null)
			return false;

		if (localPlayer.HandsController == null || localPlayer.HandsController.Item is not Weapon weapon)
			return false;

		var template = weapon.CurrentAmmoTemplate;
		if (template == null)
			return false;

		foreach (var hostile in state.Hostiles)
		{
			if (hostile == null)
				continue;

			if (!hostile.IsAlive())
				continue;

			if (PreferHeadChest)
			{
				TryEvaluateTarget(hostile, AimTargetPart.Head, 0, camera, template, ref nearestTargetDistance, ref nearestTargetPriority, ref nearestTargetPosition);
				TryEvaluateTarget(hostile, AimTargetPart.Chest, 1, camera, template, ref nearestTargetDistance, ref nearestTargetPriority, ref nearestTargetPosition);
			}

			var selectedPriority = PreferHeadChest && TargetPart is not (AimTargetPart.Head or AimTargetPart.Chest) ? 2 : 0;
			TryEvaluateTarget(hostile, TargetPart, selectedPriority, camera, template, ref nearestTargetDistance, ref nearestTargetPriority, ref nearestTargetPosition);
		}

		nearestTargetPart = PreferHeadChest
			? nearestTargetPriority switch
			{
				0 => AimTargetPart.Head,
				1 => AimTargetPart.Chest,
				_ => TargetPart
			}
			: TargetPart;
		return nearestTargetPriority != int.MaxValue;
	}

	private bool TryEvaluateTarget(
		Player hostile,
		AimTargetPart part,
		int priority,
		Camera camera,
		AmmoTemplate template,
		ref float nearestDistance,
		ref int nearestPriority,
		ref Vector3 nearestPosition)
	{
		if (!TryGetTargetTransform(hostile, part, camera, out var targetTransform))
			return false;

		var destination = targetTransform.position;
		if (VisibleOnly && !camera.IsTransformVisible(targetTransform))
			return false;

		var screenPosition = camera.WorldPointToVisibleScreenPoint(destination);
		if (screenPosition == Vector2.zero || !IsInFieldOfView(screenPosition))
			return false;

		var distance = Vector3.Distance(camera.transform.position, destination);
		if (distance > MaximumDistance)
			return false;

		if (priority > nearestPriority || (priority == nearestPriority && distance >= nearestDistance))
			return false;

		nearestDistance = distance;
		nearestPriority = priority;

		var travelTime = distance / Mathf.Max(template.InitialSpeed, 0.01f);
		nearestPosition = new Vector3(
			destination.x + hostile.Velocity.x * travelTime,
			destination.y + hostile.Velocity.y * travelTime,
			destination.z);
		return true;
	}

	[UsedImplicitly]
	protected void OnGUI()
	{
		if (!ShowFovCircle || FovRadius <= 0)
			return;

		var player = GameState.Current?.LocalPlayer;
		if (!player.IsValid())
			return;

		if (player.HandsController == null || player.HandsController.Item is not Weapon)
			return;

		Render.DrawCircle(Render.ScreenCenter, FovRadius, FovCircleColor, FovCircleThickness, 48);
	}

	private bool IsInFieldOfView(Vector2 screenPosition)
	{
		if (FovRadius <= 0f)
			return true;

		var distance = Vector2.Distance(Render.ScreenCenter, screenPosition);
		return distance <= FovRadius;
	}

	private static void AimAtPosition(Player player, Vector3 targetPosition, float smoothness)
	{
		var firingAngle = player.Fireport.position - player.Fireport.up * 1f;
		var normalized = (targetPosition - firingAngle).normalized;
		var quaternion = Quaternion.LookRotation(normalized);
		var euler = quaternion.eulerAngles;

		//This is necessary due to crossing Y plane with target
		if (euler.x > 180f)
			euler.x -= 360f;

		var playerRotation = player.MovementContext.Rotation;
		var smoothAngle = GetSmoothAngle(playerRotation, new Vector2(euler.y, euler.x), Mathf.Clamp01(smoothness));
		player.MovementContext.Rotation = smoothAngle;
	}

	private static Vector2 GetSmoothAngle(Vector2 fromAngle, Vector2 toAngle, float smoothness)
	{
		var delta = fromAngle - toAngle;
		NormalizeAngle(ref delta);
		var smoothedDelta = Vector2.Scale(delta, new Vector2(smoothness, smoothness));
		toAngle = fromAngle - smoothedDelta;
		return toAngle;
	}

	private static void NormalizeAngle(ref Vector2 angle)
	{
		var newX = angle.x switch
		{
			<= -180f => angle.x + 360f,
			> 180f => angle.x - 360f,
			_ => angle.x
		};

		var newY = angle.y switch
		{
			> 90f => angle.y - 180f,
			<= -90f => angle.y + 180f,
			_ => angle.y
		};

		angle = new Vector2(newX, newY);
	}

	private bool TryGetTargetTransform(
		Player player,
		AimTargetPart targetPart,
		Camera camera,
		[NotNullWhen(true)] out Transform? transform)
	{
		transform = null;

		var bones = player.PlayerBones;
		if (bones == null)
			return false;

		transform = targetPart switch
		{
			AimTargetPart.Head => bones.Head.Original,
			AimTargetPart.Chest => bones.Ribcage.Original,
			AimTargetPart.Stomach => bones.Spine1,
			AimTargetPart.Arms => GetBestLimbTransform(camera, bones.Upperarms[0], bones.Upperarms[1]),
			AimTargetPart.Legs => GetBestLimbTransform(camera, bones.LeftThigh2.Original, bones.RightThigh2.Original),
			_ => null
		};

		return transform != null;
	}

	private Transform? GetBestLimbTransform(Camera camera, Transform? left, Transform? right)
	{
		if (VisibleOnly)
		{
			var leftVisible = left != null && camera.IsTransformVisible(left);
			var rightVisible = right != null && camera.IsTransformVisible(right);
			if (leftVisible && rightVisible)
				return GetCloserTransform(camera, left, right);
			if (leftVisible)
				return left;
			if (rightVisible)
				return right;
		}

		if (left == null)
			return right;
		if (right == null)
			return left;

		return GetCloserTransform(camera, left, right);
	}

	private static Transform GetCloserTransform(Camera camera, Transform left, Transform right)
	{
		var origin = camera.transform.position;
		return Vector3.SqrMagnitude(left.position - origin) <= Vector3.SqrMagnitude(right.position - origin)
			? left
			: right;
	}
}
