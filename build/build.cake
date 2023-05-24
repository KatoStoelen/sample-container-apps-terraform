#addin "nuget:?package=Cake.Git&version=3.0.0"
#addin "nuget:?package=Cake.Docker&version=1.2.0"
#addin "nuget:?package=Cake.FileHelpers&version=6.1.3"

#load "utils/ansi-output.cake"

using System.Text.Json;

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var _target = Argument("target", "Zip-Artifacts");
var _configuration = Argument("configuration", "Debug");
var _containerRegistry = Argument("container-registry", string.Empty);
var _pushImages = Argument("push-images", false);
var _dotnetVerbosity = Argument("dotnet-verbosity", DotNetVerbosity.Minimal);
var _buildCounter = Argument("build-counter", 1);

///////////////////////////////////////////////////////////////////////////////
// VARIABLES
///////////////////////////////////////////////////////////////////////////////

var _rootDir = Directory("..");
var _srcDir = _rootDir + Directory("src");
var _artifactsDir = _rootDir + Directory("artifacts");

var _solutionFile = GetFiles($"{_rootDir}/*.sln").SingleOrDefault() ??
                    throw new FileNotFoundException("Could not find solution file in root directory");

var _isCI = !BuildSystem.IsLocalBuild;
var _branchName = BuildSystem.IsRunningOnGitHubActions
    ? GitHubActions.Environment.Workflow.Ref.Replace("refs/", string.Empty).Replace("heads/", string.Empty)
    : GitBranchCurrent(_rootDir).FriendlyName;

var _isMainBuild = _branchName == "main";
var _commitSha = GitLogTip(_rootDir).Sha;

var _version = GetHostVersion();
var _informationalVersion = $"{_version}+{_commitSha}";

var _containerRegistryUsername = EnvironmentVariable("CONTAINER_REGISTRY_USERNAME");
var _containerRegistryPassword = EnvironmentVariable("CONTAINER_REGISTRY_PASSWORD");
var _containerTagsByImageName = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(_ =>
{
    Info($"Branch: {_branchName}");
    Info($"Is CI? {_isCI}");
    Info($"Commit SHA: {_commitSha}");
    Info($"Hosts version: {_version}");
    Info($"Hosts informational version: {_informationalVersion}");
    Info($"Build counter: {_buildCounter}");
});

Teardown(_ =>
{
    if (BuildSystem.IsRunningOnGitHubActions)
    {
        GitHubActions.Commands.SetOutputParameter("version", _version);
        GitHubActions.Commands.SetOutputParameter("images", JsonSerializer.Serialize(_containerTagsByImageName));
    }
});

TaskSetup(context =>
{
    if (BuildSystem.IsRunningOnGitHubActions)
    {
        GitHubActions.Commands.StartGroup(context.Task.Name);
    }
});

TaskTeardown(_ =>
{
    if (BuildSystem.IsRunningOnGitHubActions)
    {
        GitHubActions.Commands.EndGroup();
    }
});

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    Info($"Cleaning {Relative(_solutionFile)}");

    DotNetClean(_solutionFile.FullPath, new DotNetCleanSettings
    {
        Configuration = _configuration,
        Verbosity = _dotnetVerbosity,
        NoLogo = true
    });
});

Task("Restore")
    .Does(() =>
{
    Info($"Restoring {Relative(_solutionFile)}");

    DotNetRestore(_solutionFile.FullPath, new DotNetRestoreSettings
    {
        Verbosity = _dotnetVerbosity,
        Interactive = BuildSystem.IsLocalBuild
    });
});

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .Does(() =>
{
    Info($"Building solution {Relative(_solutionFile)}");

    DotNetBuild(_solutionFile.FullPath, new DotNetBuildSettings
    {
        Configuration = _configuration,
        NoRestore = true,
        Verbosity = _dotnetVerbosity,
        MSBuildSettings = new DotNetMSBuildSettings()
            .SetVersion(_informationalVersion)
            .WithProperty("ContinuousIntegrationBuild", _isCI.ToString().ToLower())
    });
});

Task("Clean-Artifacts")
    .Does(() =>
{
    if (DirectoryExists(_artifactsDir))
    {
        Info($"Cleaning artifacts directory {Relative(_artifactsDir)}");
        CleanDirectory(_artifactsDir);
    }
    else
    {
        Info($"Creating artifacts directory {Relative(_artifactsDir)}");
        CreateDirectory(_artifactsDir);
    }
});

Task("Publish")
    .IsDependentOn("Build")
    .IsDependentOn("Clean-Artifacts")
    .Does(() =>
{
    foreach (var (projectFile, dockerfile) in GetProjectsWithDockerfile())
    {
        var projectDirectory = projectFile.GetDirectory();

        var publishDir = _artifactsDir +
                         Directory(XmlPeek(projectFile, "/Project/PropertyGroup/ContainerImageName")) +
                         Directory("app");

        Info($"Publishing {Relative(projectFile)} to {Relative(publishDir)}");

        DotNetPublish(projectFile.FullPath, new DotNetPublishSettings
        {
            Configuration = _configuration,
            OutputDirectory = publishDir,
            Verbosity = _dotnetVerbosity,
            NoRestore = true,
            NoBuild = true,
            NoLogo = true
        });

        var destinationDockerfile = publishDir + Directory("..") + File("Dockerfile");

        Information($"  Dockerfile -> {MakeAbsolute(destinationDockerfile)}");

        CopyFile(dockerfile, destinationDockerfile);
    }
});

Task("Docker-Login")
    .WithCriteria(_pushImages)
    .Does(() =>
{
    if (string.IsNullOrWhiteSpace(_containerRegistry))
    {
        throw new ArgumentException("--container-registry not specified");
    }

    Info($"Login to container registry {_containerRegistry}");

    DockerLogin(_containerRegistryUsername, _containerRegistryPassword, _containerRegistry);
});

Task("Docker-Build")
    .IsDependentOn("Publish")
    .Does(() =>
{
    var dockerfilesToBuild = GetFiles($"{_artifactsDir}/**/Dockerfile");
    var tags = new[] { _commitSha, _version };

    foreach (var dockerfile in dockerfilesToBuild)
    {
        var artifactDirectory = dockerfile.GetDirectory();
        var imageName = artifactDirectory.GetDirectoryName();
        var dockerContext = artifactDirectory + Directory("app");
        var iidfile = _artifactsDir + File(imageName + ".iid");

        var imageNameAndTags = tags.Select(tag => $"{imageName}:{tag}");

        Info($"Building image {imageName} with tags {string.Join(", ", tags)}");

        DockerBuild(
            new DockerImageBuildSettings
            {
                File = dockerfile.FullPath,
                Iidfile = iidfile,
                Tag = imageNameAndTags
                    .Select(nameAndTag => !string.IsNullOrWhiteSpace(_containerRegistry)
                        ? $"{_containerRegistry}/{nameAndTag}"
                        : nameAndTag)
                    .ToArray()
            },
            dockerContext);

        _containerTagsByImageName[imageName] = imageNameAndTags;
    }
});

Task("Docker-Push")
    .WithCriteria(_pushImages)
    .IsDependentOn("Docker-Build")
    .Does(() =>
{
    if (string.IsNullOrWhiteSpace(_containerRegistry))
    {
        throw new ArgumentException("--container-registry not specified");
    }

    foreach (var (imageName, _) in _containerTagsByImageName)
    {
        Info($"Pushing all tags of image {imageName} to {_containerRegistry}");

        DockerPush(
            new DockerImagePushSettings
            {
                AllTags = true,
            },
            $"{_containerRegistry}/{imageName}");
    }
});

Task("Docker-CleanUp")
    .Does(() =>
{
    var imageIds = GetFiles($"{_artifactsDir}/*.iid")
        .Select(iidfile => FileReadText(iidfile, Encoding.UTF8).Trim())
        .ToArray();

    foreach (var imageId in imageIds)
    {
        Info($"Removing image {imageId} from Docker host node");

        DockerRemove(
            new DockerImageRemoveSettings
            {
                Force = true
            },
            imageId);
    }
});

Task("CI")
    .IsDependentOn("Docker-Push")
    .IsDependentOn("Docker-CleanUp");

RunTarget(_target);

///////////////////////////////////////////////////////////////////////////////
// CUSTOM
///////////////////////////////////////////////////////////////////////////////

private FilePath Relative(FilePath filePath)
{
    var absoluteRootPath = MakeAbsolute(_rootDir);
    var absoluteFilePath = MakeAbsolute(filePath);

    return absoluteRootPath.GetRelativePath(absoluteFilePath);
}

private DirectoryPath Relative(DirectoryPath directoryPath)
{
    var absoluteRootPath = MakeAbsolute(_rootDir);
    var absoluteDirectoryPath = MakeAbsolute(directoryPath);

    return absoluteRootPath.GetRelativePath(absoluteDirectoryPath);
}

private IEnumerable<(FilePath ProjectFile, FilePath Dockerfile)> GetProjectsWithDockerfile()
{
    return
        from projectFile in GetFiles($"{_srcDir}/**/*.csproj")
        let dockerfile = Directory(projectFile.GetDirectory().FullPath) + File("Dockerfile")
        where FileExists(dockerfile)
        select (projectFile, dockerfile.Path);
}

private string GetHostVersion()
{
    var previousVersion = GitDescribe(
        _rootDir,
        renderLongFormat: false,
        GitDescribeStrategy.Tags,
        minimumCommitIdAbbreviatedSize: 0);

    var month = DateTime.Now.ToString("yyyy.M");
    var prerelease = GetPrerelease();

    if (string.IsNullOrEmpty(previousVersion) || !previousVersion.StartsWith(month))
    {
        return $"{month}.1{prerelease}";
    }

    var nextRevision = int.Parse(previousVersion.Split('.').Last()) + 1;

    return $"{month}.{nextRevision}{prerelease}";
}

private string GetPrerelease()
{
    if (_isMainBuild)
    {
        return string.Empty;
    }

    if (BuildSystem.IsRunningOnGitHubActions && GitHubActions.Environment.PullRequest.IsPullRequest)
    {
        var pullRequestNumber = _branchName.Split('/')[1]; // pull/[PR_NUMBER]/merge

        return $"-pr.{pullRequestNumber}";
    }

    return $"-beta.{_buildCounter}";
}
