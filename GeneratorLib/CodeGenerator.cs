using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;

namespace GeneratorLib
{
    public class CodeGenerator
    {
        public CodeGenerator()
        {
        }

        public Dictionary<string, JSchema> FileSchemas { get; private set; }

        public void ParseSchemas(string pathToRootSchema)
        {
            pathToRootSchema = Path.GetFullPath(pathToRootSchema);

            var settings = new JSchemaReaderSettings()
            {
                Resolver = new JSchemaUrlResolver(),
                BaseUri = new Uri(pathToRootSchema)
            };

            FileSchemas.Add(Path.GetFileName(pathToRootSchema), GetSchema(pathToRootSchema, settings));

            var directory = Path.GetDirectoryName(pathToRootSchema);
            
            foreach (var schemaFile in Directory.EnumerateFiles(directory, "*.schema.json"))
            {
                var fullPath = Path.Combine(directory, schemaFile);

                if (fullPath == pathToRootSchema)
                    continue;
                
                settings.BaseUri = new Uri(fullPath);

                FileSchemas.Add(schemaFile, GetSchema(fullPath, settings));
            }
        }

        private static JSchema GetSchema(string filePath, JSchemaReaderSettings settings)
        {
            using (var reader = new JsonTextReader(new StreamReader(filePath)))
            {
                return JSchema.Load(reader, settings);
            }
        }

        /// <summary>
        /// In glTF 2.0 an enumeration is defined by a property that contains
        /// the "anyOf" object that contains an array containing multiple
        /// "enum" objects and a single "type" object.
        /// 
        ///   {
        ///     "properties" : {
        ///       "mimeType" : {
        ///         "anyOf" : [
        ///           { "enum" : [ "image/jpeg" ] },
        ///           { "enum" : [ "image/png" ] },
        ///           { "type" : "string" }
        ///         ]
        ///       }
        ///     }
        ///   }
        ///   
        /// Unlike the default Json Schema, each "enum" object array will
        /// contain only one element for glTF.
        /// 
        /// So if the property does not have a "type" object and it has an
        /// "anyOf" object, assume it is an enum and attept to set the
        /// appropriate schema properties.
        /// </summary>
        private void EvaluateEnums()
        {
        }

        public Dictionary<string, CodeTypeDeclaration> GeneratedClasses { get; set; }

        public CompilationUnitSyntax RawClass(string fileName, out string className)
        {
            var root = FileSchemas[fileName];
            var schemaFile = new CodeCompileUnit();
            var schemaNamespace = new CodeNamespace("glTFLoader.Schema");

            var imports = new[]
            {
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Linq")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Runtime.Serialization"))
            };

            className = Helpers.ParseTitle(root.Title);

            var schemaClass = new CodeTypeDeclaration(className)
            {
                Attributes = MemberAttributes.Public
            };

            if (root.AllOf != null && root.AnyOf.Count > 0)
            {
                foreach (var typeRef in root.AllOf)
                {
                    if (typeRef.Reference != null)
                    {
                        throw new NotImplementedException();
                    }
                }
            }

            if (root.Properties != null)
            {
                foreach (var property in root.Properties)
                {
                    AddProperty(schemaClass, property.Key, property.Value);
                }
            }

            GeneratedClasses[fileName] = schemaClass;
            schemaNamespace.Types.Add(schemaClass);
            schemaFile.Namespaces.Add(schemaNamespace);
            return schemaFile;
        }

        private void AddProperty(List<MemberDeclarationSyntax> memberList, string rawName, JSchema schema)
        {
            var propertyName = Helpers.ParsePropertyName(rawName);
            var fieldName = Helpers.GetFieldName(propertyName);
            var codegenType = CodegenTypeFactory.MakeCodegenType(rawName, schema);
            target.Members.AddRange(codegenType.AdditionalMembers);

            var propertyBackingVariable = new CodeMemberField
            {
                Type = codegenType.CodeType,
                Name = fieldName,
                Comments = { new CodeCommentStatement("<summary>", true), new CodeCommentStatement($"Backing field for {propertyName}.", true), new CodeCommentStatement("</summary>", true) },
                InitExpression = codegenType.DefaultValue
            };

            target.Members.Add(propertyBackingVariable);

            var setStatements = codegenType.SetStatements ?? new CodeStatementCollection();
            setStatements.Add(new CodeAssignStatement()
            {
                Left = new CodeFieldReferenceExpression
                {
                    FieldName = fieldName,
                    TargetObject = new CodeThisReferenceExpression()
                },
                Right = new CodePropertySetValueReferenceExpression()
            });

            var property = new CodeMemberProperty
            {
                Type = codegenType.CodeType,
                Name = propertyName,
                Attributes = MemberAttributes.Public | MemberAttributes.Final,
                HasGet = true,
                GetStatements = { new CodeMethodReturnStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), fieldName)) },
                HasSet = true,
                Comments = { new CodeCommentStatement("<summary>", true), new CodeCommentStatement(schema.Description, true), new CodeCommentStatement("</summary>", true) },
                CustomAttributes = codegenType.Attributes
            };
            property.SetStatements.AddRange(setStatements);

            target.Members.Add(property);
        }

        public static CodeTypeReference GetCodegenType(CodeTypeDeclaration target, JSchema schema, string name, out CodeAttributeDeclarationCollection attributes, out CodeExpression defaultValue)
        {
            var codegenType = CodegenTypeFactory.MakeCodegenType(name, schema);
            attributes = codegenType.Attributes;
            defaultValue = codegenType.DefaultValue;
            target.Members.AddRange(codegenType.AdditionalMembers);

            return codegenType.CodeType;
        }

        public void CSharpCodeGen(string outputDirectory)
        {
            // make sure the output directory exists
            Directory.CreateDirectory(outputDirectory);

            GeneratedClasses = new Dictionary<string, CodeTypeDeclaration>();
            foreach (var schema in FileSchemas)
            {
                if (schema.Value.Type == JSchemaType.Object)
                {
                    CodeGenClass(schema.Key, outputDirectory);
                }
            }
        }

        private void CodeGenClass(string fileName, string outputDirectory)
        {
            var schemaFile = RawClass(fileName, out string className);
            CSharpCodeProvider csharpcodeprovider = new CSharpCodeProvider();
            var sourceFile = Path.Combine(outputDirectory, className + "." + csharpcodeprovider.FileExtension);

            IndentedTextWriter tw1 = new IndentedTextWriter(new StreamWriter(sourceFile, false), "    ");
            csharpcodeprovider.GenerateCodeFromCompileUnit(schemaFile, tw1, new CodeGeneratorOptions());
            tw1.Close();
        }
    }
}
