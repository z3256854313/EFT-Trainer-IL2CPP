using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Logging;
using UnityEngine;

namespace TrainerIL2CPP;

internal static class SpatialAudioCrashGuard
{
	private const string ModuleName = "lib_burst_generated.dll";
	private const string ExpectedFileSha256 =
		"D9680179B1C43BD51B3A2BEE07BB55C53972E7EA69632B5A797C92CED84E92BD";

	// The production hit-processing loop reads an int index, multiplies it by 80,
	// and dereferences the resulting structure. An invalid 0xFFFFFFFF index crashes
	// at RVA 0x55338. The guard below skips that hit and resumes the normal loop.
	private const int TrampolineRva = 0x55330;
	private const int CodeCaveRva = 0x58F08;
	private const uint PageExecuteReadWrite = 0x40;

	private static readonly byte[] OriginalTrampoline =
	[
		0x4F, 0x8D, 0x1C, 0x9B, 0x49, 0xC1, 0xE3, 0x04
	];

	private static readonly byte[] PatchedTrampoline =
	[
		0xE9, 0xD3, 0x3B, 0x00, 0x00, 0x90, 0x90, 0x90
	];

	private static readonly byte[] GuardCode =
	[
		// cmp r11d, -1
		0x41, 0x83, 0xFB, 0xFF,
		// je skip-hit
		0x74, 0x0D,
		// lea r11, [r11 + r11*4]; shl r11, 4
		0x4F, 0x8D, 0x1C, 0x9B, 0x49, 0xC1, 0xE3, 0x04,
		// jmp original-hit-load at lib_burst_generated.dll+0x55338
		0xE9, 0x1D, 0xC4, 0xFF, 0xFF,
		// skip-hit: jmp loop-advance
		0xE9, 0x00, 0xC4, 0xFF, 0xFF
	];

	private static int _applying;
	private static bool _finished;
	private static float _nextAttemptAt;

	internal static void TryApply(ManualLogSource logger)
	{
		if (_finished ||
			Time.realtimeSinceStartup < _nextAttemptAt ||
			Interlocked.Exchange(ref _applying, 1) != 0)
		{
			return;
		}

		try
		{
			var module = GetModuleHandle(ModuleName);
			if (module == IntPtr.Zero)
			{
				_nextAttemptAt = Time.realtimeSinceStartup + 0.5f;
				return;
			}

			var modulePath = GetLoadedModulePath(module);
			if (string.IsNullOrWhiteSpace(modulePath) ||
				!File.Exists(modulePath))
			{
				Fail(logger, $"Unable to resolve loaded module path for {ModuleName}.");
				return;
			}

			using var stream = File.OpenRead(modulePath);
			var fileHash = Convert.ToHexString(SHA256.HashData(stream));
			if (!string.Equals(
					fileHash,
					ExpectedFileSha256,
					StringComparison.OrdinalIgnoreCase))
			{
				Fail(
					logger,
					$"{ModuleName} hash mismatch; spatial-audio guard was not applied. " +
					$"Expected {ExpectedFileSha256}, found {fileHash}.");
				return;
			}

			var trampolineAddress = IntPtr.Add(module, TrampolineRva);
			var caveAddress = IntPtr.Add(module, CodeCaveRva);
			if (!BytesEqual(trampolineAddress, OriginalTrampoline))
			{
				Fail(
					logger,
					$"Unexpected bytes at {ModuleName}+0x{TrampolineRva:X}; " +
					"spatial-audio guard was not applied.");
				return;
			}

			if (!IsInt3Padding(caveAddress, GuardCode.Length))
			{
				Fail(
					logger,
					$"Unexpected code-cave contents at {ModuleName}+0x{CodeCaveRva:X}; " +
					"spatial-audio guard was not applied.");
				return;
			}

			WriteExecutableBytes(caveAddress, GuardCode);
			WriteExecutableBytes(trampolineAddress, PatchedTrampoline);

			if (!BytesEqual(caveAddress, GuardCode) ||
				!BytesEqual(trampolineAddress, PatchedTrampoline))
			{
				Fail(logger, "Spatial-audio guard verification failed after patching.");
				return;
			}

			FlushInstructionCache(
				GetCurrentProcess(),
				caveAddress,
				(UIntPtr)GuardCode.Length);
			FlushInstructionCache(
				GetCurrentProcess(),
				trampolineAddress,
				(UIntPtr)PatchedTrampoline.Length);

			_finished = true;
			logger.LogInfo(
				"Spatial-audio crash guard applied: invalid hit index 0xFFFFFFFF " +
				"will be skipped at lib_burst_generated.dll+0x55338.");
		}
		catch (Exception exception)
		{
			Fail(logger, "Spatial-audio crash guard failed: " + exception);
		}
		finally
		{
			Volatile.Write(ref _applying, 0);
			_nextAttemptAt = Time.realtimeSinceStartup + 0.5f;
		}
	}

	private static void Fail(ManualLogSource logger, string message)
	{
		_finished = true;
		logger.LogError(message);
	}

	private static string? GetLoadedModulePath(IntPtr module)
	{
		var path = new StringBuilder(1024);
		var length = GetModuleFileName(module, path, (uint)path.Capacity);
		return length == 0 ? null : path.ToString();
	}

	private static bool BytesEqual(IntPtr address, byte[] expected)
	{
		var actual = new byte[expected.Length];
		Marshal.Copy(address, actual, 0, actual.Length);
		return actual.AsSpan().SequenceEqual(expected);
	}

	private static bool IsInt3Padding(IntPtr address, int length)
	{
		var bytes = new byte[length];
		Marshal.Copy(address, bytes, 0, bytes.Length);
		return bytes.All(value => value == 0xCC);
	}

	private static void WriteExecutableBytes(IntPtr address, byte[] bytes)
	{
		if (!VirtualProtect(
				address,
				(UIntPtr)bytes.Length,
				PageExecuteReadWrite,
				out var oldProtect))
		{
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		try
		{
			Marshal.Copy(bytes, 0, address, bytes.Length);
		}
		finally
		{
			VirtualProtect(
				address,
				(UIntPtr)bytes.Length,
				oldProtect,
				out _);
		}
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr GetModuleHandle(string moduleName);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern uint GetModuleFileName(
		IntPtr module,
		StringBuilder fileName,
		uint size);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool VirtualProtect(
		IntPtr address,
		UIntPtr size,
		uint newProtect,
		out uint oldProtect);

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetCurrentProcess();

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool FlushInstructionCache(
		IntPtr process,
		IntPtr address,
		UIntPtr size);
}
