using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using AssetRipper.Primitives;
using Cpp2IL.Core;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.InstructionSets;
using Cpp2IL.Core.ProcessingLayers;
using Cpp2IL.Core.Utils;
using LibCpp2IL;

namespace Cpp2ILAdapter;

public static class FileHelper
{
    private static readonly List<string> PathsToDeleteOnExit = new();

    static FileHelper()
    {
        InstructionSetRegistry.RegisterInstructionSet<X86InstructionSet>(DefaultInstructionSets.X86_32);
        InstructionSetRegistry.RegisterInstructionSet<X86InstructionSet>(DefaultInstructionSets.X86_64);
        InstructionSetRegistry.RegisterInstructionSet<WasmInstructionSet>(DefaultInstructionSets.WASM);
        InstructionSetRegistry.RegisterInstructionSet<ArmV7InstructionSet>(DefaultInstructionSets.ARM_V7);
        InstructionSetRegistry.RegisterInstructionSet<NewArmV8InstructionSet>(DefaultInstructionSets.ARM_V8);
        ProcessingLayerRegistry.Register<AttributeAnalysisProcessingLayer>();
        ProcessingLayerRegistry.Register<AttributeInjectorProcessingLayer>();
        ProcessingLayerRegistry.Register<CallAnalysisProcessingLayer>();
        ProcessingLayerRegistry.Register<NativeMethodDetectionProcessingLayer>();
        ProcessingLayerRegistry.Register<StableRenamingProcessingLayer>();
        ProcessingLayerRegistry.Register<DeobfuscationMapProcessingLayer>();
        #if NET6_0_OR_GREATER
        NativeLibrary.Load(typeof(FileHelper).Assembly.Location.Replace("dnSpy.Extension.Cpp2IL.x.dll", "capstone.dll"));
        #endif
    }
    
    public static void ResolvePathsFromCommandLine(string gamePath, string? inputExeName, ref Cpp2IlRuntimeArgs args)
    {
        if (string.IsNullOrEmpty(gamePath))
            throw new Exception("No force options provided, and no game path was provided either. Please provide a game path or use the --force- options.");

        if (Directory.Exists(gamePath) && File.Exists(Path.Combine(gamePath, "GameAssembly.so")))
            HandleLinuxGamePath(gamePath, inputExeName, ref args);
        else if (Directory.Exists(gamePath))
            HandleWindowsGamePath(gamePath, inputExeName, ref args);
        else if (File.Exists(gamePath) && Path.GetExtension(gamePath).ToLowerInvariant() == ".apk")
            HandleSingleApk(gamePath, ref args);
        else if (File.Exists(gamePath) && Path.GetExtension(gamePath).ToLowerInvariant() is ".xapk" or ".apkm")
            HandleXapk(gamePath, ref args);
        else
        {
            if (!Cpp2IlPluginManager.TryProcessGamePath(gamePath, ref args))
                throw new Exception($"Could not find a valid unity game at {gamePath}");
        }
    }

    private static void HandleLinuxGamePath(string gamePath, string? inputExeName, ref Cpp2IlRuntimeArgs args)
    {
        //Linux game.
        args.PathToAssembly = Path.Combine(gamePath, "GameAssembly.so");
        var exeName = Path.GetFileName(Directory.GetFiles(gamePath)
            .FirstOrDefault(f =>
            (f.EndsWith(".x86_64") || f.EndsWith(".x86")) &&
            !MiscUtils.BlacklistedExecutableFilenames.Any(f.EndsWith)));

        exeName = inputExeName ?? exeName;

        if (exeName == null)
            throw new Exception("Failed to locate any executable in the provided game directory. Make sure the path is correct, and if you *really* know what you're doing (and know it's not supported), use the force options, documented if you provide --help.");

        var exeNameNoExt = exeName.Replace(".x86_64", "").Replace(".x86", "");

        var unityPlayerPath = Path.Combine(gamePath, exeName);
        args.PathToMetadata = Path.Combine(gamePath, $"{exeNameNoExt}_Data", "il2cpp_data", "Metadata", "global-metadata.dat");

        if (!File.Exists(args.PathToAssembly) || !File.Exists(unityPlayerPath) || !File.Exists(args.PathToMetadata))
            throw new Exception("Invalid game-path or exe-name specified. Failed to find one of the following:\n" +
            $"\t{args.PathToAssembly}\n" +
            $"\t{unityPlayerPath}\n" +
            $"\t{args.PathToMetadata}\n");

        var gameDataPath = Path.Combine(gamePath, $"{exeNameNoExt}_Data");
        var uv = Cpp2IlApi.DetermineUnityVersion(unityPlayerPath, gameDataPath);

        if (uv == default)
        {
            var userInputUv = Console.ReadLine();

            if (!string.IsNullOrEmpty(userInputUv))
                uv = UnityVersion.Parse(userInputUv);

            if (uv == default)
                throw new Exception("Failed to determine unity version. If you're not running on windows, I need a globalgamemanagers file or a data.unity3d file, or you need to use the force options.");
        }

        args.UnityVersion = uv;

        if (args.UnityVersion.Major < 4)
        {
            var readUnityVersionFrom = Path.Combine(gameDataPath, "globalgamemanagers");
            if (File.Exists(readUnityVersionFrom))
                args.UnityVersion = Cpp2IlApi.GetVersionFromGlobalGameManagers(File.ReadAllBytes(readUnityVersionFrom));
            else
            {
                readUnityVersionFrom = Path.Combine(gameDataPath, "data.unity3d");
                using var stream = File.OpenRead(readUnityVersionFrom);

                args.UnityVersion = Cpp2IlApi.GetVersionFromDataUnity3D(stream);
            }
        }

        if (args.UnityVersion.Major <= 4)
            throw new Exception($"Unable to determine a valid unity version (got {args.UnityVersion})");

        args.Valid = true;
    }

    private static void HandleWindowsGamePath(string gamePath, string? inputExeName, ref Cpp2IlRuntimeArgs args)
    {
        //Windows game.
        args.PathToAssembly = Path.Combine(gamePath, "GameAssembly.dll");
        var exeName = Path.GetFileNameWithoutExtension(Directory.GetFiles(gamePath)
            .FirstOrDefault(f => f.EndsWith(".exe") && !MiscUtils.BlacklistedExecutableFilenames.Any(f.EndsWith)));

        exeName = inputExeName ?? exeName;

        if (exeName == null)
            throw new Exception("Failed to locate any executable in the provided game directory. Make sure the path is correct, and if you *really* know what you're doing (and know it's not supported), use the force options, documented if you provide --help.");

        var unityPlayerPath = Path.Combine(gamePath, $"{exeName}.exe");
        args.PathToMetadata = Path.Combine(gamePath, $"{exeName}_Data", "il2cpp_data", "Metadata", "global-metadata.dat");

        if (!File.Exists(args.PathToAssembly) || !File.Exists(unityPlayerPath) || !File.Exists(args.PathToMetadata))
            throw new Exception("Invalid game-path or exe-name specified. Failed to find one of the following:\n" +
                                $"\t{args.PathToAssembly}\n" +
                                $"\t{unityPlayerPath}\n" +
                                $"\t{args.PathToMetadata}\n");

        var gameDataPath = Path.Combine(gamePath, $"{exeName}_Data");
        var uv = Cpp2IlApi.DetermineUnityVersion(unityPlayerPath, gameDataPath);
            
        if (uv == default)
        {
            var userInputUv = Console.ReadLine();

            if (!string.IsNullOrEmpty(userInputUv))
                uv = UnityVersion.Parse(userInputUv);

            if (uv == default)
                throw new Exception("Failed to determine unity version. If you're not running on windows, I need a globalgamemanagers file or a data.unity3d file, or you need to use the force options.");
        }

        args.UnityVersion = uv;

        if (args.UnityVersion.Major < 4)
        {
            var readUnityVersionFrom = Path.Combine(gameDataPath, "globalgamemanagers");
            if (File.Exists(readUnityVersionFrom))
                args.UnityVersion = Cpp2IlApi.GetVersionFromGlobalGameManagers(File.ReadAllBytes(readUnityVersionFrom));
            else
            {
                readUnityVersionFrom = Path.Combine(gameDataPath, "data.unity3d");
                using var stream = File.OpenRead(readUnityVersionFrom);

                args.UnityVersion = Cpp2IlApi.GetVersionFromDataUnity3D(stream);
            }
        }
        
        if (args.UnityVersion.Major <= 4)
            throw new Exception($"Unable to determine a valid unity version (got {args.UnityVersion})");

        args.Valid = true;
    }

    private static void HandleSingleApk(string gamePath, ref Cpp2IlRuntimeArgs args)
    {
        //APK
        //Metadata: assets/bin/Data/Managed/Metadata
        //Binary: lib/(armeabi-v7a)|(arm64-v8a)/libil2cpp.so
        using var stream = File.OpenRead(gamePath);
        using var zipArchive = new ZipArchive(stream);

        var globalMetadata = zipArchive.Entries.FirstOrDefault(e => e.FullName.EndsWith("assets/bin/Data/Managed/Metadata/global-metadata.dat"));
        var binary = zipArchive.Entries.FirstOrDefault(e => e.FullName.EndsWith("lib/x86_64/libil2cpp.so"));
        binary ??= zipArchive.Entries.FirstOrDefault(e => e.FullName.EndsWith("lib/x86/libil2cpp.so"));
        binary ??= zipArchive.Entries.FirstOrDefault(e => e.FullName.EndsWith("lib/arm64-v8a/libil2cpp.so"));
        binary ??= zipArchive.Entries.FirstOrDefault(e => e.FullName.EndsWith("lib/armeabi-v7a/libil2cpp.so"));

        var globalgamemanagers = zipArchive.Entries.FirstOrDefault(e => e.FullName.EndsWith("assets/bin/Data/globalgamemanagers"));
        var dataUnity3d = zipArchive.Entries.FirstOrDefault(e => e.FullName.EndsWith("assets/bin/Data/data.unity3d"));

        if (binary == null)
            throw new Exception("Could not find libil2cpp.so inside the apk.");
        if (globalMetadata == null)
            throw new Exception("Could not find global-metadata.dat inside the apk");
        if (globalgamemanagers == null && dataUnity3d == null)
            throw new Exception("Could not find globalgamemanagers or data.unity3d inside the apk");

        var tempFileBinary = Path.GetTempFileName();
        var tempFileMeta = Path.GetTempFileName();

        PathsToDeleteOnExit.Add(tempFileBinary);
        PathsToDeleteOnExit.Add(tempFileMeta);

        binary.ExtractToFile(tempFileBinary, true);
        globalMetadata.ExtractToFile(tempFileMeta, true);

        args.PathToAssembly = tempFileBinary;
        args.PathToMetadata = tempFileMeta;

        if (globalgamemanagers != null)
        {
            var ggmBytes = new byte[0x40];
            using var ggmStream = globalgamemanagers.Open();

            // ReSharper disable once MustUseReturnValue
            ggmStream.Read(ggmBytes, 0, 0x40);

            args.UnityVersion = Cpp2IlApi.GetVersionFromGlobalGameManagers(ggmBytes);
        }
        else
        {
            using var du3dStream = dataUnity3d!.Open();

            args.UnityVersion = Cpp2IlApi.GetVersionFromDataUnity3D(du3dStream);
        }
        args.Valid = true;
    }

    private static void HandleXapk(string gamePath, ref Cpp2IlRuntimeArgs args)
    {
        //XAPK file
        //Contains two APKs - one starting with `config.` and one with the package name
        //The config one is architecture-specific and so contains the binary
        //The other contains the metadata
        using var xapkStream = File.OpenRead(gamePath);
        using var xapkZip = new ZipArchive(xapkStream);

        ZipArchiveEntry? configApk = null;
        var configApks = xapkZip.Entries.Where(e => e.FullName.Contains("config.") && e.FullName.EndsWith(".apk")).ToList();
        
        var instructionSetPreference = new string[] { "arm64_v8a", "arm64", "armeabi_v7a", "arm" };
        foreach (var instructionSet in instructionSetPreference)
        {
            configApk = configApks.FirstOrDefault(e => e.FullName.Contains(instructionSet));
            if (configApk != null)
                break;
        }
            
        //Try for base.apk, else find any apk that isn't the config apk
        var mainApk = xapkZip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".apk") && e.FullName.Contains("base.apk"))
                      ?? xapkZip.Entries.FirstOrDefault(e => e != configApk && e.FullName.EndsWith(".apk"));

        if (configApk == null)
            throw new Exception("Could not find a config apk inside the XAPK");
        if (mainApk == null)
            throw new Exception("Could not find a main apk inside the XAPK");

        using var configZip = new ZipArchive(configApk.Open());
        using var mainZip = new ZipArchive(mainApk.Open());
        var binary = configZip.Entries.FirstOrDefault(e => e.FullName.EndsWith("libil2cpp.so"));
        var globalMetadata = mainZip.Entries.FirstOrDefault(e => e.FullName.EndsWith("global-metadata.dat"));

        var globalgamemanagers = mainZip.Entries.FirstOrDefault(e => e.FullName.EndsWith("globalgamemanagers"));
        var dataUnity3d = mainZip.Entries.FirstOrDefault(e => e.FullName.EndsWith("data.unity3d"));

        if (binary == null)
            throw new Exception("Could not find libil2cpp.so inside the config APK");
        if (globalMetadata == null)
            throw new Exception("Could not find global-metadata.dat inside the main APK");
        if (globalgamemanagers == null && dataUnity3d == null)
            throw new Exception("Could not find globalgamemanagers or data.unity3d inside the main APK");

        var tempFileBinary = Path.GetTempFileName();
        var tempFileMeta = Path.GetTempFileName();

        PathsToDeleteOnExit.Add(tempFileBinary);
        PathsToDeleteOnExit.Add(tempFileMeta);

        binary.ExtractToFile(tempFileBinary, true);
        globalMetadata.ExtractToFile(tempFileMeta, true);

        args.PathToAssembly = tempFileBinary;
        args.PathToMetadata = tempFileMeta;

        if (globalgamemanagers != null)
        {
            var ggmBytes = new byte[0x40];
            using var ggmStream = globalgamemanagers.Open();

            // ReSharper disable once MustUseReturnValue
            ggmStream.Read(ggmBytes, 0, 0x40);

            args.UnityVersion = Cpp2IlApi.GetVersionFromGlobalGameManagers(ggmBytes);
        }
        else
        {
            using var du3dStream = dataUnity3d!.Open();

            args.UnityVersion = Cpp2IlApi.GetVersionFromDataUnity3D(du3dStream);
        }
        args.Valid = true;
    }
    
    internal static void CleanupExtractedFiles()
    {
        foreach (var p in PathsToDeleteOnExit)
        {
            try
            {
                File.Delete(p);
            }
            catch (Exception)
            {
                //Ignore
            }
        }
    }
}