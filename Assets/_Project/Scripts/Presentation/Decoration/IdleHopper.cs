using UnityEngine;

namespace LagCompensation.Presentation.Decoration
{
    /// <summary>
    /// Decorative character: every now and then it does a few hops, turning and wandering a
    /// little around the spot where it was placed, with a bit of squash and stretch.
    /// Pure eye candy for the background: it takes no part in the lag simulation.
    ///
    /// Expects a Unity capsule (height 2, pivot in the middle) standing on flat ground.
    /// </summary>
    public sealed class IdleHopper : MonoBehaviour
    {
        [Header("Hop")]
        [SerializeField, Range(0.1f, 2f)] float hopHeight = 0.6f;
        [SerializeField, Range(0.15f, 1.5f)] float hopDuration = 0.45f;
        [Tooltip("Seconds standing still between bursts of hops (min, max).")]
        [SerializeField] Vector2 pauseRange = new Vector2(0.5f, 3f);
        [SerializeField, Range(1, 6)] int maxHopsInARow = 3;

        [Header("Wander")]
        [Tooltip("How far it may move away from where it was placed.")]
        [SerializeField, Range(0f, 5f)] float wanderRadius = 1.5f;
        [SerializeField, Range(0f, 2f)] float stepLength = 0.6f;
        [Tooltip("Chance that a hop ends with a full spin, like a victory emote.")]
        [SerializeField, Range(0f, 1f)] float spinChance = 0.15f;

        [Header("Squash and stretch")]
        [SerializeField, Range(0f, 0.5f)] float squash = 0.2f;
        [SerializeField, Range(0.05f, 0.5f)] float landingTime = 0.15f;

        const float CapsuleHalfHeight = 1f;   // Unity's capsule: 2 units tall, pivot in the centre

        Vector3 home, baseScale;
        Vector3 ground;            // where it stands now (y = home.y), before squash and jump
        Vector3 hopFrom, hopTo;
        float yawFrom, yawTo;
        float timer, wait;
        int hopsLeft;
        bool inAir;

        void Start()
        {
            home = transform.position;
            ground = home;
            baseScale = transform.localScale;
            wait = Random.Range(0f, pauseRange.y);   // so they never hop in sync
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (inAir) Fly(); else Rest();
        }

        /// <summary>On the ground: squash after landing, then wait for the next hop.</summary>
        void Rest()
        {
            float land = 1f - Mathf.Clamp01(timer / landingTime);   // 1 just landed -> 0
            Pose(ground, 0f, 1f - squash * land);

            if (timer < wait) return;
            if (hopsLeft <= 0) hopsLeft = Random.Range(1, maxHopsInARow + 1);
            StartHop();
        }

        /// <summary>Picks where the next hop lands: a short step, pulled back home if too far.</summary>
        void StartHop()
        {
            hopFrom = ground;
            Vector2 random = Random.insideUnitCircle * stepLength;
            hopTo = hopFrom + new Vector3(random.x, 0f, random.y);
            if (Vector3.Distance(hopTo, home) > wanderRadius) hopTo = hopFrom + (home - hopFrom).normalized * stepLength;

            yawFrom = transform.eulerAngles.y;
            Vector3 dir = hopTo - hopFrom;
            yawTo = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir).eulerAngles.y : yawFrom;
            yawTo = yawFrom + Mathf.DeltaAngle(yawFrom, yawTo);
            if (Random.value < spinChance) yawTo += 360f;

            inAir = true;
            timer = 0f;
        }

        /// <summary>In the air: a parabola from takeoff to landing, stretched when moving fast.</summary>
        void Fly()
        {
            float t = Mathf.Clamp01(timer / hopDuration);
            ground = Vector3.Lerp(hopFrom, hopTo, t);
            float jump = 4f * hopHeight * t * (1f - t);    // 0 at both ends, hopHeight at t = 0.5

            float speed = Mathf.Abs(2f * t - 1f);          // fast near the ground, still at the top
            Pose(ground, jump, 1f + squash * 0.5f * speed);
            transform.rotation = Quaternion.Euler(0f, Mathf.Lerp(yawFrom, yawTo, Mathf.SmoothStep(0f, 1f, t)), 0f);

            if (t < 1f) return;
            inAir = false;
            timer = 0f;
            hopsLeft--;
            wait = hopsLeft > 0 ? 0.05f : Random.Range(pauseRange.x, pauseRange.y);
        }

        /// <summary>Places the capsule `jump` units above `groundPoint` and scales it (keeping
        /// its volume) so its feet stay where they were.</summary>
        void Pose(Vector3 groundPoint, float jump, float stretchY)
        {
            float widen = 1f / Mathf.Sqrt(stretchY);
            transform.localScale = Vector3.Scale(baseScale, new Vector3(widen, stretchY, widen));
            float halfHeight = CapsuleHalfHeight * baseScale.y;
            float feet = home.y - halfHeight;
            transform.position = new Vector3(groundPoint.x, feet + halfHeight * stretchY + jump, groundPoint.z);
        }
    }
}
