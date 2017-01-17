﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Mvc.Razor.ViewCompilation.Internal
{
    public class ViewInfoContainerCodeGenerator
    {
        public ViewInfoContainerCodeGenerator(
            CSharpCompiler compiler,
            CSharpCompilation compilation)
        {
            Compiler = compiler;
            Compilation = compilation;
        }

        public CSharpCompiler Compiler { get; }

        public CSharpCompilation Compilation { get; private set; }

        public void AddViewFactory(IList<ViewCompilationInfo> result)
        {
            var precompiledViewsArray = new StringBuilder();
            foreach (var item in result)
            {
                var path = item.ViewFileInfo.ViewEnginePath;
                precompiledViewsArray.AppendLine(
                    $"new global::{typeof(ViewInfo).FullName}(@\"{path}\", typeof({item.TypeName})),");
            }

            var factoryContent = $@"
namespace {ViewsFeatureProvider.ViewInfoContainerNamespace}
{{
  public class {ViewsFeatureProvider.ViewInfoContainerTypeName} : global::{typeof(ViewInfoContainer).FullName}
  {{
    public {ViewsFeatureProvider.ViewInfoContainerTypeName}() : base(new[]
    {{
        {precompiledViewsArray}
    }})
    {{
    }}
  }}
}}";
            var syntaxTree = Compiler.CreateSyntaxTree(SourceText.From(factoryContent));
            Compilation = Compilation.AddSyntaxTrees(syntaxTree);
        }

        public void AddAssemblyMetadata(
            AssemblyName applicationAssemblyName,
            CompilationOptions compilationOptions)
        {
            if (!string.IsNullOrEmpty(compilationOptions.KeyFile))
            {
                var updatedOptions = Compilation.Options.WithStrongNameProvider(new DesktopStrongNameProvider());
                var keyFilePath = Path.GetFullPath(compilationOptions.KeyFile);

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || compilationOptions.PublicSign)
                {
                    updatedOptions = updatedOptions.WithCryptoPublicKey(
                        SnkUtils.ExtractPublicKey(File.ReadAllBytes(keyFilePath)));
                }
                else
                {
                    updatedOptions = updatedOptions.WithCryptoKeyFile(keyFilePath)
                        .WithDelaySign(compilationOptions.DelaySign);
                }

                Compilation = Compilation.WithOptions(updatedOptions);
            }

            var assemblyVersionContent = $"[assembly:{typeof(AssemblyVersionAttribute).FullName}(\"{applicationAssemblyName.Version}\")]";
            var syntaxTree = Compiler.CreateSyntaxTree(SourceText.From(assemblyVersionContent));
            Compilation = Compilation.AddSyntaxTrees(syntaxTree);
        }
    }
}
