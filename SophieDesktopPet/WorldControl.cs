using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SophieDesktopPet;

internal sealed class WorldControl : Avalonia.Controls.Control
{
    private const int CellWidth = 192;
    private const int CellHeight = 208;
    private const double PetBaseWidth = 64;
    private const double PetBaseHeight = 69.333;
    private const double WorldPetWidth = 48;
    private const double WorldPetHeight = 52;
    private const double DesktopCatWidth = 43;
    private const double DesktopCatHeight = 46.6;
    private const double WorldCatWidth = 31;
    private const double WorldCatHeight = 33.6;

    private static readonly Rect SceneBounds = new(10, 8, 420, 252);

    private readonly Bitmap _lifeAtlas;
    private readonly Bitmap _catAtlas;
    private readonly Bitmap _miniGround;
    private readonly Bitmap _weatherIcons;
    private readonly Bitmap _dayWorld;
    private readonly Bitmap _nightWorld;
    private readonly Bitmap _clouds;
    private readonly Bitmap _skyMask;
    private int _row;
    private int _column;
    private int _catRow;
    private int _catColumn;
    private bool _catVisible = true;
    private double _phase;
    private bool? _nightOverride;
    private bool? _liveIsDayOverride;
    private UpdateState? _updateState;
    private double? _updateProgress;

    public WorldControl()
    {
        using var lifeStream = AssetLoader.Open(
            new Uri("avares://SophieDesktopPet/Assets/sophie-life.png"));
        _lifeAtlas = new Bitmap(lifeStream);

        using var catStream = AssetLoader.Open(
            new Uri("avares://SophieDesktopPet/Assets/sophie-cat-life.png"));
        _catAtlas = new Bitmap(catStream);

        using var groundStream = AssetLoader.Open(
            new Uri("avares://SophieDesktopPet/Assets/mini-ground.png"));
        _miniGround = new Bitmap(groundStream);

        using var weatherIconsStream = AssetLoader.Open(
            new Uri("avares://SophieDesktopPet/Assets/weather-icons.png"));
        _weatherIcons = new Bitmap(weatherIconsStream);

        using var dayStream = AssetLoader.Open(
            new Uri("avares://SophieDesktopPet/Assets/sophie-world-day.png"));
        _dayWorld = new Bitmap(dayStream);

        using var nightStream = AssetLoader.Open(
            new Uri("avares://SophieDesktopPet/Assets/sophie-world-night.png"));
        _nightWorld = new Bitmap(nightStream);

        using var cloudsStream = AssetLoader.Open(
            new Uri("avares://SophieDesktopPet/Assets/clouds.png"));
        _clouds = new Bitmap(cloudsStream);

        using var maskStream = AssetLoader.Open(
            new Uri("avares://SophieDesktopPet/Assets/sky-mask.png"));
        _skyMask = new Bitmap(maskStream);

        RenderOptions.SetBitmapInterpolationMode(
            this, BitmapInterpolationMode.None);
    }

    public double PetX { get; set; } = 25;
    public double PetY { get; set; } = 20;
    public double PetScale { get; set; } = 1.55;
    public double CatX { get; set; } = 94;
    public bool IsWorldOpen { get; private set; }
    public WeatherMode Weather { get; private set; }
    public double? TemperatureC { get; private set; }
    public string? LiveWeatherLocation { get; private set; }
    public bool IsNight =>
        _nightOverride ??
        (_liveIsDayOverride is bool isDay
            ? !isDay
            : DateTime.Now.Hour < 6 || DateTime.Now.Hour >= 19);

    public void ShowFrame(int row, int column)
    {
        _row = row;
        _column = column;
        InvalidateVisual();
    }

    public void ShowCatFrame(int row, int column, bool visible)
    {
        _catRow = row;
        _catColumn = column;
        _catVisible = visible;
        InvalidateVisual();
    }

    public void AdvanceEnvironment()
    {
        _phase += 0.025;
        InvalidateVisual();
    }

    public void ToggleDayNight()
    {
        _nightOverride = !IsNight;
        InvalidateVisual();
    }

    public void SetWeather(WeatherMode weather)
    {
        Weather = weather;
        InvalidateVisual();
    }

    public void SetLiveWeather(LiveWeather weather)
    {
        TemperatureC = weather.TemperatureC;
        LiveWeatherLocation = weather.Location;
        _liveIsDayOverride = weather.IsDay;
        _nightOverride = null;
        Weather = weather.Mode;
        InvalidateVisual();
    }

    public void ClearLiveWeather()
    {
        TemperatureC = null;
        LiveWeatherLocation = null;
        _liveIsDayOverride = null;
        InvalidateVisual();
    }

    public void SetUpdateStatus(UpdateState? state, double? progress)
    {
        _updateState = state;
        _updateProgress = progress is double value
            ? Math.Clamp(value, 0, 1)
            : null;
        InvalidateVisual();
    }

    public bool IsOverPet(Point point) => PetBounds.Contains(point);

    public void SetWorldOpen(bool isOpen)
    {
        IsWorldOpen = isOpen;
        if (isOpen)
        {
            PetX = 195;
            CatX = 116;
            PetScale = 1;
            SetWorldGroundY(176);
        }
        else
        {
            PetX = 25;
            CatX = 94;
            PetY = 20;
            PetScale = 1.55;
        }
        InvalidateVisual();
    }

    public void SetWorldGroundY(double groundY)
    {
        PetY = groundY - WorldPetHeight * PetScale;
    }

    private Rect PetBounds => IsWorldOpen
        ? new Rect(
            PetX,
            PetY,
            WorldPetWidth * PetScale,
            WorldPetHeight * PetScale)
        : new Rect(
            PetX,
            PetY,
            PetBaseWidth * PetScale,
            PetBaseHeight * PetScale);

    private Rect CatBounds => IsWorldOpen
        ? new Rect(CatX, 181 - WorldCatHeight, WorldCatWidth, WorldCatHeight)
        : new Rect(CatX, 136 - DesktopCatHeight, DesktopCatWidth, DesktopCatHeight);

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (!IsWorldOpen)
        {
            DrawMiniGround(context);
            DrawMiniWeather(context);
            DrawCat(context);
            DrawPet(context);
            DrawUpdateProgress(context);
            return;
        }

        context.DrawRectangle(
            Brush(72, 7, 14, 24), null, new Rect(14, 13, 420, 252), 24, 24);

        using (context.PushClip(new RoundedRect(SceneBounds, 24)))
        {
            var world = IsNight ? _nightWorld : _dayWorld;
            var source = new Rect(
                0,
                world.PixelSize.Height * 0.05,
                world.PixelSize.Width,
                world.PixelSize.Height * 0.9);
            context.DrawImage(world, source, SceneBounds);

            if (IsNight)
            {
                DrawMoon(context);
            }

            DrawMovingClouds(context);
            DrawWeather(context);
            DrawWorldWeatherIndicators(context);
            DrawCat(context);
            DrawPet(context);
            DrawUpdateProgress(context);
        }

        context.DrawRectangle(
            null, new Pen(Brush(190, 255, 244, 205), 2),
            SceneBounds, 24, 24);
    }

    private void DrawMoon(DrawingContext context)
    {
        var glow = Brush(40, 220, 233, 255);
        context.DrawRectangle(glow, null, new Rect(359, 30, 20, 28));
        context.DrawRectangle(glow, null, new Rect(355, 34, 28, 20));

        var moon = Brush(235, 255, 238, 181);
        context.DrawRectangle(moon, null, new Rect(361, 32, 12, 24));
        context.DrawRectangle(moon, null, new Rect(357, 36, 20, 16));
        context.DrawRectangle(
            Brush(255, 13, 25, 67), null, new Rect(369, 32, 10, 16));
    }

    private void DrawMovingClouds(DrawingContext context)
    {
        var drift = _phase * 14;
        var mask = new ImageBrush
        {
            Source = _skyMask,
            Stretch = Stretch.Fill
        };

        using (context.PushOpacityMask(mask, SceneBounds))
        using (context.PushOpacity(IsNight ? 0.48 : 0.92))
        {
            DrawCloud(
                context,
                new Rect(50, 280, 330, 310),
                Wrap(36 + drift * 0.55, -55, 480),
                69,
                43,
                32);
            DrawCloud(
                context,
                new Rect(455, 255, 530, 350),
                Wrap(228 + drift * 0.75, -80, 510),
                24,
                70,
                42);
            DrawCloud(
                context,
                new Rect(1010, 260, 710, 340),
                Wrap(-95 + drift, -105, 535),
                78,
                94,
                45);
        }
    }

    private void DrawCloud(
        DrawingContext context,
        Rect source,
        double x,
        double y,
        double width,
        double height)
    {
        context.DrawImage(_clouds, source, new Rect(x, y, width, height));
    }

    private void DrawWeather(DrawingContext context)
    {
        if (Weather == WeatherMode.Clear)
        {
            if (IsNight)
            {
                DrawFireflies(context);
            }
            return;
        }

        if (Weather == WeatherMode.Rain)
        {
            context.DrawRectangle(
                Brush(38, 17, 40, 69), null, SceneBounds);
            for (var i = 0; i < 30; i++)
            {
                var x = 15 + i * 17 + ((_phase * 95) % 17);
                var y = 12 + ((i * 29 + _phase * 150) % 226);
                context.DrawRectangle(
                    Brush(175, 136, 205, 232), null,
                    new Rect(x, y, 2, 7));
                context.DrawRectangle(
                    Brush(175, 136, 205, 232), null,
                    new Rect(x - 2, y + 7, 2, 3));
            }
            return;
        }

        for (var i = 0; i < 24; i++)
        {
            var x = 16 + (i * 47) % 410 +
                Math.Sin(_phase * 1.7 + i) * 10;
            var y = 8 + ((i * 31 + _phase * 55) % 245);
            var size = 2 + i % 3;
            context.DrawRectangle(
                Brush(225, 242, 247, 255), null,
                new Rect(x, y, size, size));
        }
    }

    private void DrawFireflies(DrawingContext context)
    {
        for (var i = 0; i < 9; i++)
        {
            var x = 105 + (i * 47) % 275 +
                Math.Sin(_phase * 2 + i) * 6;
            var y = 150 + Math.Cos(_phase * 1.7 + i * 2) * 26;
            context.DrawRectangle(
                Brush(55, 255, 215, 71), null,
                new Rect(x - 3, y - 3, 7, 7));
            context.DrawRectangle(
                Brush(245, 255, 238, 137), null,
                new Rect(x, y, 2, 2));
        }
    }

    private void DrawMiniGround(DrawingContext context)
    {
        var column = Weather switch
        {
            WeatherMode.Rain => 1,
            WeatherMode.Snow => 2,
            _ => 0
        };
        context.DrawImage(
            _miniGround,
            new Rect(column * 256, 0, 256, 128),
            new Rect(13, 98, 124, 62));
    }

    private void DrawMiniWeather(DrawingContext context)
    {
        var column = Weather switch
        {
            WeatherMode.Rain => 2,
            WeatherMode.Snow => 3,
            _ when IsNight => 1,
            _ => 0
        };
        context.DrawImage(
            _weatherIcons,
            new Rect(column * 64, 0, 64, 64),
            new Rect(6, 6, 27, 27));
        DrawThermometer(context, new Rect(117, 6, 27, 27));
    }

    private void DrawWorldWeatherIndicators(DrawingContext context)
    {
        var column = Weather switch
        {
            WeatherMode.Rain => 2,
            WeatherMode.Snow => 3,
            _ when IsNight => 1,
            _ => 0
        };
        context.DrawImage(
            _weatherIcons,
            new Rect(column * 64, 0, 64, 64),
            new Rect(24, 20, 22, 22));
        DrawThermometer(context, new Rect(394, 20, 22, 22));
    }

    private void DrawThermometer(DrawingContext context, Rect bounds)
    {
        if (TemperatureC is not double temperature ||
            !double.IsFinite(temperature))
        {
            return;
        }

        var scale = bounds.Width / 27;
        var x = bounds.X;
        var y = bounds.Y;
        var fill = temperature switch
        {
            <= 5 => Brush(255, 83, 184, 255),
            <= 14 => Brush(255, 91, 223, 213),
            <= 27 => Brush(255, 255, 202, 78),
            _ => Brush(255, 255, 100, 92)
        };
        var level = temperature switch
        {
            <= 5 => 5,
            <= 14 => 8,
            <= 27 => 12,
            _ => 16
        };

        void Pixel(IBrush brush, double px, double py, double w, double h) =>
            context.DrawRectangle(
                brush,
                null,
                new Rect(
                    x + px * scale,
                    y + py * scale,
                    w * scale,
                    h * scale));

        var outline = Brush(255, 28, 31, 42);
        var glass = Brush(235, 233, 244, 247);

        Pixel(outline, 10, 1, 7, 17);
        Pixel(outline, 8, 16, 11, 9);
        Pixel(outline, 6, 19, 15, 4);
        Pixel(glass, 12, 3, 3, 15);
        Pixel(glass, 10, 18, 7, 5);
        Pixel(fill, 12, 17 - level, 3, level + 4);
        Pixel(fill, 10, 19, 7, 4);

        Pixel(outline, 18, 5, 3, 2);
        Pixel(outline, 18, 10, 3, 2);
        Pixel(outline, 18, 15, 3, 2);
    }

    private void DrawPet(DrawingContext context)
    {
        var rows = Math.Max(1, _lifeAtlas.PixelSize.Height / CellHeight);
        var columns = Math.Max(1, _lifeAtlas.PixelSize.Width / CellWidth);
        var row = Math.Clamp(_row, 0, rows - 1);
        var column = ((_column % columns) + columns) % columns;
        var source = new Rect(
            column * CellWidth,
            row * CellHeight,
            CellWidth,
            CellHeight);

        context.DrawEllipse(
            Brush(45, 24, 29, 21), null,
            new Point(
                PetBounds.Center.X,
                PetBounds.Bottom - (IsWorldOpen ? 1 : 3)),
            (IsWorldOpen ? 9 : 17) * PetScale,
            (IsWorldOpen ? 2.5 : 4) * PetScale);
        context.DrawImage(
            _lifeAtlas,
            source,
            PetBounds);
    }

    private void DrawCat(DrawingContext context)
    {
        if (!_catVisible)
        {
            return;
        }

        var rows = Math.Max(1, _catAtlas.PixelSize.Height / CellHeight);
        var columns = Math.Max(1, _catAtlas.PixelSize.Width / CellWidth);
        var row = Math.Clamp(_catRow, 0, rows - 1);
        var column = ((_catColumn % columns) + columns) % columns;
        var source = new Rect(
            column * CellWidth,
            row * CellHeight,
            CellWidth,
            CellHeight);

        context.DrawEllipse(
            Brush(36, 24, 29, 21), null,
            new Point(CatBounds.Center.X, CatBounds.Bottom - 1),
            IsWorldOpen ? 7 : 10,
            IsWorldOpen ? 2 : 2.5);
        context.DrawImage(_catAtlas, source, CatBounds);
    }

    private void DrawUpdateProgress(DrawingContext context)
    {
        if (_updateState is null)
        {
            return;
        }

        var bounds = IsWorldOpen
            ? new Rect(164, 244, 112, 9)
            : new Rect(34, 146, 82, 9);
        var outline = Brush(255, 28, 24, 35);
        var track = Brush(235, 78, 67, 82);
        var fill = _updateState == UpdateState.Installing
            ? Brush(255, 255, 190, 75)
            : Brush(255, 82, 218, 174);

        context.DrawRectangle(outline, null, bounds);
        context.DrawRectangle(
            track,
            null,
            new Rect(
                bounds.X + 2,
                bounds.Y + 2,
                bounds.Width - 4,
                bounds.Height - 4));

        var y = bounds.Y + 3;
        var units = Math.Max(1, (int)((bounds.Width - 5) / 5));
        var contentWidth = units * 5 - 1;
        var firstX = bounds.X +
            Math.Floor((bounds.Width - contentWidth) / 2);
        if (_updateState == UpdateState.Downloading &&
            _updateProgress is double progress)
        {
            var filled = progress >= 1
                ? units
                : (int)Math.Floor(progress * units);
            for (var unit = 0; unit < filled; unit++)
            {
                context.DrawRectangle(
                    fill, null, new Rect(firstX + unit * 5, y, 4, 3));
            }
            return;
        }

        var lead = (int)Math.Floor(_phase * 7) % (units + 4) - 3;
        for (var offset = 0; offset < 3; offset++)
        {
            var unit = lead - offset;
            if (unit >= 0 && unit < units)
            {
                context.DrawRectangle(
                    fill, null, new Rect(firstX + unit * 5, y, 4, 3));
            }
        }
    }

    private static double Wrap(double value, double min, double max)
    {
        var range = max - min;
        return min + ((value - min) % range + range) % range;
    }

    private static IBrush Brush(byte a, byte r, byte g, byte b) =>
        new SolidColorBrush(Color.FromArgb(a, r, g, b));
}

internal enum WeatherMode
{
    Clear,
    Rain,
    Snow
}
