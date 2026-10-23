using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

namespace DrakesItemShop.Storefront;

/// <summary>
/// The Storefront sounds: small .wav files embedded in the DLL (Storefront/Sounds), made for this mod, so there is nothing to
/// credit or download. Played as 3D one-shots on the game's SFX mixer group, so the player's SFX volume applies.
/// </summary>
internal static class StorefrontAudio
{
    internal const string DoorJingle = "drake_door_jingle.wav";
    internal const string DeskDing = "drake_desk_ding.wav";
    internal const string RegisterChing = "drake_register_ching.wav";
    internal const string SignFlip = "drake_sign_flip.wav";

    private static readonly Dictionary<string, AudioClip?> Clips = new();

    internal static AudioClip? Clip(string leaf)
    {
        if (Clips.TryGetValue(leaf, out var cached) && cached != null)
            return cached;
        return Clips[leaf] = Load(leaf);
    }

    private static AudioClip? Load(string leaf)
    {
        try
        {
            var assembly = typeof(StorefrontAudio).Assembly;
            var name = assembly.GetManifestResourceNames().FirstOrDefault(n =>
                n.Equals(leaf, StringComparison.OrdinalIgnoreCase) || n.EndsWith("." + leaf, StringComparison.OrdinalIgnoreCase));
            if (name == null)
            {
                ItemShopPlugin.LogSource?.LogWarning($"[Storefront] sound {leaf} is not embedded.");
                return null;
            }
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream == null)
                return null;
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return Decode(memory.ToArray(), leaf);
        }
        catch (Exception ex)
        {
            ItemShopPlugin.LogSource?.LogWarning($"[Storefront] couldn't load sound {leaf}: {ex.Message}");
            return null;
        }
    }

    /// <summary>16-bit PCM .wav (any channel count and rate) to an AudioClip.</summary>
    private static AudioClip? Decode(byte[] data, string name)
    {
        if (data.Length < 44 || data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F')
            return null;
        int channels = 1, rate = 44100, bits = 16, dataStart = -1, dataLength = 0;
        var pos = 12;
        while (pos + 8 <= data.Length)
        {
            var id = System.Text.Encoding.ASCII.GetString(data, pos, 4);
            var size = BitConverter.ToInt32(data, pos + 4);
            if (id == "fmt ")
            {
                channels = BitConverter.ToInt16(data, pos + 10);
                rate = BitConverter.ToInt32(data, pos + 12);
                bits = BitConverter.ToInt16(data, pos + 22);
            }
            else if (id == "data")
            {
                dataStart = pos + 8;
                dataLength = Math.Min(size, data.Length - dataStart);
                break;
            }
            pos += 8 + size + (size & 1);
        }
        if (dataStart < 0 || bits != 16 || channels < 1)
            return null;

        var samples = dataLength / 2;
        var floats = new float[samples];
        for (var i = 0; i < samples; i++)
            floats[i] = BitConverter.ToInt16(data, dataStart + i * 2) / 32768f;
        // The PCM reader callback fills the whole clip once on creation; avoids SetData, whose newer overload needs
        // System.Memory types this net481 project doesn't reference.
        var position = 0;
        return AudioClip.Create(name, samples / channels, channels, rate, false, buffer =>
        {
            var count = Math.Min(buffer.Length, floats.Length - position);
            if (count > 0)
                Array.Copy(floats, position, buffer, 0, count);
            for (var i = Math.Max(count, 0); i < buffer.Length; i++)
                buffer[i] = 0f;
            position += Math.Max(count, 0);
        }, pos => position = pos * channels);
    }

    private static AudioMixerGroup? SfxGroup()
    {
        try
        {
            var man = AudioMan.instance;
            return man == null ? null : typeof(AudioMan).GetMethod("GetSFXGroup")?.Invoke(man, null) as AudioMixerGroup;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Play a sound in the world. Does nothing on a dedicated server.</summary>
    internal static void PlayAt(string leaf, Vector3 position, float volume = 1f, float pitch = 1f, float range = 30f)
    {
        if (ZNet.instance != null && ZNet.instance.IsDedicated())
            return;
        if (Clip(leaf) is not { } clip)
            return;
        var go = new GameObject("drake_storefront_sfx");
        go.transform.position = position;
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = pitch;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = range;
        if (SfxGroup() is { } group)
            source.outputAudioMixerGroup = group;
        source.Play();
        UnityEngine.Object.Destroy(go, clip.length / Mathf.Max(0.2f, pitch) + 0.25f);
    }
}
