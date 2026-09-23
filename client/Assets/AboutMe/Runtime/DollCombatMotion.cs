using UnityEngine;

namespace AboutMe
{
    /// <summary>
    /// A presentation-only target-pose sampler. It never moves the authoritative root,
    /// applies damage, creates joints, or changes collision dimensions.
    /// Angles are complete offsets from the shared rig's rest pose, in degrees.
    /// </summary>
    public sealed class DollCombatMotion
    {
        public struct Input
        {
            public float dt, time, runBlend, actionElapsed, grabTension, escapeProgress, staggerRemaining, strideMeters;
            // Local +Z faces the camera's forward heading; +X is character-right.
            public Vector3 localVelocity;
            // A new impactId applies this local-space impulse once. About 6 m/s is strong.
            public Vector3 localImpact;
            public string actionKind;
            public int actionVariant, actionId, impactId;
            public bool airborne, holdingGrab;
        }

        public struct Pose
        {
            public Vector3 bodyOffset, bodyEuler, headEuler;
            // Separate seated-fall presentation, already weighted. Does not move the motor root.
            public Vector3 fallOffset, fallEuler;
            public float fallWeight;
            public Vector3 leftUpperArmEuler, rightUpperArmEuler;
            public Vector3 leftForearmEuler, rightForearmEuler;
            public Vector3 leftThighEuler, rightThighEuler;
            public Vector3 leftShinEuler, rightShinEuler;
            // Optional targets for a later foot IK stage; do not also add them to
            // an already-corrected planted foot. +Y is swing-foot lift.
            public Vector3 leftFootOffset, rightFootOffset;
            public float upperBodyWeight, feedingWeight, impactWeight;
            public float leftFootContact, rightFootContact, phase;
        }

        Vector3 velocity, recoilPosition, recoilVelocity, recoilAngle, recoilAngularVelocity;
        Pose previous;
        float phase, motionWeight, runWeight, actionAge;
        int lastActionId, lastImpactId;
        string lastActionKind = "";
        bool initialized, fallClockPresent;

        public void Reset()
        {
            velocity = recoilPosition = recoilVelocity = recoilAngle = recoilAngularVelocity = Vector3.zero;
            previous = default(Pose);
            phase = motionWeight = runWeight = actionAge = 0;
            lastActionId = lastImpactId = 0;
            lastActionKind = "";
            initialized = false;
            fallClockPresent = false;
        }

        public static float Duration(string kind)
        {
            switch (kind)
            {
                case "light": return .45f;
                case "heavy": return .65f;
                case "grab": case "grab_item": return .45f;
                case "release": case "escape": return .25f;
                case "equip": return .30f;
                case "throw": return .65f;
                case "pickup": return .55f;
                case "hurt": return .42f;
                case "jump": return .35f;
                case "fall": return 1.10f;
                // Sustained states end when the authority changes actionKind.
                case "charge": case "hold": case "eat": case "recover": return float.PositiveInfinity;
                default: return 0;
            }
        }

        public Pose Sample(Input input)
        {
            float dt = Mathf.Clamp(Finite(input.dt), 0, .10f);
            float now = Finite(input.time);
            string kind = input.actionKind ?? "";
            if (input.actionId != lastActionId || kind != lastActionKind)
            {
                actionAge = Mathf.Max(0, Finite(input.actionElapsed));
                lastActionId = input.actionId;
                lastActionKind = kind;
                fallClockPresent = false;
            }
            else actionAge += dt;
            // A negative elapsed explicitly asks for the local fallback clock.
            float elapsed = input.actionElapsed >= 0 && IsFinite(input.actionElapsed)
                ? input.actionElapsed : actionAge;
            if (kind == "fall" && input.staggerRemaining > 0) fallClockPresent = true;

            if (input.impactId != lastImpactId)
            {
                lastImpactId = input.impactId;
                if (input.impactId != 0)
                {
                    var hit = Vector3.ClampMagnitude(Clean(input.localImpact) / 6f, 1.5f);
                    recoilVelocity = Vector3.ClampMagnitude(recoilVelocity + hit * 1.6f, 2.4f);
                    recoilAngularVelocity += new Vector3(hit.z * 180, hit.x * 35, -hit.x * 170);
                    recoilAngularVelocity = Vector3.ClampMagnitude(recoilAngularVelocity, 320);
                }
            }
            // Exact damped-oscillator integration remains bounded across varying render rates.
            Spring(ref recoilPosition, ref recoilVelocity, 15f, .72f, dt);
            Spring(ref recoilAngle, ref recoilAngularVelocity, 14f, .68f, dt);
            LimitSpring(ref recoilPosition, ref recoilVelocity, .105f);
            LimitSpring(ref recoilAngle, ref recoilAngularVelocity, 19f);

            var requestedVelocity = Clean(input.localVelocity);
            requestedVelocity.y = 0;
            requestedVelocity = Vector3.ClampMagnitude(requestedVelocity, 15f);
            velocity = Vector3.Lerp(velocity, requestedVelocity, Damping(dt, .09f));
            float speed = velocity.magnitude;
            float moving = input.airborne ? 0 : Mathf.Clamp01(speed / 3f);
            motionWeight = Mathf.Lerp(motionWeight, moving, Damping(dt, moving > motionWeight ? .10f : .15f));
            runWeight = Mathf.Lerp(runWeight, Mathf.Clamp01(Finite(input.runBlend)), Damping(dt, .12f));
            if (!input.airborne && speed > .015f)
            {
                float radiansPerMeter=input.strideMeters>0?2*Mathf.PI/Mathf.Clamp(input.strideMeters,1.5f,3.5f):Mathf.Lerp(2.05f,1.95f,runWeight);
                phase = Mathf.Repeat(phase + speed * dt * radiansPerMeter, Mathf.PI * 2);
            }

            Pose target = Locomotion(velocity, now, input.airborne);
            ApplyAction(ref target, kind, elapsed, input.actionVariant,
                Mathf.Clamp01(Finite(input.grabTension)), Mathf.Clamp01(Finite(input.escapeProgress)), input.holdingGrab, now,
                fallClockPresent ? Mathf.Max(0, Finite(input.staggerRemaining)) : -1);

            // Changing action/state crossfades from the last displayed pose; gait phase survives.
            Pose result = initialized ? Blend(previous, target, Damping(dt, .035f)) : target;
            previous = result;
            initialized = true;
            // Recoil is added after pose filtering, never fed back into the next target pose.
            result.bodyOffset = Vector3.ClampMagnitude(result.bodyOffset + recoilPosition, .12f);
            result.bodyEuler += recoilAngle;
            result.headEuler -= recoilAngle * .48f;
            result.bodyEuler = Limit(result.bodyEuler, new Vector3(28, 25, 20));
            result.headEuler = Limit(result.headEuler, new Vector3(35, 20, 18));
            result.leftUpperArmEuler = Limit(result.leftUpperArmEuler, new Vector3(112, 38, 30));
            result.rightUpperArmEuler = Limit(result.rightUpperArmEuler, new Vector3(112, 38, 30));
            result.leftForearmEuler.x = Mathf.Clamp(result.leftForearmEuler.x, -112, 0);
            result.rightForearmEuler.x = Mathf.Clamp(result.rightForearmEuler.x, -112, 0);
            result.impactWeight = Mathf.Clamp01(recoilPosition.magnitude / .075f + recoilAngle.magnitude / 18f);
            result.phase = phase;
            return result;
        }

        Pose Locomotion(Vector3 localVelocity, float now, bool airborne)
        {
            float speed = localVelocity.magnitude;
            Vector3 direction = speed > .03f ? localVelocity / speed : Vector3.zero;
            float wave = Mathf.Sin(phase), walk = wave * motionWeight;
            float forward = direction.z, side = direction.x;
            float legAngle = Mathf.Lerp(27, 48, runWeight);
            float armAngle = Mathf.Lerp(22, 47, runWeight);
            float knee = Mathf.Lerp(35, 72, runWeight);
            float bend = Mathf.Lerp(-12, -68, runWeight * motionWeight);
            float bodyLean = Mathf.Lerp(2, 10, runWeight) * motionWeight;
            float leftLift = Mathf.Max(0, -wave) * motionWeight;
            float rightLift = Mathf.Max(0, wave) * motionWeight;
            var p = new Pose
            {
                bodyOffset = new Vector3(-walk * side * .015f,
                    (.5f - .5f * Mathf.Cos(phase * 2)) * motionWeight * Mathf.Lerp(.035f, .09f, runWeight), 0),
                bodyEuler = new Vector3(bodyLean * forward, walk * Mathf.Lerp(1.5f, 4, runWeight),
                    -bodyLean * side * .55f + walk * Mathf.Lerp(1, 3, runWeight)),
                headEuler = new Vector3(-bodyLean * forward * .7f, 0, bodyLean * side * .35f),
                // In reverse, the leg swing reverses rather than turning the character around.
                // During a strafe, abduction replaces most sagittal swing; both knees still lift.
                leftThighEuler = new Vector3(walk * legAngle * forward, side * 4 * motionWeight, walk * side * 23),
                rightThighEuler = new Vector3(-walk * legAngle * forward, side * 4 * motionWeight, -walk * side * 23),
                leftShinEuler = new Vector3(leftLift * knee, 0, 0),
                rightShinEuler = new Vector3(rightLift * knee, 0, 0),
                leftUpperArmEuler = new Vector3(-walk * armAngle * forward, -walk * side * 14, -(6 + runWeight * 7) - leftLift * Mathf.Abs(side) * 7),
                rightUpperArmEuler = new Vector3(walk * armAngle * forward, -walk * side * 14, 6 + runWeight * 7 + rightLift * Mathf.Abs(side) * 7),
                leftForearmEuler = new Vector3(bend + walk * runWeight * 8, 0, 0),
                rightForearmEuler = new Vector3(bend - walk * runWeight * 8, 0, 0),
                leftFootOffset = new Vector3(walk * side * .075f, leftLift * Mathf.Lerp(.045f, .10f, runWeight), walk * forward * .04f),
                rightFootOffset = new Vector3(-walk * side * .075f, rightLift * Mathf.Lerp(.045f, .10f, runWeight), -walk * forward * .04f),
                leftFootContact = 1 - Smooth(0, .3f, leftLift),
                rightFootContact = 1 - Smooth(0, .3f, rightLift),
                phase = phase
            };
            if (airborne)
            {
                p.leftThighEuler = new Vector3(-10, -4, -4);
                p.rightThighEuler = new Vector3(8, 4, 4);
                p.leftShinEuler = new Vector3(28, 0, 0);
                p.rightShinEuler = new Vector3(20, 0, 0);
                p.leftFootContact = p.rightFootContact = 0;
                p.leftFootOffset = p.rightFootOffset = Vector3.zero;
            }
            // A small breathing target prevents a perfectly frozen idle without drifting position.
            p.bodyOffset.y += Mathf.Sin(now * 2.2f) * .003f * (1 - motionWeight);
            return p;
        }

        static void ApplyAction(ref Pose p, string kind, float t, int variant, float tension, float escape, bool holding, float now, float staggerRemaining)
        {
            float weight = 0;
            bool left = (variant & 1) == 1;
            float side = left ? -1 : 1;
            Vector3 shoulder = Vector3.zero, elbow = Vector3.zero;
            switch (kind)
            {
                case "light":
                {
                    weight = Envelope(t, .035f, .18f, .45f);
                    float strike = Smooth(.055f, .10f, t);
                    shoulder = Vector3.Lerp(new Vector3(18, side * 24, side * 17), new Vector3(-88, -side * 15, side * 18), strike);
                    elbow = Vector3.Lerp(new Vector3(-88, 0, 0), new Vector3(-12, 0, 0), strike);
                    Arm(ref p, left, shoulder, elbow, weight);
                    p.bodyEuler += new Vector3(5 * strike, side * Mathf.Lerp(-10, 14, strike), -side * 3) * weight;
                    p.bodyOffset.z += .035f * strike * weight;
                    break;
                }
                case "heavy":
                {
                    weight = Envelope(t, .035f, .28f, .65f);
                    float strike = Smooth(.085f, .16f, t);
                    // A wide outside wind-up suits the oversized head: winding the fist
                    // behind the shoulder avoids forcing a short arm through the scalp.
                    shoulder = Vector3.Lerp(new Vector3(48, side * 27, side * 28), new Vector3(-72, -side * 25, side * 27), strike);
                    elbow = Vector3.Lerp(new Vector3(-102, 0, 0), new Vector3(-8, 0, 0), strike);
                    Arm(ref p, left, shoulder, elbow, weight);
                    Arm(ref p, !left, new Vector3(Mathf.Lerp(-24, 32, strike), -side * 16, -side * 26),
                        new Vector3(Mathf.Lerp(-86, -48, strike), 0, 0), weight * .92f);
                    p.bodyEuler += new Vector3(Mathf.Lerp(-10, 21, strike), side * Mathf.Lerp(-23, 23, strike), -side * Mathf.Lerp(6, 3, strike)) * weight;
                    p.headEuler += new Vector3(-6 * strike, -side * Mathf.Lerp(-6, 6, strike), 0) * weight;
                    float crouch = 1 - strike;
                    p.leftThighEuler.x -= 14 * crouch * weight;
                    p.rightThighEuler.x -= 14 * crouch * weight;
                    p.leftShinEuler.x += 32 * crouch * weight;
                    p.rightShinEuler.x += 32 * crouch * weight;
                    p.bodyOffset += new Vector3(0, Mathf.Lerp(-.06f, .012f, strike), Mathf.Lerp(-.025f, .085f, strike)) * weight;
                    break;
                }
                case "charge":
                {
                    weight = Smooth(0, .15f, t);
                    float power = Smooth(.1f, 1.2f, t);
                    Arm(ref p, left, new Vector3(-75 - power * 25, side * 25, side * 24), new Vector3(-92, 0, 0), weight);
                    Arm(ref p, !left, new Vector3(-28, -side * 14, -side * 18), new Vector3(-75, 0, 0), weight * .8f);
                    p.bodyEuler += new Vector3(-5 - power * 5, -side * 13, 0) * weight;
                    p.bodyOffset.y -= .035f * weight;
                    break;
                }
                case "grab": case "grab_item": case "hold":
                {
                    weight = kind == "hold" || holding ? Smooth(0, .14f, t) : Envelope(t, .17f, .23f, .45f);
                    float reach = Smooth(.06f, .20f, t);
                    BothArms(ref p, Mathf.Lerp(-30, -70, reach), Mathf.Lerp(-80, -28, reach), 16, weight);
                    p.bodyEuler.x += (4 - tension * 8) * weight;
                    p.bodyOffset.z += (.025f - tension * .05f) * weight;
                    p.bodyEuler.z += Mathf.Sin(now * 12) * tension * 1.6f * weight;
                    break;
                }
                case "release":
                {
                    weight = 1 - Smooth(0, .25f, t);
                    BothArms(ref p, -55, -42, 20, weight);
                    p.bodyOffset.z -= .025f * weight;
                    break;
                }
                case "escape":
                {
                    weight = Envelope(t, .035f, .10f, .25f);
                    float twist = Mathf.Sin(t / .25f * Mathf.PI * 2);
                    BothArms(ref p, -38, -84, 25, weight);
                    p.bodyEuler += new Vector3(-5, twist * 19, -twist * 6) * weight;
                    p.bodyOffset.z -= .055f * weight;
                    break;
                }
                case "throw":
                {
                    weight = Envelope(t, .04f, .28f, .65f);
                    float strike = Smooth(.09f, .17f, t);
                    BothArms(ref p, Mathf.Lerp(-108, -70, strike), Mathf.Lerp(-94, -10, strike), 21, weight);
                    p.bodyEuler.x += Mathf.Lerp(-9, 18, strike) * weight;
                    p.bodyOffset.z += .045f * strike * weight;
                    break;
                }
                case "pickup": case "recover":
                {
                    weight = kind == "recover" ? Smooth(0, .17f, t) : Envelope(t, .15f, .28f, .55f);
                    BothArms(ref p, -46, -32, 15, weight);
                    p.bodyEuler.x += 18 * weight;
                    p.headEuler.x += 6 * weight;
                    p.bodyOffset.y -= .07f * weight;
                    p.leftShinEuler.x += 16 * weight;
                    p.rightShinEuler.x += 16 * weight;
                    break;
                }
                case "equip":
                {
                    weight = Envelope(t, .065f, .12f, .30f);
                    Arm(ref p, left, new Vector3(-50, -side * 20, side * 20), new Vector3(-90, 0, 0), weight);
                    p.headEuler.x += 5 * weight;
                    break;
                }
                case "eat":
                {
                    // The existing large-mouth morph, arm stretch and feeding clearance IK
                    // own the final arms/head. This layer supplies only a gentle body target.
                    weight = Smooth(0, .18f, t);
                    p.feedingWeight = weight;
                    p.bodyEuler.x += 8 * weight;
                    p.bodyOffset.y += Mathf.Sin(t * 4.5f) * .01f * weight;
                    break;
                }
                case "hurt":
                {
                    weight = Envelope(t, .025f, .08f, .42f);
                    BothArms(ref p, -27, -76, 22, weight);
                    p.bodyEuler.x -= 7 * weight;
                    p.headEuler.x += 5 * weight;
                    break;
                }
                case "jump":
                {
                    weight = Envelope(t, .05f, .15f, .35f);
                    BothArms(ref p, -42, -40, 17, weight);
                    p.leftShinEuler.x += 15 * weight;
                    p.rightShinEuler.x += 15 * weight;
                    break;
                }
                case "fall":
                {
                    float release = staggerRemaining >= 0 ? Smooth(0, .30f, staggerRemaining) : 1 - Smooth(.38f, 1.10f, t);
                    weight = Smooth(0, .13f, t) * release;
                    p.fallWeight = weight;
                    p.fallOffset = new Vector3(side * .035f, -.38f, -.10f) * weight;
                    p.fallEuler = new Vector3(-38, 0, side * 12) * weight;
                    BothArms(ref p, -54, -70, 27, weight);
                    p.bodyEuler += new Vector3(-23, 0, side * 5) * weight;
                    p.headEuler.x += 9 * weight;
                    p.leftThighEuler = Vector3.Lerp(p.leftThighEuler, new Vector3(-42, -7, -8), weight);
                    p.rightThighEuler = Vector3.Lerp(p.rightThighEuler, new Vector3(-38, 7, 8), weight);
                    p.leftShinEuler = Vector3.Lerp(p.leftShinEuler, new Vector3(120, 0, 0), weight);
                    p.rightShinEuler = Vector3.Lerp(p.rightShinEuler, new Vector3(116, 0, 0), weight);
                    p.leftFootContact = p.rightFootContact = 1;
                    break;
                }
            }
            // During a held grab, strength and escape progress create controlled effort,
            // even when there is no newly-started one-shot animation to play.
            if (holding && kind != "eat" && kind != "release" && kind != "escape")
            {
                weight = Mathf.Max(weight, .85f);
                BothArms(ref p, -66, -35 - tension * 17, 18, .85f);
            }
            if (escape > 0 && kind != "escape")
            {
                float effort = Smooth(0, .15f, escape), wave = Mathf.Sin(now * 13);
                p.bodyEuler += new Vector3(-2, wave * 7, -wave * 3) * effort;
                p.headEuler.z += wave * 2 * effort;
            }
            p.upperBodyWeight = Mathf.Clamp01(weight);
        }

        static void Arm(ref Pose p, bool left, Vector3 shoulder, Vector3 elbow, float weight)
        {
            if (left)
            {
                p.leftUpperArmEuler = Vector3.Lerp(p.leftUpperArmEuler, shoulder, weight);
                p.leftForearmEuler = Vector3.Lerp(p.leftForearmEuler, elbow, weight);
            }
            else
            {
                p.rightUpperArmEuler = Vector3.Lerp(p.rightUpperArmEuler, shoulder, weight);
                p.rightForearmEuler = Vector3.Lerp(p.rightForearmEuler, elbow, weight);
            }
        }
        static void BothArms(ref Pose p, float shoulder, float elbow, float outward, float weight)
        {
            Arm(ref p, true, new Vector3(shoulder, -12, -outward), new Vector3(elbow, 0, 0), weight);
            Arm(ref p, false, new Vector3(shoulder, 12, outward), new Vector3(elbow, 0, 0), weight);
        }
        static Pose Blend(Pose a, Pose b, float w)
        {
            return new Pose
            {
                bodyOffset = Vector3.Lerp(a.bodyOffset, b.bodyOffset, w), bodyEuler = Vector3.Lerp(a.bodyEuler, b.bodyEuler, w),
                fallOffset = Vector3.Lerp(a.fallOffset, b.fallOffset, w), fallEuler = Vector3.Lerp(a.fallEuler, b.fallEuler, w), fallWeight = Mathf.Lerp(a.fallWeight, b.fallWeight, w),
                headEuler = Vector3.Lerp(a.headEuler, b.headEuler, w),
                leftUpperArmEuler = Vector3.Lerp(a.leftUpperArmEuler, b.leftUpperArmEuler, w), rightUpperArmEuler = Vector3.Lerp(a.rightUpperArmEuler, b.rightUpperArmEuler, w),
                leftForearmEuler = Vector3.Lerp(a.leftForearmEuler, b.leftForearmEuler, w), rightForearmEuler = Vector3.Lerp(a.rightForearmEuler, b.rightForearmEuler, w),
                leftThighEuler = Vector3.Lerp(a.leftThighEuler, b.leftThighEuler, w), rightThighEuler = Vector3.Lerp(a.rightThighEuler, b.rightThighEuler, w),
                leftShinEuler = Vector3.Lerp(a.leftShinEuler, b.leftShinEuler, w), rightShinEuler = Vector3.Lerp(a.rightShinEuler, b.rightShinEuler, w),
                leftFootOffset = Vector3.Lerp(a.leftFootOffset, b.leftFootOffset, w), rightFootOffset = Vector3.Lerp(a.rightFootOffset, b.rightFootOffset, w),
                upperBodyWeight = Mathf.Lerp(a.upperBodyWeight, b.upperBodyWeight, w), feedingWeight = Mathf.Lerp(a.feedingWeight, b.feedingWeight, w),
                leftFootContact = Mathf.Lerp(a.leftFootContact, b.leftFootContact, w), rightFootContact = Mathf.Lerp(a.rightFootContact, b.rightFootContact, w), phase = b.phase
            };
        }
        static void Spring(ref Vector3 position, ref Vector3 speed, float frequency, float damping, float dt)
        {
            float a = frequency * damping, b = frequency * Mathf.Sqrt(1 - damping * damping);
            float decay = Mathf.Exp(-a * dt), cosine = Mathf.Cos(b * dt), sine = Mathf.Sin(b * dt);
            Vector3 oldPosition = position, oldSpeed = speed;
            position = decay * (oldPosition * cosine + (oldSpeed + a * oldPosition) * (sine / b));
            speed = decay * (oldSpeed * cosine - (a * oldSpeed + frequency * frequency * oldPosition) * (sine / b));
        }
        static void LimitSpring(ref Vector3 position, ref Vector3 speed, float limit)
        {
            if (position.sqrMagnitude <= limit * limit) return;
            Vector3 normal = position.normalized;
            position = normal * limit;
            speed -= normal * Mathf.Max(0, Vector3.Dot(speed, normal));
        }
        static float Envelope(float t, float rise, float hold, float end)
        { return Smooth(0, rise, t) * (1 - Smooth(hold, end, t)); }
        static float Smooth(float from, float to, float value)
        { return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, value)); }
        static float Damping(float dt, float seconds) { return 1 - Mathf.Exp(-dt / seconds); }
        static Vector3 Limit(Vector3 value, Vector3 limit)
        { return new Vector3(Mathf.Clamp(value.x, -limit.x, limit.x), Mathf.Clamp(value.y, -limit.y, limit.y), Mathf.Clamp(value.z, -limit.z, limit.z)); }
        static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static float Finite(float value) { return IsFinite(value) ? value : 0; }
        static Vector3 Clean(Vector3 value) { return new Vector3(Finite(value.x), Finite(value.y), Finite(value.z)); }
    }
}
