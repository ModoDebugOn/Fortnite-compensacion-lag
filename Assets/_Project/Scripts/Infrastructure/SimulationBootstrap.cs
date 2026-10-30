using System.Collections.Generic;
using LagCompensation.Application;
using LagCompensation.Application.UseCases;
using LagCompensation.Domain.Movement;
using LagCompensation.Domain.Timing;
using LagCompensation.Presentation;
using LagCompensation.Presentation.Hud;
using LagCompensation.Presentation.Players;
using LagCompensation.Presentation.Screens;
using LagCompensation.Presentation.Shots;
using UnityEngine;
using Box = LagCompensation.Domain.Hit.Box;
using Pose = LagCompensation.Domain.Movement.Pose;

namespace LagCompensation.Infrastructure
{
    /// <summary>
    /// Composition root of the demo: builds the server with its clock, connects the two players
    /// (Peely standing, Raven on her scripted route), binds the views and drives it all frame by frame.
    ///
    /// The screen is split in three, one per version of the match: Peely's screen, Raven's screen and
    /// the server's. Each one shows both players as that machine knows them. The ghosts (key G) show
    /// the other versions of Raven on top, to compare.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SimulationBootstrap : MonoBehaviour
    {
        const string ShooterName = "Peely";
        const string RunnerName = "Raven";

        /// <summary>The two characters as one screen shows them.</summary>
        [System.Serializable]
        sealed class ScreenCast
        {
            public PlayerView shooter;
            public PlayerView runner;
        }

        [Header("Screens")]
        [Tooltip("Peely's screen: Peely where he is, Raven as she reaches him. Peely's start pose is taken from here.")]
        [SerializeField] ScreenCast shooterScreen;
        [Tooltip("Raven's screen: Raven where she really is, Peely as he reaches her. Raven's start pose is taken from here.")]
        [SerializeField] ScreenCast runnerScreen;
        [Tooltip("The server's screen: both players as the server knows them.")]
        [SerializeField] ScreenCast serverScreen;
        [Tooltip("The camera of Raven's screen, which follows her.")]
        [SerializeField] FollowCamera runnerCamera;
        [Tooltip("All the screens, to switch their ghosts on and off together.")]
        [SerializeField] GameScreen[] screens;
        [Tooltip("The settings on screen (ping, interpolation...), so they show up in the video. Key H.")]
        [SerializeField] SettingsPanel settingsPanel;

        [Header("Ghosts of the runner")]
        [Tooltip("Where the runner really is (ghost in her colour).")]
        [SerializeField] PlayerView runnerReal;
        [Tooltip("Where the server thinks the runner is (white ghost).")]
        [SerializeField] PlayerView runnerOnServer;
        [Tooltip("Where the shooter sees the runner (ghost in the shooter's colour).")]
        [SerializeField] PlayerView runnerSeenByShooter;

        [Header("Shooting")]
        [Tooltip("Shows the server's verdict on each shot.")]
        [SerializeField] ShotView shotView;
        [Tooltip("Peely fires on his own every few seconds (repeatable takes). F fires by hand anyway.")]
        [SerializeField] bool autoFire;
        [Tooltip("Simulation seconds between automatic shots.")]
        [SerializeField, Range(0.2f, 5f)] float autoFireEvery = 1f;
        [Tooltip("Peely fires the moment Raven gets behind cover on her own screen: the death behind the wall.")]
        [SerializeField] bool fireWhenRunnerHides;
        [Tooltip("Everything solid that stops shots (walls, crates): every BoxCollider under it.")]
        [SerializeField] Transform solids;

        [Header("Lag compensation")]
        [Tooltip("The server rewinds to what the shooter saw before checking a shot (key C).")]
        [SerializeField] bool lagCompensation = true;
        [Tooltip("The furthest back the server agrees to rewind. With more lag than this, shots stop being compensated.")]
        [SerializeField, Range(0f, 1000f)] float maxRewindMs = 1000f;
        [Tooltip("The server's album of the runner, drawn as a trail of ghosts on the server's screen.")]
        [SerializeField] HistoryTrailView runnerHistory;

        [Header("Runner's route")]
        [Tooltip("Parent of the route's stops (RouteStopMarker children, in order).")]
        [SerializeField] Transform route;
        [SerializeField, Range(1f, 10f)] float runSpeed = 5f;

        [Header("Network")]
        [Tooltip("Round trip to the server, the same for both players. Each way is half of it.")]
        [SerializeField, Range(0f, MaxPing)] float pingMs = 80f;
        [Tooltip("Ping added or removed with the arrow keys.")]
        [SerializeField, Range(5f, 100f)] float pingStepMs = 20f;
        [Tooltip("Updates per second, both ways (clients → server and server → clients).")]
        [SerializeField, Range(1f, 60f)] float tickRate = 30f;

        [Header("Interpolation")]
        [Tooltip("Draw the others moving smoothly between snapshots, instead of jumping to the newest one.")]
        [SerializeField] bool interpolate = true;
        [Tooltip("How far in the past the others are drawn on purpose. Usually two snapshots' worth.")]
        [SerializeField, Range(0f, 300f)] float interpolationMs = 100f;

        [Header("Time")]
        [Tooltip("Simulation seconds per step when advancing frame by frame.")]
        [SerializeField, Range(0.005f, 0.1f)] float stepSeconds = 1f / 60f;

        const float MaxPing = 400f;

        public ServerWorld World { get; private set; }

        /// <summary>The ping currently in use, in milliseconds (for HUDs).</summary>
        public float PingMs => pingMs;

        SimClock Clock => World.Clock;
        AdvanceSimulation advance;
        FireShot fire;
        GameClient shooterClient, runnerClient;
        double nextAutoFire;
        bool runnerWasVisible;

        void Awake()
        {
            World = new ServerWorld(new SimClock(), tickRate);
            advance = new AdvanceSimulation(World);
            fire = new FireShot(World);

            shooterClient = World.Connect(ShooterName, PoseOf(shooterScreen.shooter.transform), pingMs / 1000.0);
            runnerClient = World.Connect(RunnerName, PoseOf(runnerScreen.runner.transform), pingMs / 1000.0);
            runnerClient.SetRoute(BuildRoute(), Clock.Now);
            shooterClient.SetAimTarget(RunnerName);   // Peely keeps aiming at the Raven he sees
            AddSolids();
            ApplyNetworkSettings();
            BindViews();
        }

        /// <summary>Tells the server about every wall and crate in the scene (their box colliders).
        /// They may be turned around the vertical axis, but not tilted.</summary>
        void AddSolids()
        {
            if (solids == null) return;
            foreach (var box in solids.GetComponentsInChildren<BoxCollider>())
            {
                var t = box.transform;
                World.AddWall(new Box(t.TransformPoint(box.center).ToNumerics(),
                                      Vector3.Scale(box.size, t.lossyScale).ToNumerics(),
                                      t.eulerAngles.y));
            }
        }

        /// <summary>True on the frame Raven, on her own screen, goes from visible to hidden for Peely.</summary>
        bool RunnerJustHid()
        {
            var eye = shooterClient.LocalPose.Position + FireShot.EyeHeight;
            bool visible = World.CanSee(eye, runnerClient.LocalPose.Position);
            bool justHid = runnerWasVisible && !visible;
            runnerWasVisible = visible;
            return justHid;
        }

        /// <summary>Tells every view which version of which player it shows.</summary>
        void BindViews()
        {
            var serverShooter = World.FindPlayer(ShooterName);
            var serverRunner = World.FindPlayer(RunnerName);

            // Each player sees itself where it is, and the other one as the server has told it.
            shooterScreen.shooter.Bind(() => shooterClient.LocalPose);
            shooterScreen.runner.Bind(() => Seen(shooterClient, RunnerName));
            runnerScreen.runner.Bind(() => runnerClient.LocalPose);
            runnerScreen.shooter.Bind(() => Seen(runnerClient, ShooterName));
            serverScreen.shooter.Bind(() => serverShooter.Pose);
            serverScreen.runner.Bind(() => serverRunner.Pose);
            if (runnerCamera != null) runnerCamera.Bind(() => runnerClient.LocalPose);

            if (runnerReal != null) runnerReal.Bind(() => runnerClient.LocalPose);
            if (runnerOnServer != null) runnerOnServer.Bind(() => serverRunner.Pose);
            if (runnerSeenByShooter != null) runnerSeenByShooter.Bind(() => Seen(shooterClient, RunnerName));

            if (shotView != null) shotView.Bind(() => World.Shots, () => Clock.Paused);
            if (runnerHistory != null) runnerHistory.Bind(() => serverRunner.History);
            if (settingsPanel != null) settingsPanel.Bind(World, shooterClient);
        }

        Pose? Seen(GameClient viewer, string player) =>
            viewer.TryGetSeen(player, Clock.Now, out var pose) ? pose : (Pose?)null;

        void Update()
        {
            if (DemoInput.TogglePausePressed) Clock.TogglePause();

            float scale = DemoInput.TimeScalePressed;
            if (scale > 0f) Clock.SetTimeScale(scale);

            int pingStep = DemoInput.PingStepPressed;
            if (pingStep != 0)
            {
                pingMs = Mathf.Clamp(pingMs + pingStep * pingStepMs, 0f, MaxPing);
                ApplyNetworkSettings();
            }

            if (DemoInput.ToggleInterpolationPressed)
            {
                interpolate = !interpolate;
                ApplyNetworkSettings();
            }

            if (DemoInput.ToggleGhostsPressed)
                foreach (var screen in screens) screen.ShowGhosts = !screen.ShowGhosts;

            if (DemoInput.ToggleCompensationPressed)
            {
                lagCompensation = !lagCompensation;
                ApplyNetworkSettings();
            }

            if (DemoInput.ToggleSettingsPanelPressed && settingsPanel != null)
                settingsPanel.Visible = !settingsPanel.Visible;

            if (DemoInput.FirePressed) fire.Execute(shooterClient, RunnerName);
            if (RunnerJustHid() && fireWhenRunnerHides) fire.Execute(shooterClient, RunnerName);
            if (autoFire && Clock.Now >= nextAutoFire)
            {
                fire.Execute(shooterClient, RunnerName);
                nextAutoFire = Clock.Now + autoFireEvery;
            }

            if (Clock.Paused && DemoInput.StepFramePressed) advance.Step(stepSeconds);
            else advance.Execute(Time.deltaTime);
        }

        /// <summary>Sliders moved in the inspector while playing.</summary>
        void OnValidate()
        {
            if (World == null) return;
            ApplyNetworkSettings();
            runnerClient.SetRoute(BuildRoute(), Clock.Now);
        }

        void ApplyNetworkSettings()
        {
            foreach (var client in World.Clients)
            {
                client.SetPing(pingMs / 1000.0);
                client.SetInterpolation(interpolate, interpolationMs / 1000.0);
            }
            World.Tick.SetRate(tickRate);
            World.LagCompensation = lagCompensation;
            World.MaxRewind = maxRewindMs / 1000.0;
        }

        Route BuildRoute()
        {
            var stops = new List<RouteStop>();
            foreach (var marker in route.GetComponentsInChildren<RouteStopMarker>())
                stops.Add(new RouteStop(marker.transform.position.ToNumerics(), marker.Wait));
            return new Route(stops, runSpeed);
        }

        static Pose PoseOf(Transform t)
        {
            var forward = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            return new Pose(t.position.ToNumerics(), forward.ToNumerics());
        }
    }
}
