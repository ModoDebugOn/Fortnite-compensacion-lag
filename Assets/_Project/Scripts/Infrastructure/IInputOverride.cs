namespace LagCompensation.Infrastructure
{
    /// <summary>
    /// Another source of input that can take over from the keyboard: the recording kit, a test...
    /// While it is Active, <see cref="DemoInput"/> ignores the real devices.
    /// Nothing in the demo uses it; it is there so other tools can drive the demo.
    /// </summary>
    public interface IInputOverride
    {
        bool Active { get; }

        /// <summary>True on the frame the pause should toggle.</summary>
        bool TogglePause { get; }

        /// <summary>True on the frame the clock should move one step (while paused).</summary>
        bool StepFrame { get; }

        /// <summary>The time scale to switch to this frame, or 0 to keep the current one.</summary>
        float TimeScale { get; }

        /// <summary>Ping steps this frame: +1 up, -1 down, 0 unchanged.</summary>
        int PingStep { get; }

        /// <summary>True on the frame interpolation should switch on or off.</summary>
        bool ToggleInterpolation { get; }

        /// <summary>True on the frame the ghosts should switch on or off.</summary>
        bool ToggleGhosts { get; }

        /// <summary>True on the frame the shooter should fire.</summary>
        bool Fire { get; }

        /// <summary>True on the frame lag compensation should switch on or off.</summary>
        bool ToggleCompensation { get; }

        /// <summary>True on the frame the settings panel should show or hide.</summary>
        bool ToggleSettingsPanel { get; }
    }
}
