using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClangSharp.Interop;
using SmartCommandLineParser;

namespace CppMetaAnalyzer;

public class Program
{
    private static List<string> publicFields = new();
    private static List<FieldRef> fieldRefs = new();

    private static bool verbose = false;

    [STAThread]
    public static int Main(string[] args)
    {
        var options = new CommandLineOptions();

        options.AddRepeatable<string>("sources", new[] { "-S", "--sources" }, "Path to directory contains `*.cpp` files or `*.cpp` file to process.");
        options.AddRepeatable<string>("includes", new[] { "-I", "--include" }, "Path to include directory.");
        options.AddRepeatable<string>("defines", new[] { "-D", "--define" }, "Define like `-D MYA=MYB`.");
        options.AddOptional("vscode", false, new[] { "--vscode" }, "Load config from `.vscode/c_cpp_properties.json`.");
        options.AddOptional("verbose", false, new[] { "--verbose" }, "Show more info.");

        try
        {
            options.Parse(args);
        }
        catch (CommandLineParserException e)
        {
            Console.WriteLine("Error: " + e.Message);
            Console.WriteLine("Usage: CppMetaAnalyzer <options>");
            Console.WriteLine("Details:");
            Console.WriteLine(options.GetHelpMessage());
            return 1;
        }

        if (options.Get<bool>("verbose")) verbose = true;

        Run
        (
            options.Get<List<string>>("sources"),
            options.Get<List<string>>("includes"),
            options.Get<List<string>>("defines"),
            options.Get<bool>("vscode")
        );
        
        return 0;
    }

    private static void Run(List<string> sources, List<string> includes, List<string> defines, bool vscode)
    {
        if (vscode)
        {
            if (File.Exists(@".vscode\c_cpp_properties.json"))
            {
                var text = string.Join('\n', File.ReadAllLines(@".vscode\c_cpp_properties.json").Where(x => !x.StartsWith("//")));
                var doc = JsonSerializer.Deserialize<JsonDocument>(text);
                if (doc == null)
                {
                    Console.WriteLine(@"`.vscode\c_cpp_properties.json` is empty.");
                    return;
                }
                var configurations = doc.RootElement.GetProperty("configurations");
                var platformIO = configurations.EnumerateArray().First(x => x.GetProperty("name").GetString() == "PlatformIO");
                var includePath = platformIO.GetProperty("includePath");
                includes.AddRange(includePath.EnumerateArray().Select(x => x.GetString()!.Replace('/', '\\')));

                var defs = platformIO.GetProperty("defines");
                defines.AddRange(defs.EnumerateArray().Select(x => x.GetString()!));
            }
            else
            {
                Console.WriteLine(@"`.vscode\c_cpp_properties.json` is not found.");
                return;
            }
        }

        var cppFiles = sources.SelectMany(x => Directory.Exists(x) ? Directory.GetFiles(x, "*.cpp", SearchOption.AllDirectories) : new []{x}).ToArray();
        
        using var index = CXIndex.Create(false, verbose);

        foreach (var file in cppFiles)
        {
            // command-line-args: [ "-DmyDefine", "-ImyIncludePath" ]
            var includeOptions = includes.SelectMany(x => new[] { "-I", x }).ToArray();
            var defineOptions = defines.SelectMany(x => new[] { "-D", x }).ToArray();
            var srcFile = CXTranslationUnit.CreateFromSourceFile(index, file, includeOptions.Concat(defineOptions).ToArray().AsSpan(), null);
            
            unsafe
            {
                srcFile.Cursor.VisitChildren(Visitor, new CXClientData());
            }
        }

        publicFields = publicFields.Distinct().OrderBy(x => x).ToList();
        fieldRefs = fieldRefs.Distinct().ToList();

        if (verbose) Console.WriteLine();
        var canBeProtected = publicFields.Where(field => !fieldRefs.Any(rf => rf.Name == field && getClassName(field) != getClassName(rf.RefFrom))).ToArray();
        foreach (var s in canBeProtected) Console.WriteLine("Can be protected: " + s);
    }

    private static unsafe CXChildVisitResult Visitor(CXCursor cursor, CXCursor parent, void* clientData)
    {
        if (cursor.IsDeclaration && (cursor.Kind == CXCursorKind.CXCursor_CXXMethod || cursor.Kind == CXCursorKind.CXCursor_VarDecl)
                                 && cursor.CXXAccessSpecifier == CX_CXXAccessSpecifier.CX_CXXPublic
                                 && cursor.SemanticParent.Kind == CXCursorKind.CXCursor_ClassDecl)
        {
            var fullName = getFullName(cursor, false);
            if (fullName.Contains("::") && !fullName.StartsWith("std::"))
            {
                if (verbose) Console.WriteLine("[1] Public field: " + fullName);
                publicFields.Add(fullName);
            }
        }

        if (cursor.Kind == CXCursorKind.CXCursor_DeclRefExpr && cursor.Referenced != cursor.SemanticParent)
        {
            var fullName = getFullName(cursor.Referenced, false);
            if (fullName.Contains("::") && !fullName.StartsWith("std::"))
            {
                var refFrom = getFullName(cursor.SemanticParent, true);
                if (verbose) Console.WriteLine("[2] Ref to " + fullName + " from " + refFrom);
                fieldRefs.Add(new FieldRef(fullName, refFrom));
            }
        }

        if (cursor.Kind == CXCursorKind.CXCursor_MemberRefExpr)
        {
            var fullName = getFullName(cursor.Referenced, false);
            if (fullName.Contains("::") && !fullName.StartsWith("std::"))
            {
                var refFrom = getFullName(cursor.SemanticParent, true);
                if (verbose) Console.WriteLine("[3] Ref to " + fullName + " from " + refFrom);
                fieldRefs.Add(new FieldRef(fullName, refFrom));
            }
        }

        return CXChildVisitResult.CXChildVisit_Recurse;
    }

    private static string getFullName(CXCursor cursor, bool skipVar)
    {
        var r = !skipVar || cursor.Kind != CXCursorKind.CXCursor_VarDecl 
            ? cursor.Spelling.ToString() 
            : "";
        
        var parent = cursor.SemanticParent;
        while (parent.Kind != CXCursorKind.CXCursor_TranslationUnit && parent.Kind != CXCursorKind.CXCursor_InvalidFile)
        {
            r = parent.Spelling + (r != "" ? "::" : "") + r;
            parent = parent.SemanticParent;
        }

        return r;
    }

    private static string getClassName(string fullName)
    {
        var parts = fullName.Split("::").ToArray();
        if (parts.Length <= 1) return "";
        return string.Join("::", parts.Take(parts.Length - 1));
    }
}