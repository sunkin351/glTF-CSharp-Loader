using System.IO;
using GeneratorLib;

namespace Generator
{
    class Program
    {
        static void Main(string[] args)
        {
            var generator = new CodeGenerator();
            generator.ParseSchemas(@"..\glTF\specification\2.0\schema\glTF.schema.json");
            generator.CSharpCodeGen(Path.GetFullPath(@"..\glTFLoader\Schema"));
        }
    }
}
