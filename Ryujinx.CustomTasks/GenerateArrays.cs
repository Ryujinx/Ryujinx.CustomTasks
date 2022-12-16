using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Build.Framework;
using System.Collections.Generic;
using System.IO;
using Ryujinx.CustomTasks.SyntaxWalker;
using Ryujinx.CustomTasks.Helper;
using System.Linq;
using Task = Microsoft.Build.Utilities.Task;

namespace Ryujinx.CustomTasks
{
    public class GenerateArrays : Task
    {
        private const string InterfaceFileName = "IArray.g.cs";
        private const string ArraysFileName = "Arrays.g.cs";

        private readonly HashSet<string> _outputFiles = new HashSet<string>();

        [Required]
        public string ArrayNamespace { get; set; }

        [Required]
        public string OutputPath { get; set; }

        [Required]
        public ITaskItem[] InputFiles { get; set; }

        [Output]
        public string[] OutputFiles { get; set; }

        private static int GetAvailableMaxSize(IReadOnlyList<int> availableSizes, ref int index, int missingSize)
        {
            if (availableSizes.Count == 0 || missingSize == 1)
            {
                return 1;
            }

            if (availableSizes.Count < index + 1)
            {
                return 0;
            }

            int size = 0;

            while (size == 0 || size > missingSize && availableSizes.Count - index > 0)
            {
                index++;
                size = availableSizes[availableSizes.Count - index];
            }

            return size;
        }

        private void AddGeneratedSource(string filePath, string content)
        {
            File.WriteAllText(filePath, content);
            _outputFiles.Add(filePath);
        }

        private HashSet<int> GetArraySizes(string itemPath)
        {
            Log.LogMessage(MessageImportance.Low, $"Searching for StructArray types in: {itemPath}");

            SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(itemPath), path: itemPath);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            ArraySizeCollector collector = new ArraySizeCollector();

            collector.Visit(root);

            foreach (int size in collector.ArraySizes)
            {
                Log.LogMessage(MessageImportance.Low, $"\tFound array size: {size}");
            }

            return collector.ArraySizes;
        }

        private string GenerateInterface()
        {
            CodeGenerator generator = new CodeGenerator();

            generator.EnterScope($"namespace {ArrayNamespace}");

            generator.AppendLine("/// <summary>");
            generator.AppendLine("/// Array interface.");
            generator.AppendLine("/// </summary>");
            generator.AppendLine("/// <typeparam name=\"T\">Element type</typeparam>);");
            generator.EnterScope("public interface IArray<T> where T : unmanaged");

            generator.AppendLine("/// <summary>");
            generator.AppendLine("/// Used to index the array.");
            generator.AppendLine("/// </summary>");
            generator.AppendLine("/// <param name=\"index\">Element index</param>");
            generator.AppendLine("/// <returns>Element at the specified index</returns>");
            generator.AppendLine("ref T this[int index] { get; }");

            generator.AppendLine();

            generator.AppendLine("/// <summary>");
            generator.AppendLine("/// Number of elements on the array.");
            generator.AppendLine("/// </summary>");
            generator.AppendLine("int Length { get; }");

            generator.LeaveScope();
            generator.LeaveScope();

            return generator.ToString();
        }

        private void GenerateArray(CodeGenerator generator, int size, IReadOnlyList<int> availableSizes)
        {
            generator.EnterScope($"public struct Array{size}<T> : IArray<T> where T : unmanaged");

            generator.AppendLine("T _e0;");

            if (size > 1)
            {
                generator.AppendLine("#pragma warning disable CS0169");
            }

            if (availableSizes.Count == 0)
            {
                for (int i = 1; i < size; i++)
                {
                    generator.AppendLine($"T _e{i};");
                }
            }
            else
            {
                int counter = 1;
                int currentSize = 1;
                int maxSizeIndex = 0;
                int maxSize = 0;

                while (currentSize < size)
                {
                    if (maxSize == 0 || currentSize + maxSize > size)
                    {
                        maxSize = GetAvailableMaxSize(availableSizes, ref maxSizeIndex, size - currentSize);
                    }

                    generator.AppendLine(maxSize > 1
                        ? counter == 1
                            ? $"Array{maxSize}<T> _other;"
                            : $"Array{maxSize}<T> _other{counter};"
                        : $"T _e{counter};"
                    );

                    counter++;
                    currentSize += maxSize;
                }
            }

            if (size > 1)
            {
                generator.AppendLine("#pragma warning restore CS0169");
            }

            generator.AppendLine();
            generator.AppendLine($"public int Length => {size};");
            generator.AppendLine("public ref T this[int index] => ref AsSpan()[index];");
            generator.AppendLine("public Span<T> AsSpan() => MemoryMarshal.CreateSpan(ref _e0, Length);");

            generator.LeaveScope();
            generator.AppendLine();
        }

        public override bool Execute()
        {
            string interfaceFilePath = Path.Combine(OutputPath, InterfaceFileName);
            string arraysFilePath = Path.Combine(OutputPath, ArraysFileName);
            List<int> arraySizes = new List<int>();

            File.Delete(interfaceFilePath);
            File.Delete(arraysFilePath);

            foreach (var item in InputFiles)
            {
                string fullPath = item.GetMetadata("FullPath");

                if (fullPath.EndsWith(".g.cs") || fullPath.Contains(Path.Combine("obj","Debug")) || fullPath.Contains(Path.Combine("obj", "Release")))
                {
                    continue;
                }

                foreach (int size in GetArraySizes(fullPath))
                {
                    if (!arraySizes.Contains(size))
                    {
                        arraySizes.Add(size);
                    }
                }
            }

            if (arraySizes.Count == 0)
            {
                Log.LogWarning("No StructArray types found. Skipping code generation.");

                return true;
            }

            arraySizes.Sort();

            AddGeneratedSource(interfaceFilePath, GenerateInterface());

            CodeGenerator generator = new CodeGenerator();
            List<int> sizesAvailable = new List<int>();

            generator.AppendLine("using System;");
            generator.AppendLine("using System.Runtime.InteropServices;");
            generator.AppendLine();

            generator.EnterScope($"namespace {ArrayNamespace}");

            // Always generate Arrays for 1, 2 and 3
            GenerateArray(generator, 1, sizesAvailable);
            sizesAvailable.Add(1);
            GenerateArray(generator, 2, sizesAvailable);
            sizesAvailable.Add(2);
            GenerateArray(generator, 3, sizesAvailable);
            sizesAvailable.Add(3);

            foreach (var size in arraySizes)
            {
                if (!sizesAvailable.Contains(size))
                {
                    GenerateArray(generator, size, sizesAvailable);
                    sizesAvailable.Add(size);
                }
            }

            generator.LeaveScope();

            AddGeneratedSource(arraysFilePath, generator.ToString());

            OutputFiles = _outputFiles.ToArray();

            return true;
        }
    }
}