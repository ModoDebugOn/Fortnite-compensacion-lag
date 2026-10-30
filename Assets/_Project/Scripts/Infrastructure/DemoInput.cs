using UnityEngine.InputSystem;

namespace LagCompensation.Infrastructure
{
    /// <summary>
    /// Reads the keyboard for the demo controls:
    /// - Space: pause / resume the simulation.
    /// - . (period): one step forward while paused.
    /// - 1, 2, 3, 4: speed x1, x0.5, x0.25, x0.1 (slow motion to watch the packets travel).
    /// - Up / Down arrows: more / less ping.
    /// - I: interpolation on / off.
    /// - G: ghosts of the other versions on / off, in every screen.
    /// - F: Peely fires at the Raven he sees.
    /// - C: lag compensation (the server rewinding) on / off.
    /// - H: settings panel (ping, interpolation...) shown / hidden.
    /// </summary>
    public static class DemoInput
    {
        /// <summary>Optional input that replaces the keyboard while it is active.</summary>
        public static IInputOverride Override { get; set; }

        static bool Overridden => Override != null && Override.Active;

        static Keyboard Keys => Keyboard.current;

        public static bool TogglePausePressed =>
            Overridden ? Override.TogglePause : Keys != null && Keys.spaceKey.wasPressedThisFrame;

        public static bool StepFramePressed =>
            Overridden ? Override.StepFrame : Keys != null && Keys.periodKey.wasPressedThisFrame;

        /// <summary>The speed picked this frame, or 0 if none.</summary>
        public static float TimeScalePressed
        {
            get
            {
                if (Overridden) return Override.TimeScale;
                if (Keys == null) return 0f;
                if (Keys.digit1Key.wasPressedThisFrame) return 1f;
                if (Keys.digit2Key.wasPressedThisFrame) return 0.5f;
                if (Keys.digit3Key.wasPressedThisFrame) return 0.25f;
                if (Keys.digit4Key.wasPressedThisFrame) return 0.1f;
                return 0f;
            }
        }

        public static bool ToggleInterpolationPressed =>
            Overridden ? Override.ToggleInterpolation : Keys != null && Keys.iKey.wasPressedThisFrame;

        public static bool ToggleGhostsPressed =>
            Overridden ? Override.ToggleGhosts : Keys != null && Keys.gKey.wasPressedThisFrame;

        public static bool FirePressed =>
            Overridden ? Override.Fire : Keys != null && Keys.fKey.wasPressedThisFrame;

        public static bool ToggleCompensationPressed =>
            Overridden ? Override.ToggleCompensation : Keys != null && Keys.cKey.wasPressedThisFrame;

        public static bool ToggleSettingsPanelPressed =>
            Overridden ? Override.ToggleSettingsPanel : Keys != null && Keys.hKey.wasPressedThisFrame;

        /// <summary>+1 more ping, -1 less, 0 unchanged.</summary>
        public static int PingStepPressed
        {
            get
            {
                if (Overridden) return System.Math.Sign(Override.PingStep);
                if (Keys == null) return 0;
                if (Keys.upArrowKey.wasPressedThisFrame) return 1;
                if (Keys.downArrowKey.wasPressedThisFrame) return -1;
                return 0;
            }
        }
    }
}
