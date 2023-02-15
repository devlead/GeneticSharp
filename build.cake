#tool "nuget:?package=MSBuild.SonarQube.Runner.Tool&version=4.8.0"
#addin "nuget:?package=Cake.Sonar&version=1.1.31"
#addin nuget:?package=Cake.Git&version=3.0.0

public record BuildData(
    string SonarLogin,
    DirectoryPath SolutionDir,
    string Branch
)
{
    public bool ShouldRunSonar { get; } = !string.IsNullOrEmpty(SonarLogin);
}

Setup(
    context => new BuildData(
        EnvironmentVariable("GeneticSharp_SonarQube_login"),
        MakeAbsolute(context.Directory("src")),
        AppVeyor.Environment.Repository.Branch ?? GitBranchCurrent(".").FriendlyName
    )
);

var target = Argument("target", "Default");


Task("Build")
    .Does<BuildData>((context, data) =>
{
    var settings = new DotNetBuildSettings
    {
        Configuration = "Release",
    };

    DotNetBuild(data.SolutionDir.FullPath, settings);
});

Task("Test")
    .Does<BuildData>((context, data) =>
{
    var settings = new DotNetTestSettings
    {
        ArgumentCustomization = args => {
            return args.Append("/p:CollectCoverage=true")
                       .Append("/p:CoverletOutputFormat=opencover");
        }
    };

    DotNetTest(data.SolutionDir.FullPath, settings);
});

Task("SonarBegin")
    .WithCriteria<BuildData>(data => data.ShouldRunSonar, nameof(BuildData.ShouldRunSonar))
    .Does<BuildData>((context, data) =>
{
    SonarBegin(new SonarBeginSettings {
        Key = "GeneticSharp",
        Branch = data.Branch,
        Organization = "giacomelli-github",
        Url = "https://sonarcloud.io",
        Exclusions = "GeneticSharp.Benchmarks/*.cs,**/*Test.cs,**/Samples/*.cs,GeneticSharp.Runner.GtkApp/MainWindow.cs,GeneticSharp.Runner.GtkApp/PropertyEditor.cs,**/*.xml,**/Program.cs,**/AssemblyInfo.cs",
        OpenCoverReportsPath = "**/*.opencover.xml",
        Login = data.SonarLogin
     });
});

Task("SonarEnd")
.WithCriteria<BuildData>(data => data.ShouldRunSonar, nameof(BuildData.ShouldRunSonar))
  .Does<BuildData>((context, data) =>
{
     SonarEnd(new SonarEndSettings{
        Login = data.SonarLogin
     });
});

Task("Default")
    .IsDependentOn("SonarBegin")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .IsDependentOn("SonarEnd")
	.Does(()=> {
});

RunTarget(target);