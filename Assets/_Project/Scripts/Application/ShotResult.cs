using LagCompensation.Domain.Hit;
using LagCompensation.Domain.Movement;

namespace LagCompensation.Application
{
    /// <summary>What the server decided about one shot, and what it used to decide it.</summary>
    public sealed class ShotResult
    {
        public string Shooter { get; }
        public Ray Ray { get; }

        /// <summary>When it was fired (on the shooter's machine) and when the server checked it.</summary>
        public double FiredAt { get; }
        public double ResolvedAt { get; }

        /// <summary>The player the shot was checked against: the one hit, or the one it passed closest to.</summary>
        public string Target { get; }

        /// <summary>Where the server had the target when it checked the shot.</summary>
        public Pose TargetPose { get; }

        public bool Hit { get; }

        /// <summary>How far the shot passed from the centre of the target (metres).</summary>
        public float Miss { get; }

        /// <summary>The moment the server rewound to (what the shooter was seeing), or null if it did not rewind.</summary>
        public double? RewoundTo { get; }

        public bool Rewound => RewoundTo.HasValue;

        /// <summary>It would have hit, but a wall was in the way.</summary>
        public bool BlockedByWall { get; }

        /// <summary>The shooter was seeing further back than the server agrees to rewind: it only
        /// rewound as far as its limit.</summary>
        public bool RewindLimited { get; }

        public ShotResult(string shooter, Ray ray, double firedAt, double resolvedAt,
                          string target, Pose targetPose, bool hit, float miss, double? rewoundTo = null,
                          bool blockedByWall = false, bool rewindLimited = false)
        {
            RewoundTo = rewoundTo;
            BlockedByWall = blockedByWall;
            RewindLimited = rewindLimited;
            Shooter = shooter;
            Ray = ray;
            FiredAt = firedAt;
            ResolvedAt = resolvedAt;
            Target = target;
            TargetPose = targetPose;
            Hit = hit;
            Miss = miss;
        }

        public override string ToString() =>
            $"{Shooter} → {Target}: {(Hit ? "HIT" : BlockedByWall ? "WALL" : "MISS")} ({Miss:0.00} m from centre){(Rewound ? $", rewound {ResolvedAt - RewoundTo:0.000} s" : "")}{(RewindLimited ? " (limit)" : "")}";
    }
}
