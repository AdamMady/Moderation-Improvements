using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace BigOrb;

// Join/leave chime. Two-note bell generated at runtime so there are no audio files
// to ship. Up = join, down = leave. Main thread only.
internal static class Chime
{
    // set when playback throws so we don't spam the log every join
    private static bool _broken;

    internal static bool Enabled => !_broken && Plugin.ChimeEnabled.Value;

    // in-game setting, persisted via the config entry
    internal static void SetEnabled(bool on)
    {
        Plugin.ChimeEnabled.Value = on;
        if (on) _broken = false;
    }

    private static AudioSource _source;
    private static AudioClip _join, _leave;
    private static readonly Queue<bool> _pending = new(); // true = join
    private static float _nextPlay;

    private const int Rate = 44100;
    private const float NoteLen = 0.32f;   // total note length incl. decay
    private const float NoteFade = 0.03f;  // fade out at the end to avoid a click
    private const float NoteGap = 0.115f;  // second note offset
    private const float Spacing = 0.45f;   // min gap between chimes
    private const int MaxQueued = 3;       // cap so a mass disconnect doesn't play 20 chimes

    internal static void Play(bool join)
    {
        if (!Enabled) return;
        if (_pending.Count >= MaxQueued) return;
        _pending.Enqueue(join);
    }

    // called from OrbBehaviour.Update
    internal static void Step(GameObject holder)
    {
        if (_pending.Count == 0 || Time.unscaledTime < _nextPlay) return;
        _nextPlay = Time.unscaledTime + Spacing;
        var join = _pending.Dequeue();
        try
        {
            if (!Ensure(holder)) return;
            var clip = join ? _join : _leave;
            if (clip != null) _source.PlayOneShot(clip, Mathf.Clamp01(Plugin.ChimeVolume.Value));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError("chime: " + e.Message);
            _pending.Clear();
            _broken = true;
        }
    }

    private static bool Ensure(GameObject holder)
    {
        if (_source != null && _join != null && _leave != null) return true;
        if (holder == null) return false;

        if (_source == null)
        {
            _source = holder.GetComponent<AudioSource>();
            if (_source == null) _source = holder.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;       // 2D
            _source.bypassEffects = true;
            _source.bypassListenerEffects = true;
            _source.bypassReverbZones = true;
            _source.ignoreListenerPause = true;
            _source.ignoreListenerVolume = true; // ignore game volume
        }
        // E5->A5 join, A5->D5 leave
        _join ??= Build("orbJoin", 659.25f, 880f);
        _leave ??= Build("orbLeave", 880f, 587.33f);
        return _source != null && _join != null && _leave != null;
    }

    private static AudioClip Build(string name, float f1, float f2)
    {
        int total = (int)(Rate * (NoteGap + NoteLen));
        var data = new Il2CppStructArray<float>(total);
        for (int i = 0; i < total; i++)
        {
            float t = i / (float)Rate;
            data[i] = Mathf.Clamp(0.5f * (Note(t, f1) + Note(t - NoteGap, f2)), -1f, 1f);
        }
        var clip = AudioClip.Create(name, total, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // short attack, exp decay, plus a bit of the octave above so it sounds like a bell
    private static float Note(float t, float freq)
    {
        if (t < 0f || t > NoteLen) return 0f;
        float env = Mathf.Min(1f, t / 0.006f) * Mathf.Exp(-t * 14f);
        if (t > NoteLen - NoteFade) env *= (NoteLen - t) / NoteFade;
        return env * (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * freq * t));
    }
}
