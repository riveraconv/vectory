using Vectory.Ship;
using Xunit;

namespace Vectory.Test.ShipTests;

// Cristal dañado de la cabina: picadas, grietas largas y manchas por nivel (spec docs/efectos-5-danos-casco.md, 6)
public class CockpitGlassDamageTests
{
    private static readonly CockpitGlassSettings S = CockpitGlassSettings.Default;

    private static int Count(int level, int kind)
    {
        int max = kind == 0 ? CockpitGlassDamage.MaxChips : kind == 1 ? CockpitGlassDamage.MaxCracks : CockpitGlassDamage.MaxStains;
        int n = 0;
        for (int i = 0; i < max; i++)
        {
            GlassMark m = kind == 0 ? CockpitGlassDamage.Chip(level, i, 1234, S)
                : kind == 1 ? CockpitGlassDamage.Crack(level, i, 1234, S)
                : CockpitGlassDamage.Stain(level, i, 1234, S);
            if (m.Size > 0f)
                n++;
        }
        return n;
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1, 3, 1, 0)]
    [InlineData(2, 6, 2, 1)]
    [InlineData(3, 10, 3, 3)]
    public void Marks_by_level(int level, int chips, int cracks, int stains)
    {
        Assert.Equal(chips, Count(level, 0));
        Assert.Equal(cracks, Count(level, 1));
        Assert.Equal(stains, Count(level, 2));
    }

    [Fact]
    public void Marks_keep_their_place_when_the_level_goes_up_and_cracks_grow()
    {
        Assert.Equal(CockpitGlassDamage.Chip(1, 2, 77, S), CockpitGlassDamage.Chip(3, 2, 77, S));
        GlassMark one = CockpitGlassDamage.Crack(1, 0, 77, S);
        GlassMark three = CockpitGlassDamage.Crack(3, 0, 77, S);
        Assert.Equal((one.X, one.Y, one.Seed), (three.X, three.Y, three.Seed));
        Assert.True(three.Size > one.Size);
        Assert.Equal(CockpitGlassDamage.Stain(2, 0, 77, S).X, CockpitGlassDamage.Stain(3, 0, 77, S).X);
    }

    [Fact]
    public void A_crack_starts_at_the_chip_with_its_index()
    {
        for (int i = 0; i < CockpitGlassDamage.MaxCracks; i++)
        {
            GlassMark chip = CockpitGlassDamage.Chip(3, i, 9, S);
            GlassMark crack = CockpitGlassDamage.Crack(i + 1, i, 9, S);
            Assert.Equal((chip.X, chip.Y), (crack.X, crack.Y));
        }
    }

    [Fact]
    public void Marks_fall_inside_the_glass_area_and_change_with_the_seed()
    {
        for (int seed = 0; seed < 200; seed++)
        {
            for (int i = 0; i < CockpitGlassDamage.MaxChips; i++)
            {
                GlassMark c = CockpitGlassDamage.Chip(3, i, seed, S);
                Assert.InRange(c.X, S.MinX, S.MaxX);
                Assert.InRange(c.Y, S.MinY, S.MaxY);
                Assert.InRange(c.Size, S.ChipMin, S.ChipMax);
            }
            for (int i = 0; i < CockpitGlassDamage.MaxStains; i++)
            {
                GlassMark s = CockpitGlassDamage.Stain(3, i, seed, S);
                Assert.InRange(s.X, S.MinX, S.MaxX);
                Assert.InRange(s.Y, S.MinY, S.MaxY);
            }
        }
        Assert.NotEqual(CockpitGlassDamage.Chip(3, 0, 1, S).X, CockpitGlassDamage.Chip(3, 0, 2, S).X);
    }

    [Fact]
    public void Spread_pushes_marks_away_from_the_centre()
    {
        Assert.Equal(0.5f, CockpitGlassDamage.Spread(0.5f, 1f), 4);
        Assert.Equal(0f, CockpitGlassDamage.Spread(0f, 1f), 4);
        Assert.Equal(1f, CockpitGlassDamage.Spread(1f, 1f), 4);
        Assert.Equal(0.3f, CockpitGlassDamage.Spread(0.3f, 0f), 4);
        Assert.True(CockpitGlassDamage.Spread(0.6f, 1f) > 0.7f);
        Assert.True(CockpitGlassDamage.Spread(0.4f, 1f) < 0.3f);
        // Con 1000 semillas, menos de una de cada cinco picadas cae en el 30 % central del ancho (uniforme, serían tres de cada diez)
        int centre = 0;
        for (int seed = 0; seed < 1000; seed++)
        {
            float x = CockpitGlassDamage.Chip(3, 0, seed, S).X;
            if (x > 0.35f && x < 0.65f)
                centre++;
        }
        Assert.True(centre < 200, $"{centre}");
    }

    [Fact]
    public void Out_of_range_index_is_empty()
    {
        Assert.Equal(0f, CockpitGlassDamage.Chip(3, 10, 1, S).Size);
        Assert.Equal(0f, CockpitGlassDamage.Crack(3, 3, 1, S).Size);
        Assert.Equal(0f, CockpitGlassDamage.Crack(3, -1, 1, S).Size);
    }
}
