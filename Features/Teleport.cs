using EFT.Trainer.Configuration;
using EFT.Trainer.Extensions;
using JetBrains.Annotations;
using UnityEngine;

#nullable enable

namespace EFT.Trainer.Features;

[UsedImplicitly]
internal class Teleport : Feature
{
	public override string Name => "Teleport";
	public override string Description => "Save and restore the local player's position.";

	[ConfigurationProperty(Order = 1)]
	public KeyCode SaveKey { get; set; } = KeyCode.F7;

	[ConfigurationProperty(Order = 2)]
	public KeyCode RestoreKey { get; set; } = KeyCode.F8;

	[ConfigurationProperty(Skip = true)]
	public bool HasSavedPosition { get; private set; }

	private Vector3 _savedPosition;
	private Quaternion _savedRotation;

	private void Update()
	{
		var player = GameState.Current?.LocalPlayer;
		if (!player.IsValid())
			return;

		var playerTransform = player.gameObject.transform;

		if (SaveKey != KeyCode.None && Input.GetKeyDown(SaveKey))
		{
			_savedPosition = playerTransform.position;
			_savedRotation = playerTransform.rotation;
			HasSavedPosition = true;
			AddConsoleLog($"[Trainer] Saved position: {_savedPosition}");
		}

		if (HasSavedPosition && RestoreKey != KeyCode.None && Input.GetKeyDown(RestoreKey))
		{
			player.gameObject.SetActive(true);
			playerTransform.SetPositionAndRotation(_savedPosition, _savedRotation);
			AddConsoleLog($"[Trainer] Restored position: {_savedPosition}");
		}
	}
}
