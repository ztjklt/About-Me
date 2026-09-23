using System;
using UnityEngine;

namespace AboutMe
{
    /// <summary>
    /// Bounded presentation-joint dynamics. This is not a PhysX constraint and does not
    /// feed force into the authoritative motor. Rotations and torques use parent-local space.
    /// </summary>
    public sealed class DollJointDynamics
    {
        public enum Profile { Torso, Head, Shoulder, Elbow, Hip, Knee }

        public struct Settings
        {
            public float inertia, stiffness, damping, maximumTorque, maximumSpeed;
            // Rotation-vector limits in radians. These are not anatomical swing/twist limits.
            public Vector3 minimumAngle, maximumAngle;
        }

        struct JointState
        {
            public Quaternion rotation;
            public Vector3 angularVelocity;
            public bool initialized;
        }

        readonly JointState[] states;
        readonly Profile[] profiles;
        public int Count { get { return states.Length; } }
        public const float MaximumSubstep = 1f / 120f;

        public DollJointDynamics(int count = 10)
        {
            if (count < 1 || count > 128) throw new ArgumentOutOfRangeException(nameof(count));
            states = new JointState[count];
            profiles = new Profile[count];
            for (int i = 0; i < count; i++)
                profiles[i] = i == 0 ? Profile.Torso : i == 1 ? Profile.Head : i < 4 ? Profile.Shoulder : i < 6 ? Profile.Elbow : i < 8 ? Profile.Hip : Profile.Knee;
        }

        public void SetProfile(int index, Profile profile)
        {
            CheckIndex(index);
            if ((int)profile < 0 || (int)profile > (int)Profile.Knee) throw new ArgumentOutOfRangeException(nameof(profile));
            profiles[index] = profile;
        }

        public void Reset()
        {
            Array.Clear(states, 0, states.Length);
        }

        // Call after spawn, teleport or ragdoll recovery. External IK may impose a final
        // contact pose, but should not reset these velocities on every ordinary frame.
        public void Reset(int index, Quaternion rotation)
        {
            CheckIndex(index);
            states[index] = new JointState { rotation = Normalize(rotation), initialized = true };
        }

        public Vector3 GetAngularVelocity(int index)
        {
            CheckIndex(index);
            return states[index].angularVelocity * Mathf.Rad2Deg;
        }

        public Quaternion Sample(int index, Quaternion target, float dt, Vector3 externalTorque, bool fastAction = false)
        {
            CheckIndex(index);
            Settings settings = GetSettings(profiles[index], fastAction);
            target = Normalize(target);
            Vector3 unused = Vector3.zero;
            target = Constrain(target, ref unused, settings);
            JointState state = states[index];
            if (!state.initialized)
            {
                state.rotation = target;
                state.angularVelocity = Vector3.zero;
                state.initialized = true;
            }
            dt = Mathf.Clamp(Finite(dt), 0, .1f);
            if (dt <= 0) { states[index] = state; return state.rotation; }
            externalTorque = Vector3.ClampMagnitude(Clean(externalTorque), settings.maximumTorque * 2);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / MaximumSubstep));
            float step = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                // Shortest-arc error expressed in the same parent frame as angular velocity.
                Quaternion error = Multiply(target, Conjugate(state.rotation));
                Vector3 displacement = Log(error);
                Vector3 torque = displacement * settings.stiffness - state.angularVelocity * settings.damping + externalTorque;
                torque = Vector3.ClampMagnitude(torque, settings.maximumTorque);
                state.angularVelocity += torque * (step / settings.inertia);
                state.angularVelocity = Vector3.ClampMagnitude(state.angularVelocity, settings.maximumSpeed);
                state.rotation = Normalize(Multiply(Exp(state.angularVelocity * step), state.rotation));
                state.rotation = Constrain(state.rotation, ref state.angularVelocity, settings);
            }
            states[index] = state;
            return state.rotation;
        }

        public static Settings GetSettings(Profile profile, bool fastAction = false)
        {
            float inertia, frequency, dampingRatio, torque, speed;
            Vector3 minimum, maximum;
            switch (profile)
            {
                case Profile.Torso:
                    inertia = .10f; frequency = 24; dampingRatio = .94f; torque = 11; speed = 330;
                    minimum = new Vector3(-35, -32, -26); maximum = new Vector3(35, 32, 26); break;
                case Profile.Head:
                    inertia = .045f; frequency = 20; dampingRatio = .88f; torque = 4.5f; speed = 400;
                    minimum = new Vector3(-72, -45, -35); maximum = new Vector3(48, 45, 35); break;
                case Profile.Shoulder:
                    inertia = .018f; frequency = fastAction ? 65 : 29; dampingRatio = fastAction ? .84f : .85f;
                    torque = fastAction ? 18 : 7; speed = fastAction ? 1200 : 720;
                    minimum = new Vector3(-125, -60, -55); maximum = new Vector3(85, 60, 55); break;
                case Profile.Elbow:
                    inertia = .009f; frequency = fastAction ? 70 : 34; dampingRatio = fastAction ? .86f : .90f;
                    torque = fastAction ? 11 : 4.5f; speed = fastAction ? 1200 : 800;
                    minimum = new Vector3(-125, -7, -7); maximum = new Vector3(5, 7, 7); break;
                case Profile.Hip:
                    inertia = .022f; frequency = 40; dampingRatio = .94f; torque = 10; speed = 850;
                    minimum = new Vector3(-82, -24, -36); maximum = new Vector3(82, 24, 36); break;
                default:
                    inertia = .011f; frequency = 42; dampingRatio = .94f; torque = 5.5f; speed = 900;
                    minimum = new Vector3(-5, -7, -7); maximum = new Vector3(130, 7, 7); break;
            }
            return new Settings
            {
                inertia = inertia,
                stiffness = inertia * frequency * frequency,
                damping = 2 * dampingRatio * inertia * frequency,
                maximumTorque = torque,
                maximumSpeed = speed * Mathf.Deg2Rad,
                minimumAngle = minimum * Mathf.Deg2Rad,
                maximumAngle = maximum * Mathf.Deg2Rad
            };
        }

        static Quaternion Constrain(Quaternion rotation, ref Vector3 velocity, Settings s)
        {
            Vector3 angle = Log(rotation), clipped = new Vector3(
                Mathf.Clamp(angle.x, s.minimumAngle.x, s.maximumAngle.x),
                Mathf.Clamp(angle.y, s.minimumAngle.y, s.maximumAngle.y),
                Mathf.Clamp(angle.z, s.minimumAngle.z, s.maximumAngle.z));
            if ((angle.x > clipped.x && velocity.x > 0) || (angle.x < clipped.x && velocity.x < 0)) velocity.x = 0;
            if ((angle.y > clipped.y && velocity.y > 0) || (angle.y < clipped.y && velocity.y < 0)) velocity.y = 0;
            if ((angle.z > clipped.z && velocity.z > 0) || (angle.z < clipped.z && velocity.z < 0)) velocity.z = 0;
            return Exp(clipped);
        }

        // Implemented with managed arithmetic so the solver can be tested without a Unity scene.
        static Quaternion Normalize(Quaternion q)
        {
            if (!IsFinite(q.x) || !IsFinite(q.y) || !IsFinite(q.z) || !IsFinite(q.w)) return Quaternion.identity;
            float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (length < 1e-7f) return Quaternion.identity;
            return new Quaternion(q.x / length, q.y / length, q.z / length, q.w / length);
        }
        static Quaternion Conjugate(Quaternion q) { return new Quaternion(-q.x, -q.y, -q.z, q.w); }
        static Quaternion Multiply(Quaternion a, Quaternion b)
        {
            return new Quaternion(a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
                a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
                a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
                a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
        }
        static Vector3 Log(Quaternion q)
        {
            q = Normalize(q);
            if (q.w < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            Vector3 vector = new Vector3(q.x, q.y, q.z);
            float sine = vector.magnitude;
            if (sine < 1e-6f) return vector * 2;
            float angle = 2 * Mathf.Atan2(sine, Mathf.Clamp(q.w, -1, 1));
            return vector * (angle / sine);
        }
        static Quaternion Exp(Vector3 angle)
        {
            float length = angle.magnitude;
            if (length < 1e-6f) return Normalize(new Quaternion(angle.x * .5f, angle.y * .5f, angle.z * .5f, 1));
            float scale = Mathf.Sin(length * .5f) / length;
            return new Quaternion(angle.x * scale, angle.y * scale, angle.z * scale, Mathf.Cos(length * .5f));
        }
        void CheckIndex(int index)
        { if (index < 0 || index >= states.Length) throw new ArgumentOutOfRangeException(nameof(index)); }
        static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static float Finite(float value) { return IsFinite(value) ? value : 0; }
        static Vector3 Clean(Vector3 value) { return new Vector3(Finite(value.x), Finite(value.y), Finite(value.z)); }
    }
}
