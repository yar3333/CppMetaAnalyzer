using System;
using System.Collections.Generic;
using ClangSharp;
using ClangSharp.Interop;
using CppMetaAnalyzer;

public class Program
{
    private static List<string> publicMethods = new();
    private static List<FieldRef> fieldRefs = new();

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
        if (cursor.Kind != CXCursorKind.CXCursor_MacroDefinition)
        {
            if (cursor.IsDeclaration && cursor.Kind == CXCursorKind.CXCursor_CXXMethod 
                                     && cursor.CXXAccessSpecifier == CX_CXXAccessSpecifier.CX_CXXPublic)
            {
                var fullName = getFullName(cursor);
                Console.WriteLine("Public method: " + fullName);
                publicMethods.Add(fullName);
            }

            if (cursor.Kind == CXCursorKind.CXCursor_DeclRefExpr)
            {
                var fullName = getFullName(cursor.Referenced);
                var refFrom = getFullName(cursor.SemanticParent);
                Console.WriteLine("Ref to " + fullName + " from " + refFrom);
                fieldRefs.Add(new FieldRef(fullName, refFrom));
                //int a = 5;
            }

            // ACls *p;
            // p->myFunc3();
            if (cursor.Kind == CXCursorKind.CXCursor_MemberRefExpr)
            {
                var fullName = getFullName(cursor.Referenced);
                var refFrom = getFullName(cursor.SemanticParent);
                Console.WriteLine("Ref to " + fullName + " from " + refFrom);
                fieldRefs.Add(new FieldRef(fullName, refFrom));
                //int a = 5;
            }
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
