using System;
using System.Collections.Generic;
using System.Linq;
using EFT.CameraControl;
using EFT.InventoryLogic;
using EFT.Trainer.Configuration;
using EFT.Trainer.Extensions;
using EFT.Trainer.Properties;
using EFT.Trainer.UI;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;

#nullable enable

namespace EFT.Trainer.Features;

public class PlayerColor(Color color, Color borderColor, Color infoColor) : IFeature
{
	[ConfigurationProperty(Order = 1)]
	public Color Color { get; set; } = color;

	[ConfigurationProperty(Order = 2)]
	public Color BorderColor { get; set; } = borderColor;

	[ConfigurationProperty(Order = 3)]
	public Color InfoColor { get; set; } = infoColor;

	public string Name => nameof(PlayerColor);
}

public class ShootableColor(Color color, Color borderColor) : IFeature
{
	[ConfigurationProperty(Order = 1)]
	public Color Color { get; set; } = color;

	[ConfigurationProperty(Order = 2)]
	public Color BorderColor { get; set; } = borderColor;

	public string Name => nameof(ShootableColor);
}

[UsedImplicitly]
internal class Players : ToggleFeature
{
	public override string Name => Strings.FeaturePlayersName;
	public override string Description => Strings.FeaturePlayersDescription;

	[ConfigurationProperty(Order = 10)]
	public PlayerColor BearColors { get; set; } = new(Color.blue, Color.red, Color.red);

	[ConfigurationProperty(Order = 10)]
	public PlayerColor UsecColors { get; set; } = new(Color.green, Color.red, Color.red);

	[ConfigurationProperty(Order = 10)]
	public PlayerColor ScavColors { get; set; } = new(Color.yellow, Color.red, Color.red);

	[ConfigurationProperty(Order = 10)]
	public PlayerColor BossColors { get; set; } = new(Color.red, Color.red, Color.red);

	[ConfigurationProperty(Order = 10)]
	public PlayerColor CultistColors { get; set; } = new(Color.yellow, Color.red, Color.red);

	[ConfigurationProperty(Order = 10)]
	public PlayerColor ScavRaiderColors { get; set; } = new(Color.yellow, Color.red, Color.red);

	[ConfigurationProperty(Order = 10)]
	public PlayerColor ScavAssaultColors { get; set; } = new(Color.yellow, Color.red, Color.red);

	[ConfigurationProperty(Order = 10)]
	public PlayerColor MarksmanColors { get; set; } = new(Color.yellow, Color.red, Color.red);

	[ConfigurationProperty(Order = 10)]
	public PlayerColor RogueUsecColors { get; set; } = new(Color.gray, Color.red, Color.red);

	[ConfigurationProperty(Order = 20)]
	public bool ShowBoxes { get; set; } = true;

	[ConfigurationProperty(Order = 21)]
	public float BoxThickness { get; set; } = 2f;

	[ConfigurationProperty(Order = 22)]
	public float BoxPadding { get; set; } = 0.08f;

	[ConfigurationProperty(Order = 23)]
	public bool UseRendererBounds { get; set; } = true;

	[ConfigurationProperty(Order = 24)]
	public bool ClampBoxToScope { get; set; } = true;

	[ConfigurationProperty(Order = 30)]
	public bool ShowCharms { get; set; } = true;

	[ConfigurationProperty(Order = 31)]
	public bool XRayVision { get; set; } = true;

	[ConfigurationProperty(Order = 40)]
	public bool ShowInfos { get; set; } = false;

	[ConfigurationProperty(Order = 50)]
	public bool ShowSkeletons { get; set; } = false;

	[ConfigurationProperty(Order = 51)]
	public float SkeletonThickness { get; set; } = 2;

	[ConfigurationProperty(Order = 60)]
	public bool ShowShootable { get; set; } = false;

	[ConfigurationProperty(Order = 61)]
	public ShootableColor ShootableColors { get; set; } = new(Color.green, Color.red);

	[ConfigurationProperty(Order = 62)]
	public bool ShowNotShootable { get; set; } = false;

	[ConfigurationProperty(Order = 63)]
	public ShootableColor NotShootableColors { get; set; } = new(Color.red, Color.blue);

	[ConfigurationProperty(Order = 19)]
	public float MaximumDistance { get; set; } = 0f;

	private static bool _lastXRayVision = true;
	private static bool _lastShowCharms = true;

	private static Camera? _opticCamera;
	private static (Vector2 center, float radius) _scopeParameters;

	[UsedImplicitly]
	protected void OnGUI()
	{
		var snapshot = GameState.Current;
		if (snapshot == null)
			return;

		if (snapshot.MapMode)
			return;

		var hostiles = snapshot.Hostiles;

		var player = snapshot.LocalPlayer;
		if (player == null)
			return;

		var camera = snapshot.Camera;
		if (camera == null)
			return;

		var cacheComponent = player.GetOrAddComponent<ShaderCache>();
		var cache = cacheComponent.Cache;

		if (!Enabled || XRayVision != _lastXRayVision || ShowCharms != _lastShowCharms)
		{
			_lastXRayVision = XRayVision;
			_lastShowCharms = ShowCharms;

			if (cache.Count > 0)
				ResetShaders(cache);

			return;
		}

		var isAiming = AimingCheck(camera, player);

		foreach (var ennemy in hostiles)
		{
			if (!ennemy.IsValid())
				continue;

			var playerColors = GetPlayerColors(ennemy);
			var borderColor = playerColors.BorderColor;

			if (ShowCharms)
				SetShaders(ennemy, GameState.OutlineShader, playerColors.Color, borderColor, cache);

			var position = ennemy.Transform.position;
			var screenPosition = isAiming ? ScopePointToScreenPoint(camera, position) : camera.WorldPointToVisibleScreenPoint(position);
			if (screenPosition == Vector2.zero)
				continue;

			var distance = Mathf.Round(Vector3.Distance(camera.transform.position, position));
			if (MaximumDistance > 0 && distance > MaximumDistance)
				continue;

			var playerBones = ennemy.PlayerBones;
			if (playerBones == null)
				continue;

			if (!TryGetPlayerScreenBox(ennemy, camera, isAiming, out var playerBox))
				continue;

			if (ShowShootable)
			{
				var bonesToCheck = GetBonesToCheck(playerBones);
				borderColor = bonesToCheck.Any(bone => IsTransformVisibleCached(bone.transform, camera.IsTransformVisible))
					? ShootableColors.BorderColor
					: ShowNotShootable ? NotShootableColors.BorderColor : playerColors.BorderColor;

				if (ShowSkeletons)
				{
					foreach (var bone in bonesToCheck)
					{
						var bonesColor = IsTransformVisibleCached(bone.transform, camera.IsTransformVisible) ? ShootableColors.Color : ShowNotShootable ? NotShootableColors.Color : playerColors.Color;
						Bones.RenderBones(ennemy, bone.bones, SkeletonThickness, bonesColor, camera, isAiming);
					}

					var color = IsTransformVisibleCached(bonesToCheck[0].transform, camera.IsTransformVisible) ? ShootableColors.Color : ShowNotShootable ? NotShootableColors.Color : playerColors.Color;
					Bones.RenderHead(ennemy, SkeletonThickness, color, camera, isAiming);
					if (distance < 75f)
						Bones.RenderFingers(ennemy, SkeletonThickness, color, camera, isAiming);
				}

				ClearTransformCache();
			}
			else if (ShowSkeletons)
				Bones.RenderBones(ennemy, SkeletonThickness, playerColors.Color, camera, isAiming, distance);

			if (ShowBoxes)
				Render.DrawBox(playerBox.x, playerBox.y, playerBox.width, playerBox.height, BoxThickness, borderColor);

			var ennemyHealthController = ennemy.HealthController;
			var ennemyHandController = ennemy.HandsController;

			if (!ShowInfos || ennemyHealthController is not { IsAlive: true })
				continue;

			var bodyPartHealth = ennemyHealthController.GetBodyPartHealth(EBodyPart.Common);
			var currentPlayerHealth = bodyPartHealth.Current;
			var maximumPlayerHealth = bodyPartHealth.Maximum;

			var weaponText = ennemyHandController != null && ennemyHandController.Item is Weapon weapon ? weapon.ShortName.Localized() : string.Empty;
			var distanceText = string.Format(Strings.FeaturePointOfInterestsDistanceFormat, distance);
			var infoText = string.Format(Strings.FeaturePlayersFormat, weaponText, Mathf.Round(currentPlayerHealth * 100 / maximumPlayerHealth), distanceText).Trim();

			Render.DrawString(new Vector2(playerBox.x, playerBox.y - 20f), infoText, playerColors.InfoColor, false);
		}
	}

	private bool TryGetPlayerScreenBox(Player player, Camera camera, bool isAiming, out Rect box)
	{
		box = default;
		var minimum = new Vector2(float.MaxValue, float.MaxValue);
		var maximum = new Vector2(float.MinValue, float.MinValue);
		var points = 0;

		void Consider(Vector3 worldPoint)
		{
			var screenPoint = isAiming
				? ScopePointToScreenPointRaw(camera, worldPoint)
				: camera.WorldPointToScreenPoint(worldPoint);
			if (screenPoint == Vector2.zero)
				return;

			minimum = Vector2.Min(minimum, screenPoint);
			maximum = Vector2.Max(maximum, screenPoint);
			points++;
		}

		if (UseRendererBounds && player.PlayerBody?.BodySkins is { } skins)
		{
			foreach (var skin in skins.Values)
			{
				if (skin == null)
					continue;

				foreach (var renderer in skin.GetRenderers())
				{
					if (renderer == null)
						continue;

					var bounds = renderer.localBounds;
					var min = bounds.min;
					var max = bounds.max;
					for (var x = 0; x < 2; x++)
					{
						for (var y = 0; y < 2; y++)
						{
							for (var z = 0; z < 2; z++)
							{
								Consider(renderer.transform.TransformPoint(new Vector3(
									x == 0 ? min.x : max.x,
									y == 0 ? min.y : max.y,
									z == 0 ? min.z : max.z)));
							}
						}
					}
				}
			}
		}

		if (points == 0 && player.PlayerBones is { } bones)
		{
			Consider(bones.Head.Original.position);
			Consider(bones.Ribcage.Original.position);
			Consider(bones.Spine1.position);
			Consider(bones.Pelvis.Original.position);
			Consider(bones.Upperarms[0].position);
			Consider(bones.Upperarms[1].position);
			Consider(bones.LeftThigh2.Original.position);
			Consider(bones.RightThigh2.Original.position);
			Consider(player.Transform.position);
		}

		if (points == 0)
			return false;

		box = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
		var padding = Mathf.Clamp(BoxPadding, 0f, 0.5f) * Mathf.Min(box.width, box.height);
		box = new Rect(
			box.x - padding,
			box.y - padding,
			box.width + padding * 2f,
			box.height + padding * 2f);

		if (isAiming && ClampBoxToScope && _scopeParameters.radius > 0f)
		{
			var scopeBounds = Rect.MinMaxRect(
				_scopeParameters.center.x - _scopeParameters.radius,
				_scopeParameters.center.y - _scopeParameters.radius,
				_scopeParameters.center.x + _scopeParameters.radius,
				_scopeParameters.center.y + _scopeParameters.radius);
			var xMin = Mathf.Max(box.xMin, scopeBounds.xMin);
			var yMin = Mathf.Max(box.yMin, scopeBounds.yMin);
			var xMax = Mathf.Min(box.xMax, scopeBounds.xMax);
			var yMax = Mathf.Min(box.yMax, scopeBounds.yMax);
			if (xMax <= xMin || yMax <= yMin)
				return false;

			box = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
		}

		return box.width >= 1f && box.height >= 1f;
	}

	private static (Transform transform, string[] bones)[] GetBonesToCheck(PlayerBones playerBones)
	{
		return
		[
			(playerBones.Head.Original.transform, [Bones.Neck, Bones.Head]),
			(playerBones.Neck.transform, [Bones.RCollarbone, Bones.Spine3, Bones.LCollarbone, Bones.Spine3, Bones.Spine3, Bones.Neck]),
			(playerBones.Spine1.transform, [Bones.Pelvis, Bones.Spine1, Bones.Spine1, Bones.Spine2, Bones.Spine2, Bones.Spine3]),
			(playerBones.Upperarms[0].transform, [Bones.LCollarbone, Bones.LForearm1, Bones.LForearm1, Bones.LForearm2]),
			(playerBones.Upperarms[1].transform, [Bones.RCollarbone, Bones.RForearm1, Bones.RForearm1, Bones.RForearm2]),
			(playerBones.Forearms[0].transform, [Bones.LForearm2, Bones.LForearm3, Bones.LForearm3, Bones.LPalm]),
			(playerBones.Forearms[1].transform, [Bones.RForearm2, Bones.RForearm3, Bones.RForearm3, Bones.RPalm]),
			(playerBones.LeftThigh1.Original.transform, [Bones.Pelvis, Bones.LThigh1, Bones.LThigh1, Bones.LThigh2]),
			(playerBones.RightThigh1.Original.transform, [Bones.Pelvis, Bones.RThigh1, Bones.RThigh1, Bones.RThigh2]),
			(playerBones.LeftThigh2.Original.transform, [Bones.LThigh2, Bones.LCalf, Bones.LCalf, Bones.LFoot, Bones.LFoot, Bones.LToe]),
			(playerBones.RightThigh2.Original.transform, [Bones.RThigh2, Bones.RCalf, Bones.RCalf, Bones.RFoot, Bones.RFoot, Bones.RToe])
		];
	}

	private readonly Dictionary<Transform, bool> _cache = [];

	private bool IsTransformVisibleCached(Transform value, Func<Transform, bool> isVisibleFunc)
	{
		if (_cache.TryGetValue(value, out bool isVisible))
		{
			return isVisible;
		}

		isVisible = isVisibleFunc(value);
		_cache[value] = isVisible;
		return isVisible;
	}

	private void ClearTransformCache()
	{
		_cache.Clear();
	}

	private static bool AimingCheck(Camera camera, Player player)
	{
		var handsController = player.HandsController;
		if (handsController == null)
			return false;

		var weaponAnimation = player.ProceduralWeaponAnimation;
		if (weaponAnimation == null)
			return false;

		var aimingMod = weaponAnimation.CurrentAimingMod;
		if (aimingMod == null)
			return false;

		if (aimingMod.ScopesCount <= 0)
			return false;

		var zoom = aimingMod.GetCurrentOpticZoom();
		var isAiming = handsController.IsAiming;

		if (isAiming && zoom <= 1)
			isAiming = false;

		var currentOptic = weaponAnimation.HandsContainer.Weapon.GetComponentInChildren<OpticSight>();
		if (isAiming && currentOptic != null)
			GetScopeParameters(camera, currentOptic);

		if (_opticCamera != null)
			return isAiming;

		_opticCamera = Camera.allCameras.FirstOrDefault(c => c.name == "BaseOpticCamera(Clone)");

		return isAiming;
	}

	public PlayerColor GetPlayerColors(Player player)
	{
		var hostileType = player.GetHostileType();
		return GetPlayerColors(hostileType);
	}

	public PlayerColor GetPlayerColors(HostileType hostileType)
	{
		return hostileType switch
		{
			HostileType.Bear => BearColors,
			HostileType.Usec => UsecColors,
			HostileType.Scav => ScavColors,
			HostileType.Boss => BossColors,
			HostileType.Cultist => CultistColors,
			HostileType.ScavRaider => ScavRaiderColors,
			HostileType.ScavAssault => ScavAssaultColors,
			HostileType.Marksman => MarksmanColors,
			HostileType.RogueUsec => RogueUsecColors,
			_ => ScavColors,
		};
	}

	private void SetShaders(Player player, Shader? shader, Color color, Color borderColor, Dictionary<Renderer, Shader?> cache)
	{
		var playerBody = player.PlayerBody;
		if (playerBody == null)
			return;

		var skins = playerBody.BodySkins;
		if (skins == null)
			return;

		foreach (var skin in skins.Values)
		{
			if (skin == null)
				continue;

			foreach (var renderer in skin.GetRenderers())
			{
				if (renderer == null)
					continue;

				var material = renderer.material;
				if (material == null)
					continue;

				if (material.shader != null && material.shader == shader)
					continue;

				// disable custom occlusion/culling system, making the chams flickering or not rendering at all
				renderer.allowOcclusionWhenDynamic = false;
				renderer.forceRenderingOff = false;
				renderer.enabled = true;

				cache[renderer] = material.shader;
				material.shader = shader;

				material.SetColor(ShaderProperties.FirstOutlineColor, borderColor);
				material.SetFloat(ShaderProperties.FirstOutlineWidth, 0.02f);
				material.SetColor(ShaderProperties.SecondOutlineColor, color);
				material.SetFloat(ShaderProperties.SecondOutlineWidth, 0.0025f);
				material.SetFloat(ShaderProperties.ZTest, (float)(XRayVision ? CompareFunction.Always : CompareFunction.Less));
			}
		}
	}

	private static void ResetShaders(Dictionary<Renderer, Shader?> cache)
	{
		var hits = 0;
		foreach (var renderer in cache.Keys)
		{
			if (renderer == null)
				continue;

			if (renderer.material == null)
				continue;

			var shader = cache[renderer];
			if (renderer.material.shader == shader)
				continue;

			renderer.material.shader = shader;
			hits++;
		}

		if (hits == 0 && cache.Count > 0)
			cache.Clear();
	}

	public static Vector2 ScopePointToScreenPoint(Camera camera, Vector3 worldPoint, bool clamp = false)
	{
		if (_opticCamera == null || !GetCameraOffset(camera, out var scale, out var cameraOffset))
			return camera.WorldPointToScreenPoint(worldPoint);

		var scopePoint = ScopePointToScreenPointRaw(camera, worldPoint);
		if (scopePoint == Vector2.zero)
			return Vector2.zero;

		if (clamp)
			return ClampPointToScope(scopePoint);

		var distance = Vector2.Distance(_scopeParameters.center, scopePoint);
		if (distance <= _scopeParameters.radius)
			return scopePoint;

		return Vector2.zero;
	}

	private static Vector2 ScopePointToScreenPointRaw(Camera camera, Vector3 worldPoint)
	{
		if (_opticCamera == null || !GetCameraOffset(camera, out var scale, out var cameraOffset))
			return camera.WorldPointToScreenPoint(worldPoint);

		var opticScreenPoint = _opticCamera.WorldToScreenPoint(worldPoint);
		if (opticScreenPoint.z <= 0.01f)
			return Vector2.zero;

		var scopePoint = (Vector2)opticScreenPoint + cameraOffset;
		scopePoint.y = Screen.height - scopePoint.y * scale;
		scopePoint.x *= scale;
		return scopePoint;
	}

	private static bool GetCameraOffset(Camera camera, out float scale, out Vector2 cameraOffset)
	{
		scale = 0f;
		cameraOffset = Vector2.zero;

		if (_opticCamera == null)
			return false;

		scale = Screen.height / (float)camera.scaledPixelHeight;
		cameraOffset = new Vector2(
			camera.pixelWidth / 2 - _opticCamera.pixelWidth / 2,
			camera.pixelHeight / 2 - _opticCamera.pixelHeight / 2);

		return true;
	}
	private static Vector2 ClampPointToScope(Vector2 scopePoint)
	{
		var distance = Vector2.Distance(_scopeParameters.center, scopePoint);

		var clampedPoint = scopePoint;

		if (distance > _scopeParameters.radius)
		{
			var clampedVector = (scopePoint - _scopeParameters.center).normalized * _scopeParameters.radius;
			clampedPoint = _scopeParameters.center + clampedVector;
		}

		return clampedPoint;
	}

	private static void GetScopeParameters(Camera camera, OpticSight currentOptic)
	{
		var opticTransform = currentOptic.LensRenderer.transform;
		var lensMesh = currentOptic.LensRenderer.GetComponent<MeshFilter>().mesh;
		var lensUpperRight = opticTransform.TransformPoint(lensMesh.bounds.max);
		var lensUpperLeft = opticTransform.TransformPoint(new Vector3(lensMesh.bounds.min.x, 0, lensMesh.bounds.max.z));

		var lensUpperRight3D = camera.WorldPointToScreenPoint(lensUpperRight);
		var lensUpperLeft3D = camera.WorldPointToScreenPoint(lensUpperLeft);
		_scopeParameters.radius = Vector2.Distance(lensUpperRight3D, lensUpperLeft3D) / 2;
		_scopeParameters.center = camera.WorldPointToScreenPoint(opticTransform.position);
	}
}
