using Kadr.SharedKernel.Text;

namespace Kadr.Platform.Tests.Text;

public sealed class ArabicTextTests
{
    [Theory]
    [InlineData("أحمد", "احمد")]
    [InlineData("إدارة", "اداره")]
    [InlineData("على", "علي")]
    public void NormalizeForSearch_folds_arabic_letters(string input, string expected)
    {
        Assert.Equal(expected, ArabicText.NormalizeForSearch(input));
    }

    [Fact]
    public void NormalizeForSearch_strips_tatweel_and_diacritics()
    {
        var withMarks = "أَحْـمَد";
        Assert.Equal("احمد", ArabicText.NormalizeForSearch(withMarks));
    }
}
