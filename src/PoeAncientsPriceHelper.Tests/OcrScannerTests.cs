using PoeAncientsPriceHelper;

namespace PoeAncientsPriceHelper.Tests;

public class OcrScannerTests
{
    [Theory]
    [InlineData("Support: Scattering Flame", "support scattering flame")]
    [InlineData("Chilling Flux", "chilling flux")]
    [InlineData("Skill: Grip Filters", "skill grip filters")]
    [InlineData("  VERISIUM FLUX  ", "verisium flux")]
    [InlineData("Rune-of-Aldur", "rune of aldur")]
    public void NormalizeName_ProducesExpectedKey(string input, string expected)
    {
        Assert.Equal(expected, OcrScanner.NormalizeName(input));
    }

    [Fact]
    public void NormalizeName_EmptyAfterStrip_ReturnsEmpty()
    {
        Assert.Equal("", OcrScanner.NormalizeName(":::---"));
    }

    [Fact]
    public void NormalizeName_CollapseWhitespace()
    {
        Assert.Equal("a b c", OcrScanner.NormalizeName("a   b   c"));
    }

    [Theory]
    [InlineData("14x adaptive alloy", "adaptive alloy")]
    [InlineData("1 mystic alloy", "mystic alloy")]
    [InlineData("3x rune of aldur", "rune of aldur")]
    [InlineData("adaptive alloy", "adaptive alloy")]
    [InlineData("1 1 adaptive alloy", "adaptive alloy")]
    [InlineData("e l8 n 1x the greatwolf s rune of willpower", "the greatwolf s rune of willpower")]
    [InlineData("oa a 1x greater orb of transmutation", "greater orb of transmutation")]
    [InlineData("b l38 unique quarterstaff", "unique quarterstaff")]
    [InlineData("krogin 1x ancient rune of decay", "ancient rune of decay")]
    [InlineData("hefod 1x ancient rune of the titan", "ancient rune of the titan")]
    [InlineData("nerog 11x ancient rune of discovery", "ancient rune of discovery")]
    [InlineData("ancient rune of shattering", "ancient rune of shattering")]
    public void StripLeadingNoise_RemovesQuantityPrefix(string input, string expected)
    {
        Assert.Equal(expected, OcrScanner.StripLeadingNoise(input));
    }

    [Theory]
    // Per-line OCR leaves divider / price-column fragments at the end of a name; strip them.
    [InlineData("ancient rune of splinters jp", "ancient rune of splinters")]
    [InlineData("armourer s scrap i", "armourer s scrap")]
    [InlineData("warding rune of protection i", "warding rune of protection")]
    [InlineData("rune of the blossom l", "rune of the blossom")]
    [InlineData("greater glacial rune", "greater glacial rune")]      // clean name unchanged
    [InlineData("uncut spirit gem level 19", "uncut spirit gem level 19")] // digit level kept
    [InlineData("void flux", "void flux")]                            // two short-ish real words kept
    // Trailing "(N)" quantity notation: NormalizeName turns "(1)" into " 1"; strip it so short RU
    // names like "руна основ" (10 chars) aren't padded to 12, dropping them below the 0.84 fuzzy score.
    [InlineData("руна основ 1", "руна основ")]
    [InlineData("руна славы 1", "руна славы")]
    [InlineData("руна охвата 1", "руна охвата")]
    [InlineData("большая сфера царей 1", "большая сфера царей")]
    [InlineData("rune of foundations 1", "rune of foundations")]
    [InlineData("uncut skill gem level 20 1", "uncut skill gem level 20")] // level protected, qty stripped
    public void StripTrailingNoise_RemovesShortTailFragments(string input, string expected)
    {
        Assert.Equal(expected, OcrScanner.StripTrailingNoise(input));
    }

    [Theory]
    [InlineData("14x adaptive alloy", 14)]
    [InlineData("3x rune of aldur", 3)]
    [InlineData("1 mystic alloy", 1)]              // no x marker → default 1
    [InlineData("adaptive alloy", 1)]             // no quantity → default 1
    [InlineData("e l8 n 1x the greatwolf", 1)]
    [InlineData("krogin 2x ancient rune of decay", 2)]
    [InlineData("nerog 11x ancient rune of discovery", 11)]
    [InlineData("oa a 1x greater orb of transmutation", 1)]
    [InlineData("warding rune of protection i", 1)] // roman numeral, not a multiplier
    // "(N)" suffix: NormalizeName strips parens → trailing " N" detected as quantity
    [InlineData("рунный сплав 2", 2)]
    [InlineData("divine orb 3", 3)]
    [InlineData("сфера алхимии 2", 2)]
    [InlineData("uncut skill gem level 20 2", 2)]  // level guard active, qty still extracted
    [InlineData("uncut skill gem level 20 1", 1)]
    [InlineData("неограненный камень умения уровень 20 2", 2)]
    public void ExtractMultiplier_ReadsQuantity(string input, int expected)
    {
        Assert.Equal(expected, OcrScanner.ExtractMultiplier(input));
    }

    [Theory]
    [InlineData(360, 110)]
    [InlineData(520, 156)]
    [InlineData(900, 190)]
    public void CalculateLeftCut_ClampsPercentCrop(int width, int expected)
    {
        Assert.Equal(expected, OcrScanner.CalculateLeftCut(width));
    }
}
