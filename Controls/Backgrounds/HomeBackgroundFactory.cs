namespace Aeroway.Controls.Backgrounds;

public static class HomeBackgroundFactory
{
    public static AnimatedBackgroundBase? Create(string id)
    {
        return HomeBackgroundCatalog.Normalize(id) switch
        {
            "shooting-stars" => new ShootingStarsBackground(),
            "meteors" => new MeteorsBackground(),
            "sparkles" => new SparklesBackground(),
            "aurora" => new AuroraBackground(),
            "vortex" => new VortexBackground(),
            "grid" => new GridBackground(dots: false),
            "dots" => new GridBackground(dots: true),
            "ripple" => new RippleBackground(),
            "wavy" => new WavyBackground(),
            "gradient" => new GradientAnimationBackground(),
            "lines" => new LinesBackground(),
            "spotlight" => new SpotlightBackground(),
            "beams" => new BeamsBackground(),
            "none" => null,
            _ => new ShootingStarsBackground()
        };
    }
}
