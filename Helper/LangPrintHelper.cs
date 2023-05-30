using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CG.SDK.Dotnet.Engine.Models;
using CG.SDK.Dotnet.Helper;
using LangPrint;
using LangPrint.CSharp;

namespace CG.Output.UnityCSharp.Helper;

public static class LangPrintHelper
{
    /// <summary>
    /// Convert <see cref="EngineAttribute"/> to <see cref="CSharpAttribute"/>
    /// </summary>
    /// <param name="eAttr">Attribute to convert</param>
    /// <returns>Converted <see cref="CSharpStruct"/></returns>
    internal static CSharpAttribute ToCSharp(this EngineAttribute eAttr)
    {
        string inlineComment = $"RVA: 0x{eAttr.Rva:X}, Offset: 0x{eAttr.Offset:X}, VA: 0x{eAttr.Va:X}";

        return new CSharpAttribute()
        {
            Name = eAttr.Name,
            Arguments = eAttr.Arguments,
            Conditions = eAttr.Conditions,
            InlineComment = inlineComment
        };
    }

    /// <summary>
    /// Convert <see cref="EngineEnum"/> to <see cref="CSharpEnum"/>
    /// </summary>
    /// <param name="eEnum">Enum to convert</param>
    /// <returns>Converted <see cref="CSharpStruct"/></returns>
    internal static CSharpEnum ToCSharp(this EngineEnum eEnum)
    {
        return new CSharpEnum()
        {
            AccessModifier = eEnum.Modifiers.AccessModifiers.ToString().ToLower(CultureInfo.InvariantCulture),
            Attributes = eEnum.Attributes.Select(ToCSharp).ToList(),
            Name = eEnum.Name,
            Type = eEnum.Type,
            Values = eEnum.Values.Select(kv => new PackageNameValue() { Name = kv.Key, Value = kv.Value }).ToList(),
            HexValues = eEnum.HexValues,
            Conditions = eEnum.Conditions,
            Comments = eEnum.Comments
        }.WithComment(new List<string>() { eEnum.FullName });
    }

    /// <summary>
    /// Convert <see cref="EngineField"/> to <see cref="CSharpField"/>
    /// </summary>
    /// <param name="eField">Field to convert</param>
    /// <returns>Converted <see cref="CSharpField"/></returns>
    internal static CSharpField ToCSharp(this EngineField eField)
    {
        var inlineComment = new StringBuilder();
        inlineComment.Append($"0x{eField.Offset:X4}");

        if (!string.IsNullOrEmpty(eField.Comment))
            inlineComment.Append($" {eField.Comment}");

        if (!string.IsNullOrEmpty(eField.FlagsString))
            inlineComment.Append($" {eField.FlagsString}");

        return new CSharpField()
        {
            AccessModifier = eField.Modifiers.AccessModifiers.ToString().ToLower(CultureInfo.InvariantCulture),
            Attributes = eField.Attributes.Select(ToCSharp).ToList(),
            Name = eField.Name,
            Type = eField.Type,
            Value = eField.Value,
            IsStatic = eField.IsStatic,
            IsArray = !string.IsNullOrWhiteSpace(eField.ArrayDim),
            IsReadOnly = eField.IsReadOnly,
            IsConst = eField.IsConst,
            IsVolatile = (eField.Modifiers.DeclarationModifiers & EngineItemDeclarationModifier.Volatile) != 0,
            InlineComment = eField.Comment,
            Conditions = eField.Conditions,
            Comments = eField.Comments
        }.WithInlineComment(inlineComment.ToString());
    }

    /// <summary>
    /// Convert <see cref="EngineProperty"/> to <see cref="CSharpProperty"/>
    /// </summary>
    /// <param name="eProp">Field to convert</param>
    /// <returns>Converted <see cref="CSharpProperty"/></returns>
    internal static CSharpProperty ToCSharp(this EngineProperty eProp)
    {
        var inlineComment = new StringBuilder();
        inlineComment.Append($"0x{eProp.Offset:X4}");

        if (!string.IsNullOrEmpty(eProp.Comment))
            inlineComment.Append($" {eProp.Comment}");

        if (!string.IsNullOrEmpty(eProp.FlagsString))
            inlineComment.Append($" {eProp.FlagsString}");

        return new CSharpProperty()
        {
            AccessModifier = eProp.Modifiers.AccessModifiers.ToString().ToLower(CultureInfo.InvariantCulture),
            Attributes = eProp.Attributes.Select(ToCSharp).ToList(),
            Name = eProp.Name,
            Type = eProp.Type,
            Value = eProp.Value,
            IsStatic = (eProp.Modifiers.DeclarationModifiers & EngineItemDeclarationModifier.Static) != 0,
            IsArray = !string.IsNullOrWhiteSpace(eProp.ArrayDim),
            IsAbstract = (eProp.Modifiers.DeclarationModifiers & EngineItemDeclarationModifier.Abstract) != 0,
            IsOverride = (eProp.Modifiers.DeclarationModifiers & EngineItemDeclarationModifier.Override) != 0,
            IsVirtual = (eProp.Modifiers.DeclarationModifiers & EngineItemDeclarationModifier.Virtual) != 0,
            HaveGetter = eProp.HaveGetter,
            HaveSetter = eProp.HaveSetter,
            InlineComment = eProp.Comment,
            Conditions = eProp.Conditions,
            Comments = eProp.Comments
        }.WithInlineComment(inlineComment.ToString());
    }

    /// <summary>
    /// Convert <see cref="EngineParameter"/> to <see cref="CSharpParameter"/>
    /// </summary>
    /// <param name="param">Parameter to convert</param>
    /// <returns>Converted <see cref="CSharpParameter"/></returns>
    internal static CSharpParameter ToCSharp(this EngineParameter param)
    {
        return new CSharpParameter()
        {
            Attributes = param.Attributes.Select(ToCSharp).ToList(),
            Name = param.Name,
            Type = param.Type,
            IsRef = param.IsReference,
            Conditions = param.Conditions,
            Comments = param.Comments
        };
    }

    /// <summary>
    /// Convert <see cref="EngineFunction"/> to <see cref="CSharpFunction"/>
    /// </summary>
    /// <param name="func">Function to convert</param>
    /// <returns>Converted <see cref="CSharpStruct"/></returns>
    internal static CSharpFunction ToCSharp(this EngineFunction func)
    {
        List<EngineParameter> @params = func.Parameters
            .Where(p => !p.IsReturn)
            .ToList();

        List<string> comments;
        if (!func.Comments.IsEmpty())
        {
            comments = func.Comments;
        }
        else
        {
            comments = new List<string>()
            {
                "Function:",
                $"\t\tRVA    -> 0x{func.Rva:X8}",
                $"\t\tName   -> {func.FullName}",
                $"\t\tFlags  -> ({func.FlagsString})"
            };

            if (@params.Count > 0)
                comments.Add("Parameters:");

            foreach (EngineParameter param in @params)
            {
                string comment = string.IsNullOrWhiteSpace(param.FlagsString)
                    ? $"\t\t{param.Type,-50} {param.Name}"
                    : $"\t\t{param.Type,-50} {param.Name,-58} ({param.FlagsString})";

                comments.Add(comment);
            }
        }

        return new CSharpFunction()
        {
            AccessModifier = func.Modifiers.AccessModifiers.ToString().ToLower(CultureInfo.InvariantCulture),
            Attributes = func.Attributes.Select(ToCSharp).ToList(),
            Name = func.Name,
            Type = func.Parameters.First(p => p.IsReturn).Type,
            GenericParams = func.TemplateParams,
            Params = @params.Select(ep => ep.ToCSharp()).ToList(),
            Body = func.Body,
            IsStatic = func.IsStatic,
            IsAbstract = (func.Modifiers.DeclarationModifiers & EngineItemDeclarationModifier.Abstract) != 0,
            IsOverride = (func.Modifiers.DeclarationModifiers & EngineItemDeclarationModifier.Override) != 0,
            IsVirtual = (func.Modifiers.DeclarationModifiers & EngineItemDeclarationModifier.Virtual) != 0,
            IsExtern = (func.Modifiers.DeclarationModifiers & EngineItemDeclarationModifier.Extern) != 0,
            IsAsync = false,
            IsUnsafe = false,
            Conditions = func.Conditions,
            Comments = func.Comments
        }.WithComment(comments);
    }

    /// <summary>
    /// Convert <see cref="EngineStruct"/> to <see cref="CSharpStruct"/>
    /// </summary>
    /// <param name="struct">Struct to convert</param>
    /// <returns>Converted <see cref="CSharpStruct"/></returns>
    internal static CSharpStruct ToCSharp(this EngineStruct @struct)
    {
        var comments = new List<string>()
        {
            @struct.FullName
        };
        comments.AddRange(@struct.Comments);

        return new CSharpStruct()
        {
            AccessModifier = @struct.Modifiers.AccessModifiers.ToString().ToLower(CultureInfo.InvariantCulture),
            Attributes = @struct.Attributes.Select(ToCSharp).ToList(),
            Name = @struct.Name,
            IsClass = false,
            Super = @struct.Supers.Select(kv => kv.Value).FirstOrDefault() ?? string.Empty,
            Fields = @struct.Fields.Select(ev => ev.ToCSharp()).ToList(),
            Methods = @struct.Methods.Select(em => em.ToCSharp()).ToList(),
            GenericParams = @struct.TemplateParams,
            Interfaces = @struct.ImpInterfaces.Select(kv => kv.Value).ToList(),
            Properties = @struct.Properties.Select(ToCSharp).ToList(),
            IsStatic = (@struct.Modifiers.DeclarationModifiers & EngineItemDeclarationModifier.Static) != 0,
            IsReadOnly = (@struct.Modifiers.DeclarationModifiers & EngineItemDeclarationModifier.Readonly) != 0,
            IsAbstract = (@struct.Modifiers.DeclarationModifiers & EngineItemDeclarationModifier.Abstract) != 0,
            IsSealed = (@struct.Modifiers.DeclarationModifiers & EngineItemDeclarationModifier.Sealed) != 0,
            Conditions = @struct.Conditions,
            Comments = comments,
            InlineComment = $"TypeDefIndex: {@struct.ObjectIndex}"
        };
    }

    /// <summary>
    /// Convert <see cref="EngineClass"/> to <see cref="CSharpStruct"/>
    /// </summary>
    /// <param name="class">Class to convert</param>
    /// <returns>Converted <see cref="CSharpStruct"/></returns>
    internal static CSharpStruct ToCSharp(this EngineClass @class)
    {
        CSharpStruct ret = ((EngineStruct)@class).ToCSharp();
        ret.IsClass = !@class.IsInterface;
        ret.IsInterface = @class.IsInterface;

        return ret;
    }

    internal static T WithComment<T>(this T cSharpItem, List<string> comments) where T : PackageItemBase
    {
        cSharpItem.Comments = comments;
        return cSharpItem;
    }

    internal static T WithInlineComment<T>(this T cSharpItem, string inlineComment) where T : PackageItemBase
    {
        cSharpItem.InlineComment = inlineComment;
        return cSharpItem;
    }

    internal static T WithCondition<T>(this T cSharpItem, List<string> conditions) where T : PackageItemBase
    {
        cSharpItem.Conditions = conditions;
        return cSharpItem;
    }
}
