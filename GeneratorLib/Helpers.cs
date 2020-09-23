using System.CodeDom;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GeneratorLib
{
    public static class Helpers
    {
        public static string GetFieldName(string name)
        {
            return "m_" + name.Substring(0, 1).ToLower() + name.Substring(1);
        }

        public static string ParsePropertyName(string rawName)
        {
            return rawName.Substring(0, 1).ToUpper() + rawName.Substring(1);
        }

        public static string ParseTitle(string rawTitle)
        {
            var words = rawTitle.ToLower().Split(' ');

            StringBuilder builder = new StringBuilder();
            foreach (var word in words)
            {
                builder.Append(char.ToUpper(word[0]));
                builder.Append(word, 1, word.Length - 1);
            }
            return builder.ToString();
        }

        public static MethodDeclarationSyntax CreateMethodThatChecksIfTheValueOfAMemberIsNotEqualToAnotherExpression(
           string name, ExpressionSyntax expression)
        {
            string fieldName;
            {
                var arr = new char[name.Length + 2];

                arr[0] = 'm';
                arr[1] = '_';
                arr[2] = char.ToLower(name[0]);
                name.CopyTo(1, arr, 3, name.Length - 1);

                fieldName = new string(arr);
            }

            var newExpression = SyntaxFactory.BinaryExpression(SyntaxKind.NotEqualsExpression, SyntaxFactory.IdentifierName(fieldName), expression);

            return SyntaxFactory.MethodDeclaration(default,
                SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)),
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)),
                default, SyntaxFactory.Identifier("ShouldSerialize" + name), default, default, default, null, SyntaxFactory.ArrowExpressionClause(newExpression),
                SyntaxFactory.Token(SyntaxKind.SemicolonToken)
            );
        }

        public static CodeMemberMethod CreateMethodThatChecksIfTheArrayOfValueOfAMemberIsNotEqualToAnotherExpression(
           string name, CodeExpression expression)
        {
            return new CodeMemberMethod
            {
                ReturnType = new CodeTypeReference(typeof(bool)),
                Statements =
                {
                    new CodeMethodReturnStatement()
                    {
                        Expression = new CodeBinaryOperatorExpression()
                        {
                            Left = new CodeMethodInvokeExpression(
                                new CodeFieldReferenceExpression() {FieldName = "m_" + name.Substring(0, 1).ToLower() + name.Substring(1)},
                                "SequenceEqual",
                                new CodeExpression[] { expression}
                                )
                            ,
                            Operator = CodeBinaryOperatorType.ValueEquality,
                            Right = new CodePrimitiveExpression(false)
                        }
                    }
                },
                Attributes = MemberAttributes.Public | MemberAttributes.Final,
                Name = "ShouldSerialize" + name
            };
        }
    }
}
