﻿using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ClangSharpTest2020
{
    public sealed class TranslatedLibrary : IDisposable
    {
        private readonly List<TranslatedFile> Files = new List<TranslatedFile>();
        private readonly CXIndex Index;
        private readonly string[] ClangCommandLineArgumentsArray;

        internal ReadOnlySpan<string> ClangCommandLineArguments => ClangCommandLineArgumentsArray;
        
        /// <summary>True if any file in this library contains diagnostics with <see cref="TranslationDiagnostic.IsError"/> of true.</summary>
        public bool HasErrors { get; private set; }

        public TranslatedLibrary(IEnumerable<string> clangCommandLineArguments)
        {
            ClangCommandLineArgumentsArray = clangCommandLineArguments.ToArray();
            Index = CXIndex.Create(displayDiagnostics: true);
        }

        public void AddFile(string filePath)
        {
            TranslatedFile newFile;
            //using (new WorkingDirectoryScope(Path.GetFileNameWithoutExtension(filePath)))
            { newFile = new TranslatedFile(this, Index, filePath); }

            Files.Add(newFile);

            if (newFile.HasErrors)
            {
                Debug.Assert(HasErrors, "The library should already have errors if the new file does as well.");
                HasErrors = true;
            }
        }

        internal void HandleDiagnostic(in TranslationDiagnostic diagnostic)
        {
            if (diagnostic.IsError)
            { HasErrors = true; }

            TextWriter output;
            ConsoleColor oldForegroundColor = Console.ForegroundColor;
            ConsoleColor oldBackgroundColor = Console.BackgroundColor;

            try
            {
                switch (diagnostic.Severity)
                {
                    case TranslationDiagnosticSeverity.Ignored:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        output = Console.Out;
                        break;
                    case TranslationDiagnosticSeverity.Note:
                        output = Console.Out;
                        break;
                    case TranslationDiagnosticSeverity.Warning:
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        output = Console.Error;
                        break;
                    case TranslationDiagnosticSeverity.Error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        output = Console.Error;
                        break;
                    case TranslationDiagnosticSeverity.Fatal:
                    default:
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.BackgroundColor = ConsoleColor.DarkRed;
                        output = Console.Error;
                        break;
                }

                if (!diagnostic.Location.IsNull)
                {
                    string fileName = Path.GetFileName(diagnostic.Location.SourceFile);
                    if (diagnostic.Location.Line != 0)
                    { output.WriteLine($"{diagnostic.Severity} at {fileName}:{diagnostic.Location.Line}: {diagnostic.Message}"); }
                    else
                    { output.WriteLine($"{diagnostic.Severity} at {fileName}: {diagnostic.Message}"); }
                }
                else
                { output.WriteLine($"{diagnostic.Severity}: {diagnostic.Message}"); }
            }
            finally
            {
                Console.BackgroundColor = oldBackgroundColor;
                Console.ForegroundColor = oldForegroundColor;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                foreach (TranslatedFile file in Files)
                { file.Dispose(); }
            }

            Index.Dispose();
        }

        ~TranslatedLibrary()
        {
            // Dispose order matters for the unmanaged resources indirectly managed by this class, so it's not ideal to allow it to be garbage collected.
            // As such, we complain if we're allowed to be collected.
            Console.Error.WriteLine("TranslatedLibrary must be disposed of explicitly!");
            Debugger.Break();
            Dispose(false);
        }
    }
}
