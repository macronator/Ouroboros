using System.Globalization;
using System.IO;
using System.Text;

namespace Ouroboros.Automation.Walking;

/// <summary>
///     Loads and saves <see cref="WaypointRoute" />s as plain-text files under a routes directory.
///     Format: line 1 is <c>proximity stepDelayMs loop(0|1)</c>; each subsequent line is
///     <c>mapId,x,y</c>. Unparseable lines are skipped.
/// </summary>
public static class RouteStore
{
    public const string Extension = ".route";

    public static string Serialize(WaypointRoute route)
    {
        var options = route.Options;
        var sb = new StringBuilder();

        sb.Append(options.Proximity)
          .Append(' ')
          .Append((int)options.StepDelay.TotalMilliseconds)
          .Append(' ')
          .Append(options.Loop ? 1 : 0)
          .Append('\n');

        foreach (var waypoint in route.Waypoints)
            sb.Append(waypoint.MapId)
              .Append(',')
              .Append(waypoint.X)
              .Append(',')
              .Append(waypoint.Y)
              .Append('\n');

        return sb.ToString();
    }

    public static WaypointRoute Deserialize(string name, string text)
    {
        var route = new WaypointRoute(name);
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
            return route;

        var header = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (header.Length >= 3
            && int.TryParse(header[0], out var proximity)
            && int.TryParse(header[1], out var delayMs)
            && int.TryParse(header[2], out var loop))
            route.Options = new WalkOptions
            {
                Proximity = proximity,
                StepDelay = TimeSpan.FromMilliseconds(delayMs),
                Loop = loop != 0
            };

        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 3
                && short.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mapId)
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
                route.Waypoints.Add(new Waypoint(mapId, x, y));
        }

        return route;
    }

    public static void Save(WaypointRoute route, string directory)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(PathFor(route.Name, directory), Serialize(route));
    }

    public static WaypointRoute? Load(string name, string directory)
    {
        var path = PathFor(name, directory);

        return File.Exists(path) ? Deserialize(name, File.ReadAllText(path)) : null;
    }

    public static IReadOnlyList<string> List(string directory)
        => Directory.Exists(directory)
            ? Directory.GetFiles(directory, $"*{Extension}").Select(Path.GetFileNameWithoutExtension).OfType<string>().ToArray()
            : [];

    private static string PathFor(string name, string directory) => Path.Combine(directory, name + Extension);
}
