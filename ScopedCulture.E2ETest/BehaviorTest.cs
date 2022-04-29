using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using ScopedCulture.E2ETest.Internals;

namespace ScopedCulture.E2ETest;

public class BehaviorTest
{
    private static readonly IReadOnlyDictionary<string, string> LongDateFormat = new Dictionary<string, string>
    {
        ["en"] = "dddd, MMMM d, yyyy",
        ["sv"] = "'den 'd MMMM yyyy",
        ["ja"] = "yyyy'”N'M'ŒŽ'd'“ú'",
    };

    private static readonly IEnumerable<string> TargetFrameworks = new[] {
        TargetFramework.NETCOREAPP31,
        TargetFramework.NET50,
        TargetFramework.NET60,
        TargetFramework.NET70,
    };

    public static object[][] TestCases { get; } = (
        from l1 in LongDateFormat.Keys
        from l2 in LongDateFormat.Keys
        from f in TargetFrameworks
        where l1 != l2
        select new object[] { l1, l2, f }).ToArray();

    public static (string, string) GetTexts(string lang, string framework)
    {
        var culture = CultureInfo.GetCultureInfo(lang);

        var langDispName = ((framework == TargetFramework.NET60 || framework == TargetFramework.NET70) ? culture.NativeName : culture.EnglishName) + $" ({lang})";
        var todayText = DateTime.Today.ToString((framework == TargetFramework.NETCOREAPP31 ? LongDateFormat[lang] : culture.DateTimeFormat.LongDatePattern), culture);
        return (langDispName, todayText);
    }

    [TestCaseSource(nameof(TestCases))]
    public async Task Test1(string a1stLang, string a2ndLang, string framework)
    {
        var (a1stLangDispName, a1stLangTodayText) = GetTexts(a1stLang, framework);
        var (a2ndLangDispName, a2ndLangTodayText) = GetTexts(a2ndLang, framework);

        var sampleSite = await SampleSite.RunAsync(framework);

        using var driver = StartWebDriver(a1stLang);

        // Goto Home, and validate the culture name (ex."en") in navigation pane.
        driver.Navigate().GoToUrl(sampleSite.GetUrl());
        driver.GetElement(By.TagName("h1")).Text.Is("Hello, world!");
        driver.GetElement(By.Id("cultureinfo-currentculture-name")).Text.Is(a1stLang);

        // Click the "Culture" link,
        driver.GetElement(By.ClassName("goto-culture")).Click();
        driver.GetElement(By.TagName("h1")).Text.Is("Culture");

        // and validate the culture display name (ex."English (en)"), and...
        driver.GetElement(By.Id("cultureinfo-currentculture-name")).Text.Is(a1stLang);
        driver.GetElement(By.Id("cultureinfo-currentculture")).Text.Is(a1stLangDispName);
        driver.GetElement(By.Id("scopedculture-currentculture")).Text.Is(a1stLangDispName);

        // date string (ex."Thursday, April 21, 2022") in the "Culture" page.
        driver.GetElement(By.Id("today")).Text.Is(a1stLangTodayText);

        // Change the language by manipulating the culture selector dropdown list,
        driver.GetElement(By.Id("culture-selector")).SendKeys(a2ndLang);

        // and validate the culture display name (ex."English (en)"), and...
        driver.GetElement(By.Id("cultureinfo-currentculture-name")).Text.Is(a2ndLang);
        driver.GetElement(By.Id("cultureinfo-currentculture")).Text.Is(a2ndLangDispName);
        driver.GetElement(By.Id("scopedculture-currentculture")).Text.Is(a2ndLangDispName);

        // date string (ex."Thursday, April 21, 2022") in the "Culture" page.
        driver.GetElement(By.Id("today")).Text.Is(a2ndLangTodayText);

        // Click the "Counter" link,
        // and validate the culture display name (ex."English (en)") in the "Culture" page.
        driver.GetElement(By.ClassName("goto-counter")).Click();
        driver.GetElement(By.TagName("h1")).Text.Is("Counter");
        driver.GetElement(By.Id("cultureinfo-currentculture-name")).Text.Is(a2ndLang);

        // Make some interactions. (Click the button and increment counter.)
        for (var i = 0; i < 3; i++)
        {
            await Task.Delay(200);
            driver.GetElement(By.ClassName("btn")).Click();
        }
        driver.GetElement(By.Id("current-count")).Text.Is("3");

        // Still keep changed lang.
        driver.GetElement(By.Id("cultureinfo-currentculture-name")).Text.Is(a2ndLang);

        // In the "Counter" page, revert current culture lang, and validate it.
        driver.GetElement(By.Id("culture-selector")).SendKeys(a1stLang);
        driver.GetElement(By.Id("cultureinfo-currentculture-name")).Text.Is(a1stLang);

        // Click the "Culture" link, and validate it.
        driver.GetElement(By.ClassName("goto-culture")).Click();
        driver.GetElement(By.TagName("h1")).Text.Is("Culture");
        driver.GetElement(By.Id("cultureinfo-currentculture-name")).Text.Is(a1stLang);
        driver.GetElement(By.Id("cultureinfo-currentculture")).Text.Is(a1stLangDispName);
        driver.GetElement(By.Id("scopedculture-currentculture")).Text.Is(a1stLangDispName);
        driver.GetElement(By.Id("today")).Text.Is(a1stLangTodayText);
    }

    private static WebDriver StartWebDriver(string lang)
    {
        var options = new ChromeOptions();
        options.AddUserProfilePreference("intl.accept_languages", lang);
        return new ChromeDriver(options);
    }
}