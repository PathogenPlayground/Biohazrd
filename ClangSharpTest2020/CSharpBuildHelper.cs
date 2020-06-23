﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

namespace ClangSharpTest2020
{
    internal sealed class CSharpBuildHelper
    {
        private readonly List<SyntaxTree> SyntaxTrees = new List<SyntaxTree>();
        private readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.None, SourceCodeKind.Regular);
        private readonly CSharpCompilationOptions CompilationOptions = new CSharpCompilationOptions
        (
            OutputKind.DynamicallyLinkedLibrary,
            allowUnsafe: true,
            platform: Platform.X64,
            specificDiagnosticOptions: new Dictionary<string, ReportDiagnostic>()
            {
                // These diagnostics are a bit annoying to avoid when generating code, so we avoid them for now.
                // Besides not anticipating the need to avoid them soon enough, they are a little annoying because you only need a `new` on the first members that conflicts.
                // Putting the `new` keyword on every conflicting member causes CS0109
                { "CS0114", ReportDiagnostic.Hidden }, // '...' hides inherited member '...'. To make the current member override that implementation, add the override keyword. Otherwise add the new keyword.
                { "CS0108", ReportDiagnostic.Hidden }, // '...' hides inherited member '...'. Use the new keyword if hiding was intended.
            }
        );

        public void AddFile(string filePath)
        {
            string sourceCode = File.ReadAllText(filePath);
            SourceText sourceText = SourceText.From(sourceCode);
            SyntaxTree syntaxTree = SyntaxFactory.ParseSyntaxTree(sourceText, ParseOptions, filePath);
            SyntaxTrees.Add(syntaxTree);
        }

        public ImmutableArray<Diagnostic> Compile()
        {
            // We're using the .NET 5 preview since we're also using the pre-release compiler meant to be paired with it since we're using the unreleased C# 9 function pointers feature.
            const string referenceAssemblyRoot = @"C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\5.0.0-preview.5.20278.1\ref\net5.0\";
            static MetadataReference GetSystemReference(string assemblyFileName)
                => MetadataReference.CreateFromFile(Path.Combine(referenceAssemblyRoot, assemblyFileName + ".dll"));

            List<MetadataReference> references = new List<MetadataReference>()
            {
                GetSystemReference("netstandard"),
                GetSystemReference("System"),
                GetSystemReference("System.Runtime"),
                GetSystemReference("System.Runtime.InteropServices"),
                GetSystemReference("System.Runtime.CompilerServices.Unsafe"),
            };

            CSharpCompilation compilation = CSharpCompilation.Create("Test.dll", SyntaxTrees, references, CompilationOptions);
            return compilation.GetDiagnostics();
        }
    }
}
