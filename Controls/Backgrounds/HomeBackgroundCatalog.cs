namespace Aeroway.Controls.Backgrounds;



/// <summary>

/// Фоны главной страницы (вдохновлено bedolaga-cabinet backgrounds).

/// </summary>

public static class HomeBackgroundCatalog

{

    public sealed record Entry(string Id, string LabelKey);



    public static readonly Entry[] All =

    [

        new("wavy", "bg_wavy"),

        new("shooting-stars", "bg_shooting_stars"),

        new("meteors", "bg_meteors"),

        new("sparkles", "bg_sparkles"),

        new("aurora", "bg_aurora"),

        new("vortex", "bg_vortex"),

        new("grid", "bg_grid"),

        new("dots", "bg_dots"),

        new("ripple", "bg_ripple"),

        new("gradient", "bg_gradient"),

        new("lines", "bg_lines"),

        new("spotlight", "bg_spotlight"),

        new("beams", "bg_beams"),

        new("none", "bg_none")

    ];



    public static string GetLabel(string id)

    {

        var entry = All.FirstOrDefault(e => e.Id == id);

        return Helpers.Loc.T(entry?.LabelKey ?? "bg_wavy");

    }



    public static string Normalize(string? id)

    {

        if (string.IsNullOrWhiteSpace(id)) return All[0].Id;

        return All.Any(e => e.Id == id) ? id : All[0].Id;

    }



    public static (string Id, string Label) Get(string? id)

    {

        var normalized = Normalize(id);

        return (normalized, GetLabel(normalized));

    }



    public static (string Id, string Label) Next(string? id)

    {

        var normalized = Normalize(id);

        var index = Array.FindIndex(All, e => e.Id == normalized);

        var next = All[(index + 1) % All.Length];

        return (next.Id, GetLabel(next.Id));

    }



    /// <summary>Фоны без движения — ползунок скорости на них не действует.</summary>

    public static bool SupportsMotionSpeed(string? id)

    {

        var normalized = Normalize(id);

        return normalized is not ("none" or "grid" or "dots");

    }

}


