using System;
using System.Collections.Generic;
using ClangSharp;
using ClangSharp.Interop;

public class Program
{
    private static List<string> publicMethods = new();

    [STAThread]
    public static void Main()
    {
        //System.IO.Directory.SetCurrentDirectory(@"c:\MyDocs\Arduino\projects\eradio32");

        using var index = CXIndex.Create(false, true);

        // command-line-args: [ "-DmyDefine", "-ImyIncludePath" ]
        var srcFile = CXTranslationUnit.CreateFromSourceFile(index, @"main.cpp", new[] { "-Iinclude" }, null);
                
        var clientData = new CXClientData();
        unsafe
        {
            srcFile.Cursor.VisitChildren(Visitor, clientData);
        }
    }

    private static unsafe CXChildVisitResult Visitor(CXCursor cursor, CXCursor parent, void* client_data)
    {
        if (cursor.IsDeclaration && cursor.Kind == CXCursorKind.CXCursor_CXXMethod 
                                 && cursor.CXXAccessSpecifier == CX_CXXAccessSpecifier.CX_CXXPublic)
        {
            var fullName = getFullName(cursor);
            Console.WriteLine("Public method: " + fullName);
            publicMethods.Add(fullName);
        }

        if (cursor.Kind == CXCursorKind.CXCursor_CallExpr)
        {
            int a = 5;
        }

        return CXChildVisitResult.CXChildVisit_Recurse;
    }

    private static string getFullName(CXCursor cursor)
    {
        var r = cursor.Spelling.ToString();
        
        var parent = cursor.SemanticParent;
        while (parent.Kind != CXCursorKind.CXCursor_TranslationUnit)
        {
            r = parent.Spelling + "::" + r;
            parent = parent.SemanticParent;
        }

        return r;
    }
}
