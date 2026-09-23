using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace AboutMe
{
    /// <summary>
    /// Cached, source-traceable Tripo motion samples retargeted offline to the toy rig.
    /// Does not load meshes, move roots or replace the final contact/feeding IK stage.
    /// </summary>
    public static class DollTripoMotion
    {
        [Serializable, Preserve]
        sealed class ClipData
        {
            public int schemaVersion, sampleRate, frameCount;
            public string name, sourceProvider, sourceType, sourceTaskId, sourceRigTaskId, sourceFile;
            public float duration;
            public float loopStart, loopEnd, loopBlendDuration, recommendedStart, recommendedEnd, strikeTime;
            public int sourceStrikeSide;
            public bool loop, rootTranslationExcluded;
            public BoneData[] bones;
        }
        [Serializable, Preserve]
        sealed class BoneData
        {
            public string name, parent, sourceBone;
            public float[] rotations;
        }
        sealed class Clip
        {
            public float duration;
            public float rawDuration, loopStart, loopEnd, loopBlend, recommendedStart, recommendedEnd, strikeTime;
            public int strikeSide;
            public int rate, frameCount;
            public bool loop;
            public string taskId, rigTaskId;
            public readonly Dictionary<string, Quaternion[]> bones = new Dictionary<string, Quaternion[]>(StringComparer.Ordinal);
        }

        // Null entries intentionally cache unavailable clips too: no repeated Resources IO
        // when the caller chooses its separately-labelled local fallback animation.
        static readonly Dictionary<string, Clip> cache = new Dictionary<string, Clip>(StringComparer.OrdinalIgnoreCase);
        static readonly string[] requiredBones = {
            "Hips", "Spine", "Torso", "Chest", "Neck", "Head",
            "LeftShoulder", "LeftUpperArm", "LeftForearm", "LeftHand",
            "RightShoulder", "RightUpperArm", "RightForearm", "RightHand",
            "LeftThigh", "LeftShin", "LeftFoot", "LeftToes",
            "RightThigh", "RightShin", "RightFoot", "RightToes"
        };

        public static void Preload(params string[] clipNames)
        {
            if (clipNames == null) return;
            foreach (string name in clipNames) Load(name);
        }
        // Useful after an editor re-export. Ordinary play should not clear a populated cache.
        public static void ClearCache() { cache.Clear(); }
        public static bool IsAvailable(string clipName) { return Load(clipName) != null; }
        public static float Duration(string clipName)
        { Clip clip = Load(clipName); return clip == null ? 0 : clip.duration; }
        public static string SourceTaskId(string clipName)
        { Clip clip = Load(clipName); return clip == null ? "" : clip.taskId; }
        public static string SourceRigTaskId(string clipName)
        { Clip clip = Load(clipName); return clip == null ? "" : clip.rigTaskId; }
        public static int SourceStrikeSide(string clipName)
        { Clip clip = Load(clipName); return clip == null ? 0 : clip.strikeSide; }

        // Align one selected source reaction/punch to the authority's impact moment.
        // This does not play several seconds of an injury/punch preset in one short action.
        public static float MapActionTime(string clipName, float elapsed, float actionDuration, float hitTime)
        {
            Clip clip = Load(clipName);
            if (clip == null || !Finite(elapsed) || !Finite(actionDuration) || actionDuration <= 0) return 0;
            float t = Mathf.Clamp(elapsed, 0, actionDuration);
            float hit = Mathf.Clamp(Finite(hitTime) ? hitTime : actionDuration * .35f, .001f, Mathf.Max(.001f, actionDuration - .001f));
            return t <= hit ? Mathf.Lerp(clip.recommendedStart, clip.strikeTime, t / hit)
                : Mathf.Lerp(clip.strikeTime, clip.recommendedEnd, (t - hit) / Mathf.Max(.001f, actionDuration - hit));
        }

        public static bool TrySample(string clipName, float timeSeconds, string boneName, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            Clip clip = Load(clipName);
            if (clip == null || string.IsNullOrEmpty(boneName) || !clip.bones.TryGetValue(boneName, out var track)) return false;
            if (!Finite(timeSeconds)) return false;
            float time = clip.loop ? clip.loopStart + clip.loopBlend + Mathf.Repeat(timeSeconds, clip.duration) : Mathf.Clamp(timeSeconds, 0, clip.rawDuration);
            rotation = SampleTrack(clip, track, time);
            if (clip.loop && clip.loopBlend > .0001f && time > clip.loopEnd - clip.loopBlend)
            {
                float tail = time - (clip.loopEnd - clip.loopBlend);
                Quaternion next = SampleTrack(clip, track, clip.loopStart + tail);
                rotation = Quaternion.SlerpUnclamped(rotation, next, Mathf.SmoothStep(0, 1, tail / clip.loopBlend));
            }
            return true;
        }

        static Quaternion SampleTrack(Clip clip, Quaternion[] track, float time)
        {
            float position = Mathf.Clamp(time * clip.rate, 0, clip.frameCount - 1);
            int first = Mathf.FloorToInt(position), second = Mathf.Min(first + 1, clip.frameCount - 1);
            return Quaternion.SlerpUnclamped(track[first], track[second], position - first);
        }

        static Clip Load(string name)
        {
            if (!ValidName(name)) return null;
            if (cache.TryGetValue(name, out var cached)) return cached;
            cache[name] = null;
            TextAsset asset = Resources.Load<TextAsset>("CampusDoll/Motion/" + name.ToLowerInvariant());
            if (!asset) return null;
            try
            {
                ClipData data = JsonUtility.FromJson<ClipData>(asset.text);
                if (data == null || data.schemaVersion != 1 || data.sampleRate != 30 || data.frameCount < 2 || data.frameCount > 3601
                    || !Finite(data.duration) || data.duration <= 0 || Mathf.Abs(data.duration - (data.frameCount - 1f) / data.sampleRate) > .002f
                    || !string.Equals(data.name, name, StringComparison.OrdinalIgnoreCase)
                    || data.sourceProvider != "Tripo" || data.sourceType != "animate_retarget"
                    || string.IsNullOrEmpty(data.sourceTaskId) || string.IsNullOrEmpty(data.sourceRigTaskId) || !data.rootTranslationExcluded
                    || data.bones == null || data.bones.Length != requiredBones.Length)
                    throw new FormatException("Invalid Tripo motion metadata or source provenance");
                var result = new Clip { duration = data.duration, rate = data.sampleRate, frameCount = data.frameCount,
                    rawDuration = data.duration, loop = data.loop, taskId = data.sourceTaskId, rigTaskId = data.sourceRigTaskId };
                result.loopStart = Finite(data.loopStart) ? Mathf.Clamp(data.loopStart, 0, data.duration) : 0;
                result.loopEnd = Finite(data.loopEnd) && data.loopEnd > result.loopStart ? Mathf.Min(data.loopEnd, data.duration) : data.duration;
                float span = result.loopEnd - result.loopStart;
                result.loopBlend = Finite(data.loopBlendDuration) ? Mathf.Clamp(data.loopBlendDuration, 0, span * .25f) : 0;
                if (result.loop) result.duration = span - result.loopBlend;
                if (result.duration <= .001f) throw new FormatException("Empty playback window");
                result.recommendedStart = Mathf.Clamp(Finite(data.recommendedStart) ? data.recommendedStart : 0, 0, data.duration);
                result.recommendedEnd = Finite(data.recommendedEnd) && data.recommendedEnd > result.recommendedStart ? Mathf.Min(data.recommendedEnd, data.duration) : data.duration;
                result.strikeTime = Mathf.Clamp(Finite(data.strikeTime) ? data.strikeTime : result.recommendedStart, result.recommendedStart, result.recommendedEnd);
                result.strikeSide = data.sourceStrikeSide < 0 ? -1 : data.sourceStrikeSide > 0 ? 1 : 0;
                foreach (BoneData bone in data.bones)
                {
                    if (bone == null || string.IsNullOrEmpty(bone.name) || string.IsNullOrEmpty(bone.sourceBone)
                        || bone.rotations == null || bone.rotations.Length != data.frameCount * 4 || result.bones.ContainsKey(bone.name))
                        throw new FormatException("Invalid or duplicated bone track");
                    Quaternion[] frames = new Quaternion[data.frameCount];
                    for (int frame = 0; frame < data.frameCount; frame++)
                    {
                        int i = frame * 4;
                        float x = bone.rotations[i], y = bone.rotations[i + 1], z = bone.rotations[i + 2], w = bone.rotations[i + 3];
                        if (!Finite(x) || !Finite(y) || !Finite(z) || !Finite(w)) throw new FormatException("Non-finite rotation sample");
                        float magnitude = Mathf.Sqrt(x * x + y * y + z * z + w * w);
                        if (magnitude < .98f || magnitude > 1.02f) throw new FormatException("Rotation sample is not normalized");
                        frames[frame] = new Quaternion(x / magnitude, y / magnitude, z / magnitude, w / magnitude);
                    }
                    result.bones.Add(bone.name, frames);
                }
                foreach (string required in requiredBones)
                    if (!result.bones.ContainsKey(required)) throw new FormatException("Missing required toy-rig bone: " + required);
                cache[name] = result;
                return result;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Tripo motion '" + name + "' is unavailable: " + exception.Message);
                return null;
            }
        }
        static bool ValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 48) return false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_')) return false;
            }
            return true;
        }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
