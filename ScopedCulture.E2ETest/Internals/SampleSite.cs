using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Toolbelt;
using Toolbelt.Diagnostics;

namespace ScopedCulture.E2ETest;

[SetUpFixture]
public class SampleSite : IDisposable
{
    private static int _ListenPortCounter = 5050;

    private static ConcurrentDictionary<string, SampleSite> Instances { get; } = new();

    private string TargetFramework { get; } = "";

    private int ListenPort { get; }

    private WorkDirectory? ProjectDir { get; }

    private XProcess? DotNetCLI { get; set; }

    private bool RunOnce { get; set; } = false;

    public SampleSite()
    {
    }

    public SampleSite(string targetFramework, int listenPort)
    {
        this.TargetFramework = targetFramework;
        this.ListenPort = listenPort;

        var solutionDir = FileIO.FindContainerDirToAncestor("*.sln");
        var srcDir = Path.Combine(solutionDir, "DevelopBenchApp");
        this.ProjectDir = WorkDirectory.CreateCopyFrom(srcDir, args => args.Name is (not "obj" and not "bin"));
    }

    public string GetUrl() => $"http://localhost:{this.ListenPort}";

    private async ValueTask RunAsync()
    {
        ArgumentNullException.ThrowIfNull(this.ProjectDir);

        var output = "";
        if (!this.RunOnce && this.DotNetCLI == null)
        {
            this.RunOnce = true;
            this.DotNetCLI = XProcess.Start(
                "dotnet",
                $"run -f:{this.TargetFramework} -- --urls {this.GetUrl()}/",
                workingDirectory: this.ProjectDir, options => options.WhenDisposing = XProcessTerminate.Yes);
            var success = await this.DotNetCLI.WaitForOutputAsync(output => output.Contains(this.GetUrl()), millsecondsTimeout: 15000);

            if (!success)
            {
                try { this.DotNetCLI.Dispose(); } catch { }
                output = this.DotNetCLI.Output;
                this.DotNetCLI = null;
            }
        }

        if (this.DotNetCLI == null) throw new TimeoutException($"\"dotnet run\" did not respond \"Now listening on: {this.GetUrl()}\".\r\n" + output);
    }

    public static async ValueTask<SampleSite> RunAsync(string targetFramework)
    {
        var sampleSite = Instances.GetOrAdd(targetFramework, f =>
        {
            var listenPort = Interlocked.Increment(ref _ListenPortCounter);
            return new SampleSite(f, listenPort);
        });
        await sampleSite.RunAsync();

        return sampleSite;
    }

    public void Dispose()
    {
        this.DotNetCLI?.Dispose();
        this.ProjectDir?.Dispose();
    }

    [OneTimeTearDown]
    public void Cleanup()
    {
        foreach (var instance in Instances.Values)
        {
            instance.Dispose();
        }
    }
}
