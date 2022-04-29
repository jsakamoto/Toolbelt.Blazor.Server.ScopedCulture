using System;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace ScopedCulture.E2ETest.Internals;

public static class WebDriverExtensions
{
    private static volatile string? _Buff;

    public static IWebElement GetElement(this IWebDriver driver, By by, int millisecondsTimeout = 5000)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromMilliseconds(millisecondsTimeout))
        {
            PollingInterval = TimeSpan.FromMilliseconds(100)
        };
        Thread.Sleep(100);
        return wait.Until(d =>
        {
            var element = d.FindElement(by);
            try { _Buff = element.Text; }
            catch (StaleElementReferenceException) { return null; }
            //Thread.Sleep(50);
            return element;
        })!;
    }
}
