using System.Diagnostics.CodeAnalysis;
using Chaos.Common.Definitions;
using Chaos.Geometry.Abstractions.Definitions;

namespace Ouroboros.Model;

/// <summary>A generic creature (monster or non-merchant NPC) — anything that isn't an Aisling or Merchant.</summary>
public sealed class Monster : Creature
{
    public string Name { get; }

    [SetsRequiredMembers]
    public Monster(uint id, Map map, ushort sprite, int x, int y, CreatureType type, Direction direction, string name)
        : base(id, map, sprite, x, y, type, direction)
        => Name = name;
}
