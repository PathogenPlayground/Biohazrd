﻿using ClangSharp;
using ClangSharp.Interop;
using ClangSharp.Pathogen;
using System;
using ClangType = ClangSharp.Type;

namespace Biohazrd
{
    public sealed class TranslatedNormalField : TranslatedField
    {
        internal FieldDecl Field { get; }

        private readonly bool IsBitField;

        internal unsafe TranslatedNormalField(TranslatedRecord record, PathogenRecordField* field)
            : base(record, field)
        {
            if (field->Kind != PathogenRecordFieldKind.Normal)
            { throw new ArgumentException("The specified field must be a normal field.", nameof(field)); }

            Field = (FieldDecl)Library.FindCursor(field->FieldDeclaration);
            IsBitField = field->IsBitField != 0;

            Accessibility = Field.Access switch
            {
                CX_CXXAccessSpecifier.CX_CXXPublic => AccessModifier.Public,
                CX_CXXAccessSpecifier.CX_CXXProtected => AccessModifier.Private, //TODO: Implement protected access
                _ => AccessModifier.Private
            };
        }

        public override void Validate()
        {
            base.Validate();

            // Fields in C++ can have the same name as their enclosing type, but this isn't allowed in C# (it results in CS0542)
            // When we encounter such fields, we rename them to avoid the error.
            if (Parent is TranslatedDeclaration parentDeclaration && TranslatedName == parentDeclaration.TranslatedName)
            {
                File.Diagnostic(Severity.Note, Field, $"Renaming '{this}' to avoid conflict with parent with the same name.");
                TranslatedName += "_";
            }
        }

        protected override void TranslateImplementation(CodeWriter writer)
        {
            //TODO: Bitfields
            using var _bitfields = writer.DisableScope(IsBitField, File, Context, "Unimplemented translation: Bitfields.");

            // If the field is a constant array, we need special translation handling
            ClangType reducedType;
            int levelsOfIndirection;
            File.ReduceType(FieldType, Field, TypeTranslationContext.ForField, out reducedType, out levelsOfIndirection);

            bool isPointerToConstantArray = reducedType.Kind == CXTypeKind.CXType_ConstantArray && levelsOfIndirection > 0;
            using var _pointerToConstantArray = writer.DisableScope(isPointerToConstantArray, File, Context, "Unimplemented translation: Pointer to constant array.");

            if (reducedType is ConstantArrayType constantArray && levelsOfIndirection == 0)
            {
                TranslateConstantArrayField(writer, constantArray);
                return;
            }

            // Perform the translation
            base.TranslateImplementation(writer);
        }

        private void TranslateConstantArrayField(CodeWriter writer, ConstantArrayType constantArrayType)
        {
            using var _constantArrays = writer.DisableScope(true, File, Context, "Disabled translation: Constant array translation needs rethinking.");

            // Reduce the element type
            ClangType reducedElementType;
            int levelsOfIndirection;
            File.ReduceType(constantArrayType.ElementType, Field, TypeTranslationContext.ForField, out reducedElementType, out levelsOfIndirection);

            using var _constantArrayOfArrays = writer.DisableScope(reducedElementType.Kind == CXTypeKind.CXType_ConstantArray, File, Context, "Unimplemented translation: Constant array of constant arrays.");

            // Write out the first element field
            writer.Using("System"); // For ReadOnlySpan<T>
            writer.Using("System.Runtime.InteropServices"); // For FieldOffsetAttribute
            writer.Using("System.Runtime.CompilerServices"); // For Unsafe

            writer.Write($"[FieldOffset({Offset})] private ");
            File.WriteReducedType(writer, reducedElementType, levelsOfIndirection, FieldType, Field, TypeTranslationContext.ForField);
            writer.Write(' ');
            string element0Name = $"__{TranslatedName}_Element0";
            writer.WriteIdentifier(element0Name);
            writer.WriteLine(';');

            writer.Write($"{Accessibility.ToCSharpKeyword()} ReadOnlySpan<");
            File.WriteReducedType(writer, reducedElementType, levelsOfIndirection, FieldType, Field, TypeTranslationContext.ForField);
            writer.Write("> ");
            writer.WriteIdentifier(TranslatedName);
            writer.WriteLine();
            using (writer.Indent())
            {
                writer.Write("=> new ReadOnlySpan<");
                File.WriteReducedType(writer, reducedElementType, levelsOfIndirection, FieldType, Field, TypeTranslationContext.ForField);
                // Note that using fixed would not be valid here since the span leaves the scope where we are fixed.
                // This relies on the fact that TranslatedRecord writes structs out as ref structs. If that were to change, a different strategy is needed here.
                writer.Write(">(Unsafe.AsPointer(ref ");
                writer.WriteIdentifier(element0Name);
                writer.WriteLine($"), {constantArrayType.Size});");
            }
        }
    }
}