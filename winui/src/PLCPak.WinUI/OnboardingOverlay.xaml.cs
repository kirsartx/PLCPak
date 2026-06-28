using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using PLCPak.WinUI.Infrastructure;

namespace PLCPak.WinUI;

public sealed partial class OnboardingOverlay : UserControl
{
    private int _step;
    private bool _isAnimating;
    private static readonly (string TitleKey, string BodyKey)[] Steps =
    [
        ("onboarding.s1.title", "onboarding.s1.body"),
        ("onboarding.s2.title", "onboarding.s2.body"),
        ("onboarding.s3.title", "onboarding.s3.body"),
        ("onboarding.s4.title", "onboarding.s4.body")
    ];

    public OnboardingOverlay()
    {
        InitializeComponent();
        LocalizationService.LanguageChanged += OnLanguageChanged;
        ThemeService.ThemeChanged += OnThemeChanged;
        Loaded += (_, _) =>
        {
            RefreshThemeBrushes();
            ApplyStepContent();
            EnsureCardVisible();
        };
        RefreshThemeBrushes();
        ApplyStepContent();
        EnsureCardVisible();
    }

    public event EventHandler? Completed;

    public void Show()
    {
        _step = 0;
        _isAnimating = false;
        Visibility = Visibility.Visible;
        RefreshThemeBrushes();
        ApplyStepContent();
        EnsureCardVisible();

        if (UiMotionHelper.ShouldReduceMotion())
            return;

        CardBorder.Opacity = 0;
        AnimateFadeIn(durationMs: 320);
        ScheduleVisibilityFallback();
    }

    public void SkipOrFinish()
    {
        if (Visibility != Visibility.Visible)
            return;

        Finish();
    }

    private void OnLanguageChanged() => ApplyStepContent();

    private void OnThemeChanged()
    {
        RefreshThemeBrushes();
        EnsureCardVisible();
    }

    private void RefreshThemeBrushes()
    {
        if (Application.Current?.Resources is not ResourceDictionary resources)
            return;

        CardBorder.Background = resources["PlcCardBackgroundBrush"] as Brush;
        CardBorder.BorderBrush = resources["PlcCardStrokeBrush"] as Brush;
        StepTitleText.Foreground = resources["PlcTitleBarTextBrush"] as Brush;
        StepBodyText.Foreground = resources["PlcSecondaryTextBrush"] as Brush;
        EscHintTextBlock.Foreground = resources["PlcSecondaryTextBrush"] as Brush;
        StepProgress.Foreground = resources["PlcAccentBrush"] as Brush;
    }

    private void ApplyStepContent()
    {
        var (titleKey, bodyKey) = Steps[_step];
        StepTitleText.Text = LocalizationService.T(titleKey);
        StepBodyText.Text = LocalizationService.T(bodyKey);
        StepProgress.Value = _step + 1;
        EscHintTextBlock.Text = LocalizationService.T("onboarding.escHint");
        SkipBtn.Content = LocalizationService.T("onboarding.skip");
        NextBtn.Content = _step >= Steps.Length - 1
            ? LocalizationService.T("onboarding.done")
            : LocalizationService.T("onboarding.next");
    }

    private void EnsureCardVisible()
    {
        if (Visibility != Visibility.Visible)
            return;

        CardBorder.Opacity = 1;
    }

    private void ScheduleVisibilityFallback()
    {
        var queue = DispatcherQueue.GetForCurrentThread();
        queue?.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (Visibility == Visibility.Visible && CardBorder.Opacity < 0.99)
                CardBorder.Opacity = 1;
        });
    }

    private void AnimateFadeIn(int durationMs)
    {
        RunOpacityAnimation(CardBorder.Opacity, 1, durationMs, EasingMode.EaseOut, _ =>
        {
            _isAnimating = false;
            EnsureCardVisible();
        });
    }

    private void AnimateStepTransition(Action onFadedOut)
    {
        if (UiMotionHelper.ShouldReduceMotion())
        {
            onFadedOut();
            ApplyStepContent();
            EnsureCardVisible();
            return;
        }

        _isAnimating = true;
        RunOpacityAnimation(CardBorder.Opacity, 0, 140, EasingMode.EaseIn, _ =>
        {
            onFadedOut();
            AnimateFadeIn(durationMs: 220);
            ScheduleVisibilityFallback();
        });
    }

    private void RunOpacityAnimation(
        double from,
        double to,
        int durationMs,
        EasingMode easingMode,
        Action<Storyboard>? onCompleted)
    {
        CardBorder.Opacity = from;

        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = easingMode }
        };
        Storyboard.SetTarget(animation, CardBorder);
        Storyboard.SetTargetProperty(animation, "Opacity");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) =>
        {
            CardBorder.Opacity = to;
            onCompleted?.Invoke(storyboard);
        };

        try
        {
            _isAnimating = true;
            storyboard.Begin();
        }
        catch (ArgumentException)
        {
            CardBorder.Opacity = to;
            _isAnimating = false;
            onCompleted?.Invoke(storyboard);
        }
    }

    private void NextBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isAnimating)
            return;

        if (_step >= Steps.Length - 1)
        {
            Finish();
            return;
        }

        AnimateStepTransition(() =>
        {
            _step++;
            ApplyStepContent();
        });
    }

    private void SkipBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isAnimating)
            return;

        Finish();
    }

    private void Finish()
    {
        _isAnimating = false;
        EnsureCardVisible();
        Visibility = Visibility.Collapsed;
        Completed?.Invoke(this, EventArgs.Empty);
    }
}