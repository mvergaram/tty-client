using System.Diagnostics;

namespace TtyClient.Tests;

public class PublishOutputTests
{
    [Fact]
    public void BuildOutputDoesNotContainTestAudioAssets()
    {
        var repoRoot = FindRepoRoot();
        var mainProject = Path.Combine(repoRoot, "TtyClient.csproj");
        var tempDir = Directory.CreateTempSubdirectory("TtyClientBuildCheck_").FullName;

        try
        {
            var psi = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("build");
            psi.ArgumentList.Add(mainProject);
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("Release");
            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(tempDir);

            using var process = Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.True(process.ExitCode == 0, $"dotnet build failed with exit code {process.ExitCode}.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

            var wavFiles = Directory.GetFiles(tempDir, "*.wav", SearchOption.AllDirectories);
            Assert.Empty(wavFiles);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TtyClient.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing TtyClient.csproj.");
    }
}
