using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace Ryujinx.CustomTasks.SyntaxWalker
{
    class ArraySizeCollector : CSharpSyntaxWalker
    {
        public HashSet<int> ArraySizes { get; } = new HashSet<int>();

        private void AddArrayString(string name)
        {
            if (!name.StartsWith("Array") || !name.Contains("<"))
            {
                return;
            }

            string rawArrayType = name.Split('<')[0];

            if (int.TryParse(rawArrayType.Substring(5), out int size))
            {
                ArraySizes.Add(size);
            }
        }

        // https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.genericnamesyntax?view=roslyn-dotnet-4.3.0
        public override void VisitGenericName(GenericNameSyntax node) => AddArrayString(node.ToString());
        // https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.identifiernamesyntax?view=roslyn-dotnet-4.3.0
        public override void VisitIdentifierName(IdentifierNameSyntax node) => AddArrayString(node.ToString());
    }
}