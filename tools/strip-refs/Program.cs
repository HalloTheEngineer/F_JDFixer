using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace JDFixer.Tools
{
    /// <summary>
    /// Clears method bodies from every managed assembly in a reference bundle, keeping all metadata.
    /// </summary>
    /// <remarks>
    /// A compiler only needs signatures, so emptying the IL leaves a bundle that still builds against
    /// while containing none of the game's code. This is the same intent as the "strip" in
    /// ProjectSIRA/Suto and beat-forge/GenericStripper, minus their virtualisation layer and minus their
    /// dependency on Windows.
    /// <para>
    /// Two things this must get right, both learned the hard way. The assembly must be read
    /// <c>InMemory</c> and written to a sibling temp file, because writing back over the path Cecil
    /// still holds truncates the file to zero. And the writer resolves cross-assembly references even
    /// though it emits nothing for them, so a resolver pointed at the bundle's own directories is
    /// required or the write throws.
    /// </para>
    /// </remarks>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("usage: strip-refs <bundle-dir>");
                return 2;
            }

            string root = Path.GetFullPath(args[0]);
            if (!Directory.Exists(root))
            {
                Console.Error.WriteLine($"error: '{root}' is not a directory");
                return 1;
            }

            var resolver = BuildResolver(root);
            int stripped = 0, cleared = 0, skipped = 0;

            foreach (string path in Directory.GetFiles(root, "*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    using var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters
                    {
                        InMemory = true,
                        AssemblyResolver = resolver,
                    });

                    bool touched = false;
                    foreach (ModuleDefinition module in assembly.Modules)
                    {
                        foreach (TypeDefinition type in module.GetTypes())
                        {
                            foreach (MethodDefinition method in type.Methods)
                            {
                                if (!method.HasBody)
                                {
                                    continue;
                                }

                                method.Body.Instructions.Clear();
                                method.Body.Variables.Clear();
                                method.Body.ExceptionHandlers.Clear();
                                cleared++;
                                touched = true;
                            }
                        }
                    }

                    if (!touched)
                    {
                        continue;
                    }

                    string temp = path + ".stripping";
                    assembly.Write(temp);
                    File.Move(temp, path, overwrite: true);
                    stripped++;
                }
                catch (Exception e)
                {
                    // Native DLLs and assemblies whose references are not in the bundle land here.
                    // Leaving them untouched is safe: they are only ever compile-time references.
                    Console.Error.WriteLine($"  skip {Path.GetFileName(path)}: {e.Message}");
                    skipped++;
                }
            }

            Console.WriteLine($"stripped {stripped} assemblies, cleared {cleared} method bodies, skipped {skipped}");
            return 0;
        }

        private static DefaultAssemblyResolver BuildResolver(string root)
        {
            var resolver = new DefaultAssemblyResolver();
            foreach (string dir in new[]
                     {
                         Path.Combine(root, "Beat Saber_Data", "Managed"),
                         Path.Combine(root, "Libs"),
                         Path.Combine(root, "Plugins"),
                         Path.Combine(root, "IPA", "Libs"),
                         Path.Combine(root, "IPA", "Data", "Managed"),
                     })
            {
                if (Directory.Exists(dir))
                {
                    resolver.AddSearchDirectory(dir);
                }
            }

            return resolver;
        }
    }
}
