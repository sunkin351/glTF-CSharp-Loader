﻿using System;
using System.CodeDom;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;

namespace GeneratorLib
{
    public static class CodegenTypeFactory
    {
        public static CodegenType MakeCodegenType(string name, JSchema schema)
        {
            var codegenType = InternalMakeCodegenType(Helpers.ParsePropertyName(name), schema);

            if (schema.Required != null && schema.Required.Count > 0)
            {
                codegenType.Attributes.Add(new CodeAttributeDeclaration(new CodeTypeReference(typeof(JsonRequiredAttribute))));
            }

            codegenType.Attributes.Add(new CodeAttributeDeclaration(new CodeTypeReference(typeof(JsonPropertyAttribute)), new[] { new CodeAttributeArgument(new CodePrimitiveExpression(name)) }));

            return codegenType;
        }

        private static CodegenType InternalMakeCodegenType(string name, JSchema schema)
        {
            if (schema.Reference != null)
            {
                throw new InvalidOperationException("We don't support de-referencing here.");
            }

            if (schema.AdditionalProperties == null)
            {
                if (schema.Type == JSchemaType.Array)
                {
                    return ArrayValueCodegenTypeFactory.MakeCodegenType(name, schema);
                }

                return SingleValueCodegenTypeFactory.MakeCodegenType(name, schema);
            }

            if (schema.Type == JSchemaType.Object)
            {
                return MakeDictionaryType(name, schema);
            }

            throw new InvalidOperationException();
        }

        private static CodegenType MakeDictionaryType(string name, Schema schema)
        {
            var returnType = new CodegenType();

            if (schema.AdditionalProperties.Type.Count > 1)
            {
                returnType.CodeType = new CodeTypeReference(typeof(Dictionary<string, object>));
                returnType.AdditionalMembers.Add(Helpers.CreateMethodThatChecksIfTheValueOfAMemberIsNotEqualToAnotherExpression(name, new CodePrimitiveExpression(null)));
                return returnType;
            }

            if (schema.HasDefaultValue())
            {
                throw new NotImplementedException("Defaults for dictionaries are not yet supported");
            }

            if (schema.AdditionalProperties.Type[0].Name == "object")
            {
                if (schema.AdditionalProperties.Title != null)
                {
                    returnType.CodeType = new CodeTypeReference($"System.Collections.Generic.Dictionary<string, {Helpers.ParseTitle(schema.AdditionalProperties.Title)}>");
                    returnType.AdditionalMembers.Add(Helpers.CreateMethodThatChecksIfTheValueOfAMemberIsNotEqualToAnotherExpression(name, new CodePrimitiveExpression(null)));
                    return returnType;
                }
                returnType.CodeType = new CodeTypeReference(typeof(Dictionary<string, object>));
                returnType.AdditionalMembers.Add(Helpers.CreateMethodThatChecksIfTheValueOfAMemberIsNotEqualToAnotherExpression(name, new CodePrimitiveExpression(null)));
                return returnType;
            }

            if (schema.AdditionalProperties.Type[0].Name == "string")
            {
                returnType.CodeType = new CodeTypeReference(typeof(Dictionary<string, string>));
                returnType.AdditionalMembers.Add(Helpers.CreateMethodThatChecksIfTheValueOfAMemberIsNotEqualToAnotherExpression(name, new CodePrimitiveExpression(null)));
                return returnType;
            }

            if (schema.AdditionalProperties.Type[0].Name == "integer")
            {
                returnType.CodeType = new CodeTypeReference(typeof(Dictionary<string, int>));
                returnType.AdditionalMembers.Add(Helpers.CreateMethodThatChecksIfTheValueOfAMemberIsNotEqualToAnotherExpression(name, new CodePrimitiveExpression(null)));
                return returnType;
            }

            throw new NotImplementedException($"Dictionary<string,{schema.AdditionalProperties.Type[0].Name}> not yet implemented.");
        }
    }
}
