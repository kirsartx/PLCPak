using Microsoft.UI.Xaml;
using Windows.Devices.Haptics;

namespace PLCPak.WinUI.Infrastructure;

public static class WizardCompletionFeedback
{
    public static void Play()
    {
        try
        {
            var prefs = App.Services.UiPreferences.Load();
            if (!prefs.EnableWizardCompletionFeedback)
                return;

            PlaySound();
            _ = TryPlayHapticAsync();
        }
        catch
        {
            // Feedback is best-effort; never break wizard flow.
        }
    }

    private static void PlaySound()
    {
        try
        {
            if (ElementSoundPlayer.State == ElementSoundPlayerState.Auto)
                ElementSoundPlayer.State = ElementSoundPlayerState.On;

            ElementSoundPlayer.Play(ElementSoundKind.Show);
        }
        catch
        {
        }
    }

    private static async Task TryPlayHapticAsync()
    {
        try
        {
            var device = await VibrationDevice.GetDefaultAsync();
            if (device is null)
                return;

            var controller = device.SimpleHapticsController;
            if (controller is null)
                return;

            var preferredWaveform = KnownSimpleHapticsControllerWaveforms.Click;
            SimpleHapticsControllerFeedback? feedback = null;
            foreach (var supported in controller.SupportedFeedback)
            {
                if (supported.Waveform == preferredWaveform)
                {
                    feedback = supported;
                    break;
                }
            }

            feedback ??= controller.SupportedFeedback.FirstOrDefault();
            if (feedback is null)
                return;

            if (controller.IsIntensitySupported)
                controller.SendHapticFeedback(feedback, 0.5);
            else
                controller.SendHapticFeedback(feedback);
        }
        catch
        {
            // Desktop or unavailable — no-op.
        }
    }
}