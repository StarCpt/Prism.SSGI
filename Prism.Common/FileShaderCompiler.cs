using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Prism.Common
{
    public class FileShaderCompiler : IShaderCompiler
    {
        private class FileIncludeHandler : Include
        {
            public IDisposable Shadow { get; set; }

            private readonly string _compileFilePath;
            private readonly string _baseSystemIncludePath;

            public FileIncludeHandler(string compileFilePath, string baseSystemIncludePath)
            {
                _compileFilePath = Path.GetDirectoryName(compileFilePath);
                _baseSystemIncludePath = baseSystemIncludePath;
            }

            public void Close(Stream stream)
            {
                stream.Dispose();
            }

            public Stream Open(IncludeType type, string fileName, Stream parentStream)
            {
                if (fileName == null)
                {
                    throw new ArgumentNullException(nameof(fileName));
                }

                string includeFilePath;
                if (type is IncludeType.Local)
                {
                    if (parentStream is FileStream fs)
                    {
                        string parentFilePath = Path.GetDirectoryName(fs.Name);
                        includeFilePath = Path.Combine(parentFilePath, fileName);
                    }
                    else
                    {
                        includeFilePath = Path.Combine(_compileFilePath, fileName);
                    }
                }
                else if (type is IncludeType.System)
                {
                    includeFilePath = Path.Combine(_baseSystemIncludePath, fileName);
                }
                else
                {
                    throw new ArgumentException($"Invalid {nameof(IncludeType)}.");
                }

                return File.OpenRead(includeFilePath);
            }

            public void Dispose()
            {
                Shadow?.Dispose();
            }
        }

        private readonly string _baseShaderPath;
        private readonly string _gameShaderBasePath;

        public FileShaderCompiler(string baseShaderPath, string gameShaderBasePath)
        {
            _baseShaderPath = baseShaderPath;
            _gameShaderBasePath = gameShaderBasePath;
        }

        public VertexShader CompileVertex(Device device, string id, string entryPoint, params ShaderMacro[] defines)
        {
            CompilationResult compilation = CompileVertexBytecode(id, entryPoint, defines);
            return new VertexShader(device, compilation);
        }

        public PixelShader CompilePixel(Device device, string id, string entryPoint, params ShaderMacro[] defines)
        {
            CompilationResult compilation = CompilePixelBytecode(id, entryPoint, defines);
            return new PixelShader(device, compilation);
        }

        public ComputeShader CompileCompute(Device device, string id, string entryPoint, params ShaderMacro[] defines)
        {
            CompilationResult compilation = CompileComputeBytecode(id, entryPoint, defines);
            return new ComputeShader(device, compilation);
        }

        public CompilationResult CompileVertexBytecode(string id, string entryPoint, params ShaderMacro[] defines) => CompileBytecodeInternal(id, entryPoint, "vs_5_0", defines);
        public CompilationResult CompilePixelBytecode(string id, string entryPoint, params ShaderMacro[] defines) => CompileBytecodeInternal(id, entryPoint, "ps_5_0", defines);
        public CompilationResult CompileComputeBytecode(string id, string entryPoint, params ShaderMacro[] defines) => CompileBytecodeInternal(id, entryPoint, "cs_5_0", defines);

        private CompilationResult CompileBytecodeInternal(string id, string entryPoint, string profile, ShaderMacro[] defines)
        {
            string filePath = Path.Combine(_baseShaderPath, id);
            using StreamReader sr = new(filePath, Encoding.UTF8);
            string shaderSource = sr.ReadToEnd();

            if (string.IsNullOrEmpty(shaderSource))
            {
                throw new ArgumentNullException(nameof(shaderSource));
            }

            using Include include = new FileIncludeHandler(filePath, _gameShaderBasePath);
            IntPtr sourcePtr = Marshal.StringToHGlobalAnsi(shaderSource);
            try
            {
                return ShaderBytecode.Compile(sourcePtr, shaderSource.Length, entryPoint, profile, ShaderFlags.OptimizationLevel3, EffectFlags.None, defines, include);
            }
            finally
            {
                if (sourcePtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(sourcePtr);
                }
            }
        }
    }
}
