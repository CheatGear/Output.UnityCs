using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CG.Framework.Attributes;
using CG.Framework.Engines;
using CG.Framework.Engines.Models;
using CG.Framework.Engines.Unity;
using CG.Framework.Helper;
using CG.Framework.Helper.IO;
using CG.Framework.Plugin.Output;
using CG.Output.UnityCSharp.Helper;
using LangPrint;
using LangPrint.CSharp;

namespace CG.Output.UnityCSharp;

// TODO: `RuntimeRemoteClassHandle.value` type should be `RuntimeStructs_RemoteClass*`

internal enum CppOptions
{
    PrecompileSyntax
}

[PluginInfo("CorrM", "UnityCSharp", "CSharp syntax support for Unity", "https://github.com/CheatGear", "https://github.com/CheatGear/Output.UnityCs")]
public sealed class UnityCSharp : OutputPlugin<UnitySdkFile>
{
    private CSharpProcessor _cSharpProcessor;

    protected override Dictionary<string, string> LangTypes { get; } = new()
    {
        { "int64_t", "long" },
        { "int32_t", "int" },
        { "int16_t", "short" },
        { "int8_t", "sbyte" },

        { "uint64_t", "ulong" },
        { "uint32_t", "uint" },
        { "uint16_t", "ushort" },
        { "uint8_t", "byte" },

        { "intptr_t", "IntPtr" },
        { "Il2CppString", "string" },
        { "Il2CppObject", "Object" },
    };

    public override Version TargetFrameworkVersion { get; } = new(3, 1, 0);
    public override Version PluginVersion { get; } = new(3, 1, 0);

    public override string OutputName => "CSharp";
    public override EngineType SupportedEngines => EngineType.Unity;
    public override OutputProps SupportedProps => OutputProps.Internal | OutputProps.External;

    public override IReadOnlyDictionary<Enum, OutputOption> Options { get; } = new Dictionary<Enum, OutputOption>()
    {
        {
            CppOptions.PrecompileSyntax,
            new OutputOption(
                "Precompile Syntax",
                OutputOptionType.CheckBox,
                "Use precompile headers for most build speed",
                "true"
            )
        },
    };

    public UnityCSharp()
    {
        _cSharpProcessor = new CSharpProcessor();
    }
    
    private string FixArrayTypeName(string type)
    {
        if (type.EndsWith("_Array"))
            return type[..^"_Array".Length] + "[]";

        return type;
    }

    private void FixTypes(EngineEnum eEnum)
    {
        eEnum.Type = ToLangType(eEnum.Type, false);
    }

    private void FixTypes(EngineField eField)
    {
        eField.Type = FixArrayTypeName(ToLangType(eField.Type, false));
    }

    private void FixTypes(EngineProperty eProp)
    {
        eProp.Type = FixArrayTypeName(ToLangType(eProp.Type, false));
    }

    private void FixTypes(EngineParameter eParam)
    {
        eParam.Type = FixArrayTypeName(ToLangType(eParam.Type, false));
    }

    private void FixTypes(EngineFunction eFunc)
    {
        foreach (EngineParameter engineParameter in eFunc.Parameters)
            FixTypes(engineParameter);
    }

    private void FixTypes(EngineStruct eStruct)
    {
        foreach (EngineField eStructField in eStruct.Fields)
            FixTypes(eStructField);

        foreach (EngineProperty eStructProp in eStruct.Properties)
            FixTypes(eStructProp);

        foreach (EngineFunction eStructFunc in eStruct.Methods)
            FixTypes(eStructFunc);
    }

    private void PrepareStruct(EngineStruct eStruct)
    {
        if (eStruct.Supers.Count > 0)
        {
            (string key, string value) = eStruct.Supers.First();
            if (value is "UObject" or "Il2CppObject")
                eStruct.Supers.Remove(key);
        }

        int getTypeInfoIdx = eStruct.Methods.FindIndex(m => m.Name == "GetTypeInfo");
        if (getTypeInfoIdx != -1)
            eStruct.Methods.RemoveAt(getTypeInfoIdx);

        int getKlassIdx = eStruct.Methods.FindIndex(m => m.Name == "GetKlass");
        if (getKlassIdx != -1)
            eStruct.Methods.RemoveAt(getKlassIdx);
    }

    private IEnumerable<CSharpEnum> GetEnums(UnityPackage enginePackage)
    {
        return enginePackage.Enums.Select(ee => ee.ToCSharp());
    }

    private IEnumerable<CSharpField> GetFields(UnityPackage enginePackage)
    {
        return enginePackage.Fields.Select(ef => ef.ToCSharp());
    }

    private IEnumerable<CSharpFunction> GetFunctions(UnityPackage enginePackage)
    {
        return enginePackage.Functions.Select(ef => ef.ToCSharp());
    }

    private IEnumerable<CSharpStruct> GetStructs(UnityPackage enginePackage)
    {
        return enginePackage.Structs
            .Where(ec => !ec.IsSubType)
            .Select(eStruct =>
            {
                PrepareStruct(eStruct);
                return eStruct.ToCSharp();
            });
    }

    private IEnumerable<CSharpStruct> GetClasses(UnityPackage enginePackage)
    {
        return enginePackage.Classes
            .Where(ec => !ec.IsSubType)
            .Select(eStruct =>
            {
                PrepareStruct(eStruct);
                return eStruct.ToCSharp();
            });
    }

    /// <summary>
    /// Generate enginePackage files
    /// </summary>
    /// <param name="unityPack">Package to generate files for</param>
    /// <returns>File name and its content</returns>
    private async ValueTask<Dictionary<string, string>> GeneratePackageFilesAsync(UnityPackage unityPack)
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);

#if DEBUG
        //if (enginePackage.Name != "BasicTypes")
        //    return new Dictionary<string, string>();
#endif

        if (unityPack.IsPredefined)
            return new Dictionary<string, string>();

        var ret = new Dictionary<string, string>();

        // Make CppPackageModel
        List<CSharpStruct> structs = GetStructs(unityPack).ToList();
        structs.AddRange(GetClasses(unityPack));

        // Make CppPackage
        var cppPackage = new CSharpPackage()
        {
            Name = unityPack.Name,
            //BeforeNameSpace = $"#ifdef _MSC_VER{Environment.NewLine}\t#pragma pack(push, 0x{SdkFile.GlobalMemberAlignment:X2}){Environment.NewLine}#endif",
            //AfterNameSpace = $"#ifdef _MSC_VER{Environment.NewLine}\t#pragma pack(pop){Environment.NewLine}#endif",
            HeadingComment = new List<string>() { $"Name: {SdkFile.GameName}", $"Version: {SdkFile.GameVersion}" },
            NameSpace = SdkFile.Namespace,
            Enums = GetEnums(unityPack).ToList(),
            Structs = structs,
            Conditions = unityPack.Conditions,
        };

        // Generate files
        Dictionary<string, string> cppFiles = _cSharpProcessor.GenerateFiles(cppPackage);
        foreach ((string fName, string fContent) in cppFiles)
            ret.Add(fName, fContent);

        return ret;
    }

    /// <summary>
    /// Process local files that needed to be included
    /// </summary>
    /// <param name="processProps">Process props</param>
    private ValueTask<Dictionary<string, string>> GenerateIncludesAsync(OutputProps processProps)
    {
        var ret = new Dictionary<string, string>();
        return ValueTask.FromResult(ret);

        /*
        // Init
        var unitTestCpp = new UnitTest(this);

        if (processProps == OutputProps.External)
        {
            var mmHeader = new MemManagerHeader(this);
            var mmCpp = new MemManagerCpp(this);

            ValueTask<string> taskMmHeader = mmHeader.ProcessAsync(processProps);
            ValueTask<string> taskMmCpp = mmCpp.ProcessAsync(processProps);

            ret.Add(mmHeader.FileName, await taskMmHeader.ConfigureAwait(false));
            ret.Add(mmCpp.FileName, await taskMmCpp.ConfigureAwait(false));
        }

        // Process
        ValueTask<string> taskUnitTestCpp = unitTestCpp.ProcessAsync(processProps);

        // Wait tasks
        ret.Add(unitTestCpp.FileName, await taskUnitTestCpp.ConfigureAwait(false));

        // PchHeader
        if (Options[CppOptions.PrecompileSyntax].Value == "true")
        {
            var pchHeader = new PchHeader(this);
            ret.Add(pchHeader.FileName, await pchHeader.ProcessAsync(processProps).ConfigureAwait(false));
        }

        return ret;
        */
    }

    protected override ValueTask OnInitAsync()
    {
        ArgumentNullException.ThrowIfNull(SdkFile);

        var cSharpOpts = new CSharpLangOptions()
        {
            NewLine = NewLineType.CRLF,
            PrintSectionName = true,
            InlineCommentPadSize = 40,
            FieldMemberTypePadSize = 50,
            GeneratePackageSyntax = false
        };
        _cSharpProcessor.Init(cSharpOpts);

#if DEBUG
        Options[CppOptions.PrecompileSyntax].SetValue("true");
#endif

        // Fix types in all packages
        foreach (UnityPackage pack in SdkFile.Packages)
        {
            foreach (EngineEnum engineEnum in pack.Enums)
                FixTypes(engineEnum);

            foreach (EngineClass engineClass in pack.Classes)
                FixTypes(engineClass);

            foreach (EngineStruct engineStruct in pack.Structs)
                FixTypes(engineStruct);
        }

        return ValueTask.CompletedTask;
    }

    public override string ToLangType(string cppType, bool onlySubOld)
    {
        (int startPos, int endPos) = GetClearTypeNamePos(cppType);
        string clearedType = cppType[startPos..endPos];

        return base.ToLangType(clearedType, onlySubOld);
    }

    public override async ValueTask StartAsync(string saveDirPath, OutputProps processProps)
    {
        var builder = new MyStringBuilder();

        builder.AppendLine($"#pragma once{Environment.NewLine}");
        builder.AppendLine("// --------------------------------------- \\\\");
        builder.AppendLine("//      Sdk Generated By ( CheatGear )     \\\\");
        builder.AppendLine("// --------------------------------------- \\\\");
        builder.AppendLine($"// Name: {SdkFile.GameName.Trim()}, Version: {SdkFile.GameVersion}{Environment.NewLine}");

        builder.AppendLine("#include <set>");
        builder.AppendLine("#include <string>");
        builder.AppendLine("#include <vector>");
        builder.AppendLine("#include <locale>");
        builder.AppendLine("#include <unordered_set>");
        builder.AppendLine("#include <unordered_map>");
        builder.AppendLine("#include <iostream>");
        builder.AppendLine("#include <sstream>");
        builder.AppendLine("#include <cstdint>");
        builder.AppendLine("#include <Windows.h>");

        // Packages generator [ Should be first task ]
        int packCount = 0;
        foreach (UnityPackage pack in SdkFile.Packages)
        {
            foreach ((string fName, string fContent) in await GeneratePackageFilesAsync(pack).ConfigureAwait(false))
                await FileManager.WriteAsync(saveDirPath, fName, fContent).ConfigureAwait(false);

            if (Status?.ProgressbarStatus is not null)
            {
                await Status.ProgressbarStatus.Invoke(
                    "",
                    packCount,
                    SdkFile.Packages.Count - packCount
                ).ConfigureAwait(false);
            }

            packCount++;
        }

        // Includes
        foreach ((string fName, string fContent) in await GenerateIncludesAsync(processProps).ConfigureAwait(false))
        {
            await FileManager.WriteAsync(saveDirPath, fName, fContent).ConfigureAwait(false);

            if (!fName.EndsWith(".cpp") && fName.ToLower() != "pch.h")
                builder.AppendLine($"#include \"{fName.Replace("\\", "/")}\"");
        }

        builder.Append(Environment.NewLine);

        // Package sorter
        if (Status?.TextStatus is not null)
            await Status.TextStatus.Invoke("Sort packages depend on dependencies").ConfigureAwait(false);

        PackageSorterResult<IEnginePackage> sortResult = PackageSorter.Sort(SdkFile.Packages.Cast<IEnginePackage>().ToList());
        if (sortResult.CycleList.Count > 0)
        {
            builder.AppendLine("// # Dependency cycle headers");
            builder.AppendLine($"// # (Sorted: {sortResult.SortedList.Count}, Cycle: {sortResult.CycleList.Count})\n");

            foreach ((IEnginePackage package, IEnginePackage dependPackage) in sortResult.CycleList)
            {
                builder.AppendLine($"// {package.Name} <-> {dependPackage.Name}");
                builder.AppendLine($"#include \"SDK/{package.Name}_Package.h\"");
            }

            builder.AppendLine();
            builder.AppendLine();
        }

        foreach (IEnginePackage package in sortResult.SortedList.Where(p => p.IsPredefined))
            builder.AppendLine($"#include \"SDK/{package.Name}_Package.h\"");

        foreach (IEnginePackage package in sortResult.SortedList.Where(p => !p.IsPredefined))
            builder.AppendLine($"#include \"SDK/{package.Name}_Package.h\"");

        await FileManager.WriteAsync(saveDirPath, "SDK.h", builder.ToString()).ConfigureAwait(false);
    }
}