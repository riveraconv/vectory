using System.Globalization;
using System.Threading;
using Vectory.Records;
using Xunit;

namespace Vectory.Test.RecordsTests;

public class RecordFormatTests
{
    [Fact]
    public void Time_WithRecord_UsesTimeFormat_WithoutRecord_Dashes()
    {
        Assert.Equal("1:12.384", RecordFormat.Time(72.384));
        Assert.Equal("--:--.---", RecordFormat.Time(null));
    }

    [Theory]
    [InlineData("es-ES")]
    [InlineData("en-US")]
    [InlineData("ja-JP")]
    public void Date_EnglishUppercase_WhateverTheCulture(string culture)
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        CultureInfo previousUi = Thread.CurrentThread.CurrentUICulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);

            Assert.Equal("29 OCT 2026", RecordFormat.Date(true, "2026-10-29"));
            Assert.Equal("5 JAN 2027", RecordFormat.Date(true, "2027-01-05"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
            Thread.CurrentThread.CurrentUICulture = previousUi;
        }
    }

    [Fact]
    public void Date_OlderIsoFormat_StillReadable()
    {
        Assert.Equal("29 OCT 2026", RecordFormat.Date(true, "2026-10-29T11:59:42.1234567+01:00"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a date")]
    [InlineData("2026-13-45")]
    public void Date_RecordWithoutReadableDate_Dash(string? stored)
    {
        Assert.Equal("—", RecordFormat.Date(true, stored));
    }

    [Fact]
    public void Date_NoRecord()
    {
        Assert.Equal("NO RECORD", RecordFormat.Date(false, null));
        Assert.Equal("NO RECORD", RecordFormat.Date(false, "2026-10-29"));
    }
}
