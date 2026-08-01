using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;

namespace SophieDesktopPet;

internal sealed class PetWindow : Window
{
    private const int IdleTicks = 200;
    private const int FrameHoldTickCount = 5;
    private static readonly TimeSpan NormalWeatherRefresh =
        TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FailedWeatherRefresh =
        TimeSpan.FromMinutes(2);

    private readonly WorldControl _world = new();
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _weatherTimer = new();
    private readonly Random _random = new();
    private readonly WeatherService _weatherService = new();
    private readonly CancellationTokenSource _weatherLifetime = new();
    private readonly UpdateService _updateService =
        new(AppVersion.Current);
    private readonly CancellationTokenSource _updateLifetime = new();

    private PetState _state = PetState.Idle;
    private int _frame;
    private int _frameDelayTicks;
    private int _ticksRemaining;
    private bool _autoLife = true;
    private bool _automaticWeather = true;
    private bool _weatherRefreshRunning;
    private bool _isClosing;
    private bool _weatherResourcesDisposed;
    private bool _moving;
    private LifePhase _lifePhase = LifePhase.IdleBeat;
    private LiveWeather? _lastGoodWeather;
    private PetState? _pendingActivity;
    private int _pendingActivityTicks;
    private double _walkTargetX = double.NaN;
    private MenuItem? _worldToggleMenu;
    private MenuItem? _weatherStatusMenu;
    private MenuItem? _updateMenu;
    private Task? _updateMonitorTask;

    public PetWindow()
    {
        Title = "Sophie's Little World";
        Width = 150;
        Height = 160;
        MinWidth = Width;
        MinHeight = Height;
        MaxWidth = Width;
        MaxHeight = Height;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        CanResize = false;
        Topmost = true;
        ShowInTaskbar = false;
        ExtendClientAreaToDecorationsHint = true;
        Content = _world;

        ConfigureInput();
        ConfigureMenu();
        _updateService.StatusChanged += (_, status) =>
            Dispatcher.UIThread.Post(() => ApplyUpdateStatus(status));
        Opened += async (_, _) =>
        {
            PlaceNearBottomRight();
            StartUpdateMonitor();
            await RefreshWeatherAsync();
        };
        Closed += (_, _) =>
        {
            _isClosing = true;
            _weatherTimer.Stop();
            _weatherLifetime.Cancel();
            _updateLifetime.Cancel();
            if (!_weatherRefreshRunning)
            {
                DisposeWeatherResources();
            }
        };

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += (_, _) => Tick();
        _weatherTimer.Tick += async (_, _) =>
        {
            _weatherTimer.Stop();
            await RefreshWeatherAsync();
        };
        BeginIdleBeat();
        _timer.Start();
    }

    private void ConfigureInput()
    {
        _world.PointerPressed += (_, e) =>
        {
            var point = e.GetCurrentPoint(_world);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (e.ClickCount >= 2 && _world.IsOverPet(point.Position))
            {
                ToggleWorld();
                e.Handled = true;
                return;
            }

            BeginMoveDrag(e);
        };
    }

    private void ConfigureMenu()
    {
        var worldToggle = new MenuItem
        {
            Header = "打开 Sophie 的小世界"
        };
        _worldToggleMenu = worldToggle;
        worldToggle.Click += (_, _) => ToggleWorld();

        var autoLife = new MenuItem
        {
            Header = "自动生活",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = true
        };
        autoLife.Click += (_, _) =>
        {
            _autoLife = autoLife.IsChecked;
            BeginIdleBeat();
        };

        var dayNight = new MenuItem { Header = "切换昼夜" };
        dayNight.Click += (_, _) =>
        {
            DisableAutomaticWeather("天气：手动昼夜");
            _world.ToggleDayNight();
        };

        var clearWeather = new MenuItem { Header = "天气：晴天" };
        clearWeather.Click += (_, _) =>
            SetManualWeather(WeatherMode.Clear, "晴天");

        var rainWeather = new MenuItem { Header = "天气：像素雨" };
        rainWeather.Click += (_, _) =>
            SetManualWeather(WeatherMode.Rain, "像素雨");

        var snowWeather = new MenuItem { Header = "天气：像素雪" };
        snowWeather.Click += (_, _) =>
            SetManualWeather(WeatherMode.Snow, "像素雪");

        var automaticWeather = new MenuItem
        {
            Header = "恢复自动定位天气"
        };
        automaticWeather.Click += async (_, _) =>
            await RestoreAutomaticWeatherAsync();

        _weatherStatusMenu = new MenuItem
        {
            Header = "天气：正在自动定位…",
            IsEnabled = false
        };

        _updateMenu = new MenuItem
        {
            Header = "更新：准备检查…",
            IsEnabled = false
        };
        _updateMenu.Click += async (_, _) => await HandleUpdateMenuAsync();

        var quit = new MenuItem { Header = "离开小世界" };
        quit.Click += (_, _) =>
            (Application.Current?.ApplicationLifetime as
                IClassicDesktopStyleApplicationLifetime)?.Shutdown();

        _world.ContextMenu = new ContextMenu
        {
            ItemsSource = new object[]
            {
                worldToggle,
                new Separator(),
                autoLife,
                _weatherStatusMenu,
                dayNight,
                clearWeather,
                rainWeather,
                snowWeather,
                automaticWeather,
                new Separator(),
                _updateMenu,
                new Separator(),
                quit
            }
        };
    }

    private void Tick()
    {
        var animation = LifeAnimations.For(_state);
        _world.ShowFrame(animation.Row, _frame);
        _world.AdvanceEnvironment();
        _frameDelayTicks++;
        if (_frameDelayTicks >= FrameHoldTicks(_state))
        {
            _frameDelayTicks = 0;
            _frame++;
            if (_frame >= animation.Frames)
            {
                _frame = animation.Loops ? 0 : animation.Frames - 1;
            }
        }

        if (_moving)
        {
            if (_world.IsWorldOpen)
            {
                WalkInsideWorld();
            }
            else
            {
                WalkOnMiniGround();
            }
        }

        _ticksRemaining--;
        if (_ticksRemaining <= 0)
        {
            AdvanceLifeCycle();
        }
    }

    private void AdvanceLifeCycle()
    {
        if (!_autoLife)
        {
            BeginIdleBeat();
            return;
        }

        if (_lifePhase == LifePhase.Activity)
        {
            BeginIdleBeat();
            return;
        }

        if (_world.IsWorldOpen)
        {
            ChooseNextIslandActivity();
            return;
        }

        ChooseNextDesktopActivity();
    }

    private void BeginIdleBeat()
    {
        _lifePhase = LifePhase.IdleBeat;
        _pendingActivity = null;
        _walkTargetX = double.NaN;
        StartState(SelectIdleVisual(), IdleTicks);
    }

    private void RefreshIdleVisual()
    {
        if (_lifePhase != LifePhase.IdleBeat)
        {
            return;
        }

        var remaining = Math.Max(1, _ticksRemaining);
        StartState(SelectIdleVisual(), remaining);
    }

    private PetState SelectIdleVisual()
    {
        return PetState.Idle;
    }

    private void BeginActivity(PetState state, int ticks)
    {
        if (state == PetState.Idle)
        {
            throw new ArgumentException(
                "随机生活动作不能是待机。", nameof(state));
        }

        _lifePhase = LifePhase.Activity;
        _pendingActivity = null;
        _walkTargetX = double.NaN;
        StartState(state, ticks);
    }

    private void StartState(PetState state, int ticks)
    {
        _state = state;
        _frame = 0;
        _frameDelayTicks = 0;
        _ticksRemaining = Math.Max(1, ticks);
        _moving = state is PetState.RunningLeft or PetState.RunningRight;
    }

    private void ChooseNextIslandActivity()
    {
        if (TryChooseWeatherActivity(island: true))
        {
            return;
        }

        if (TryChooseCuteActivity(island: true))
        {
            return;
        }

        var roll = _random.Next(100);
        if (_world.IsNight)
        {
            if (roll < 38)
            {
                BeginIslandActivity(
                    PetState.Sleeping, _random.Next(70, 100), 218);
            }
            else if (roll < 66)
            {
                BeginIslandActivity(
                    PetState.Reading, _random.Next(56, 82), 225);
            }
            else if (roll < 82)
            {
                BeginIslandActivity(
                    PetState.Eating, _random.Next(40, 58), 178);
            }
            else if (roll < 94)
            {
                BeginIslandActivity(
                    PetState.Sitting, _random.Next(38, 58), 260);
            }
            else
            {
                BeginIslandActivity(
                    PetState.Sitting, _random.Next(38, 58), 260);
            }
            return;
        }

        if (_world.Weather != WeatherMode.Clear)
        {
            if (roll < 34)
            {
                BeginIslandActivity(
                    PetState.Reading, _random.Next(54, 78), 225);
            }
            else if (roll < 62)
            {
                BeginIslandActivity(
                    PetState.Sleeping, _random.Next(58, 82), 218);
            }
            else if (roll < 82)
            {
                BeginIslandActivity(
                    PetState.Eating, _random.Next(40, 58), 178);
            }
            else
            {
                BeginIslandActivity(
                    PetState.Sitting, _random.Next(34, 54), 260);
            }
            return;
        }

        if (roll < 30)
        {
            var goRight = _random.Next(2) == 0;
            BeginActivity(
                goRight ? PetState.RunningRight : PetState.RunningLeft,
                _random.Next(24, 48));
        }
        else if (roll < 43)
        {
            BeginIslandActivity(PetState.Sitting, _random.Next(34, 54), 260);
        }
        else if (roll < 56)
        {
            BeginIslandActivity(PetState.Eating, _random.Next(40, 58), 178);
        }
        else if (roll < 69)
        {
            BeginIslandActivity(PetState.Sleeping, _random.Next(58, 86), 218);
        }
        else if (roll < 82)
        {
            BeginIslandActivity(PetState.Gardening, _random.Next(42, 62), 300);
        }
        else if (roll < 95)
        {
            BeginIslandActivity(PetState.Reading, _random.Next(52, 78), 225);
        }
        else
        {
            BeginIslandActivity(
                PetState.Sitting, _random.Next(34, 54), 260);
        }
    }

    private void BeginIslandActivity(
        PetState activity,
        int ticks,
        double targetX)
    {
        if (activity == PetState.Idle)
        {
            throw new ArgumentException(
                "小世界动作不能是待机。", nameof(activity));
        }

        _lifePhase = LifePhase.Activity;
        if (Math.Abs(_world.PetX - targetX) < 2)
        {
            BeginActivity(activity, ticks);
            return;
        }

        _pendingActivity = activity;
        _pendingActivityTicks = ticks;
        _walkTargetX = targetX;
        StartState(
            targetX > _world.PetX
                ? PetState.RunningRight
                : PetState.RunningLeft,
            1000);
    }

    private void ChooseNextDesktopActivity()
    {
        if (TryChooseWeatherActivity(island: false))
        {
            return;
        }

        if (TryChooseCuteActivity(island: false))
        {
            return;
        }

        var roll = _random.Next(100);
        if (_world.IsNight)
        {
            if (roll < 36)
            {
                BeginActivity(PetState.Sleeping, _random.Next(58, 86));
            }
            else if (roll < 58)
            {
                BeginActivity(PetState.Reading, _random.Next(48, 70));
            }
            else if (roll < 74)
            {
                BeginActivity(PetState.Eating, _random.Next(36, 54));
            }
            else if (roll < 88)
            {
                BeginActivity(PetState.Sitting, _random.Next(32, 50));
            }
            else
            {
                BeginActivity(PetState.Reading, _random.Next(44, 66));
            }
            return;
        }

        if (_world.Weather != WeatherMode.Clear)
        {
            if (roll < 28)
            {
                BeginActivity(PetState.Reading, _random.Next(44, 66));
            }
            else if (roll < 52)
            {
                BeginActivity(PetState.Sleeping, _random.Next(50, 74));
            }
            else if (roll < 70)
            {
                BeginActivity(PetState.Eating, _random.Next(36, 54));
            }
            else if (roll < 86)
            {
                BeginActivity(PetState.Sitting, _random.Next(32, 50));
            }
            else
            {
                BeginActivity(PetState.Sitting, _random.Next(32, 50));
            }
            return;
        }

        if (roll < 34)
        {
            var goRight = _random.Next(2) == 0;
            BeginActivity(
                goRight ? PetState.RunningRight : PetState.RunningLeft,
                _random.Next(24, 48));
        }
        else if (roll < 48)
        {
            BeginActivity(PetState.Sitting, _random.Next(32, 50));
        }
        else if (roll < 62)
        {
            BeginActivity(PetState.Eating, _random.Next(36, 54));
        }
        else if (roll < 74)
        {
            BeginActivity(PetState.Sleeping, _random.Next(48, 70));
        }
        else if (roll < 87)
        {
            BeginActivity(PetState.Gardening, _random.Next(38, 58));
        }
        else if (roll < 96)
        {
            BeginActivity(PetState.Reading, _random.Next(44, 66));
        }
        else
        {
            BeginActivity(PetState.Sitting, _random.Next(32, 50));
        }
    }

    private bool TryChooseWeatherActivity(bool island)
    {
        var weather = _world.Weather;
        if (weather == WeatherMode.Snow)
        {
            if (_random.Next(100) < 28)
            {
                BeginPositionedActivity(
                    PetState.Snowman, _random.Next(55, 86), island, 230);
                return true;
            }

            return false;
        }

        if (weather == WeatherMode.Rain)
        {
            if (_random.Next(100) < 35)
            {
                BeginPositionedActivity(
                    PetState.RainUmbrella, _random.Next(50, 91), island, 195);
                return true;
            }

            return false;
        }

        var temperature = _world.TemperatureC;
        if (temperature is <= 5 && _random.Next(100) < 35)
        {
            BeginPositionedActivity(
                PetState.Shiver, _random.Next(35, 66), island, 195);
            return true;
        }

        if (temperature is > 27 && _random.Next(100) < 35)
        {
            BeginPositionedActivity(
                PetState.Sweat, _random.Next(35, 66), island, 195);
            return true;
        }

        return false;
    }

    private bool TryChooseCuteActivity(bool island)
    {
        if (!_world.IsNight && _world.Weather == WeatherMode.Clear)
        {
            var daytimeRoll = _random.Next(100);
            if (daytimeRoll < 7)
            {
                BeginPositionedActivity(
                    PetState.BackpackOuting, _random.Next(32, 51), island, 286);
                return true;
            }

            if (daytimeRoll < 12)
            {
                BeginPositionedActivity(
                    PetState.Affectionate, _random.Next(44, 71), island, 205);
                return true;
            }

            if (daytimeRoll < 17)
            {
                BeginPositionedActivity(
                    PetState.JoyJump, _random.Next(24, 37), island, 270);
                return true;
            }

            return false;
        }

        var quietRoll = _random.Next(100);
        if (quietRoll < 7)
        {
            BeginPositionedActivity(
                PetState.Affectionate, _random.Next(44, 71), island, 205);
            return true;
        }

        if (quietRoll < 9)
        {
            BeginPositionedActivity(
                PetState.JoyJump, _random.Next(24, 37), island, 270);
            return true;
        }

        return false;
    }

    private void BeginPositionedActivity(
        PetState activity,
        int ticks,
        bool island,
        double islandTargetX)
    {
        if (island)
        {
            BeginIslandActivity(activity, ticks, islandTargetX);
            return;
        }

        BeginActivity(activity, ticks);
    }

    private async Task RefreshWeatherAsync()
    {
        if (!_automaticWeather || _weatherRefreshRunning)
        {
            return;
        }

        _weatherRefreshRunning = true;
        var succeeded = false;
        try
        {
            var weather = await _weatherService.GetCurrentAsync(
                _weatherLifetime.Token);
            _lastGoodWeather = weather;
            if (_automaticWeather)
            {
                _world.SetLiveWeather(weather);
                UpdateWeatherStatus(weather);
                RefreshIdleVisual();
            }
            succeeded = true;
        }
        catch (OperationCanceledException)
            when (_weatherLifetime.IsCancellationRequested)
        {
            return;
        }
        catch (Exception error)
        {
            if (_lastGoodWeather is null && _weatherStatusMenu is not null)
            {
                _weatherStatusMenu.Header =
                    $"天气：定位失败，稍后重试（{error.GetType().Name}）";
            }
        }
        finally
        {
            _weatherRefreshRunning = false;
            if (_isClosing)
            {
                DisposeWeatherResources();
            }
            else if (!_weatherLifetime.IsCancellationRequested &&
                _automaticWeather)
            {
                ScheduleWeatherRefresh(
                    succeeded
                        ? NormalWeatherRefresh
                        : FailedWeatherRefresh);
            }
        }
    }

    private void ScheduleWeatherRefresh(TimeSpan delay)
    {
        _weatherTimer.Stop();
        _weatherTimer.Interval = delay;
        _weatherTimer.Start();
    }

    private void UpdateWeatherStatus(LiveWeather weather)
    {
        if (_weatherStatusMenu is not null)
        {
            _weatherStatusMenu.Header =
                $"天气：{weather.Location}";
        }
    }

    private void DisableAutomaticWeather(string status, bool refreshIdle = true)
    {
        _automaticWeather = false;
        _weatherTimer.Stop();
        _world.ClearLiveWeather();
        if (_weatherStatusMenu is not null)
        {
            _weatherStatusMenu.Header = status;
        }
        if (refreshIdle)
        {
            RefreshIdleVisual();
        }
    }

    private void SetManualWeather(WeatherMode mode, string label)
    {
        DisableAutomaticWeather($"天气：手动{label}", refreshIdle: false);
        _world.SetWeather(mode);
        RefreshIdleVisual();
    }

    private async Task RestoreAutomaticWeatherAsync()
    {
        _automaticWeather = true;
        if (_lastGoodWeather is { } weather)
        {
            _world.SetLiveWeather(weather);
            UpdateWeatherStatus(weather);
            RefreshIdleVisual();
        }
        else if (_weatherStatusMenu is not null)
        {
            _weatherStatusMenu.Header = "天气：正在自动定位…";
        }
        await RefreshWeatherAsync();
    }

    private void StartUpdateMonitor()
    {
        if (_updateMonitorTask is not null)
        {
            return;
        }
        _updateMonitorTask = MonitorUpdatesAsync();
    }

    private async Task MonitorUpdatesAsync()
    {
        try
        {
            await _updateService.RunMonitorAsync(_updateLifetime.Token);
        }
        finally
        {
            await _updateService.DisposeAsync();
            _updateLifetime.Dispose();
        }
    }

    private void ApplyUpdateStatus(UpdateStatus status)
    {
        if (_updateMenu is null)
        {
            return;
        }
        _updateMenu.Header = status.Text;
        _updateMenu.IsEnabled =
            status.State is UpdateState.Ready or UpdateState.Error;
    }

    private async Task HandleUpdateMenuAsync()
    {
        var status = _updateService.Status;
        if (status.State == UpdateState.Error)
        {
            await _updateService.CheckNowAsync(_updateLifetime.Token);
            return;
        }
        if (status.State != UpdateState.Ready)
        {
            return;
        }

        if (await _updateService.InstallReadyUpdateAsync(
                _updateLifetime.Token))
        {
            (Application.Current?.ApplicationLifetime as
                IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        }
    }

    private void DisposeWeatherResources()
    {
        if (_weatherResourcesDisposed)
        {
            return;
        }
        _weatherResourcesDisposed = true;
        _weatherLifetime.Dispose();
        _weatherService.Dispose();
    }

    private void WalkInsideWorld()
    {
        const double minX = 150;
        const double maxX = 306;
        var step = _state == PetState.RunningRight ? 1.45 : -1.45;
        _world.PetX += step;

        if (_pendingActivity is not null)
        {
            var reached = step > 0
                ? _world.PetX >= _walkTargetX
                : _world.PetX <= _walkTargetX;
            if (reached)
            {
                _world.PetX = _walkTargetX;
                UpdateIslandGroundPlane(minX, maxX);
                var activity = _pendingActivity.Value;
                var ticks = _pendingActivityTicks;
                BeginActivity(activity, ticks);
                return;
            }
        }

        if (_world.PetX <= minX)
        {
            _world.PetX = minX;
            _state = PetState.RunningRight;
            _frame = 0;
        }
        else if (_world.PetX >= maxX)
        {
            _world.PetX = maxX;
            _state = PetState.RunningLeft;
            _frame = 0;
        }

        // The island's walkable clearing recedes gently toward the upper-left.
        // Keep Sophie's feet on that ground plane and make depth scaling subtle.
        UpdateIslandGroundPlane(minX, maxX);
    }

    private void UpdateIslandGroundPlane(double minX, double maxX)
    {
        var depth = Math.Clamp(
            (_world.PetX - minX) / (maxX - minX),
            0,
            1);
        _world.PetScale = 0.94 + depth * 0.06;
        _world.SetWorldGroundY(171 + depth * 10);
    }

    private void WalkOnMiniGround()
    {
        const double minX = 18;
        const double maxX = 33;
        var step = _state == PetState.RunningRight ? 0.75 : -0.75;
        _world.PetX += step;

        if (_world.PetX <= minX)
        {
            _world.PetX = minX;
            _state = PetState.RunningRight;
            _frame = 0;
            _frameDelayTicks = 0;
        }
        else if (_world.PetX >= maxX)
        {
            _world.PetX = maxX;
            _state = PetState.RunningLeft;
            _frame = 0;
            _frameDelayTicks = 0;
        }
    }

    private static int FrameHoldTicks(PetState state) =>
        FrameHoldTickCount;

    private void ToggleWorld()
    {
        var open = !_world.IsWorldOpen;
        _world.SetWorldOpen(open);
        SetFixedSize(open ? 440 : 150, open ? 270 : 160);
        if (_worldToggleMenu is not null)
        {
            _worldToggleMenu.Header = open
                ? "收起 Sophie 的小世界"
                : "打开 Sophie 的小世界";
        }
        BeginIdleBeat();
        PlaceNearBottomRight();
    }

    private void SetFixedSize(double width, double height)
    {
        MinWidth = 0;
        MinHeight = 0;
        MaxWidth = double.PositiveInfinity;
        MaxHeight = double.PositiveInfinity;
        Width = width;
        Height = height;
        _world.Width = width;
        _world.Height = height;
        MinWidth = width;
        MinHeight = height;
        MaxWidth = width;
        MaxHeight = height;
    }

    private void PlaceNearBottomRight()
    {
        var screen = Screens.Primary;
        if (screen is null)
        {
            return;
        }

        var area = screen.WorkingArea;
        var x = area.Right - (int)Math.Ceiling(Width * screen.Scaling) - 34;
        var y = area.Bottom - (int)Math.Ceiling(Height * screen.Scaling) - 18;
        Position = new PixelPoint(Math.Max(area.X, x), Math.Max(area.Y, y));
    }

    private enum LifePhase
    {
        IdleBeat,
        Activity
    }
}
