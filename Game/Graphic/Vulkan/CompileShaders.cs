using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Game.Graphic.Vulkan;

internal static partial class VulkanGraphics
{
    // ReSharper disable once UnusedMember.Global
    public static void Compile()
    {
        string resourceDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..\\..\\..\\Resource"));
        string[] fileNames = Directory.GetFiles(resourceDir).Where(fileName => !fileName.EndsWith("spv")).ToArray();
        foreach (string fileName in fileNames)
        {
            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = false,
                UseShellExecute = false,
                FileName = "C:\\VulkanSDK\\1.2.198.1\\Bin\\glslc.exe",
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = $"{fileName} -o {fileName}.spv",
                RedirectStandardError = true, RedirectStandardOutput = true
            };
            Console.Out.WriteLine($"Compiling {fileName}...");
            using Process exeProcess = Process.Start(startInfo)!;
            Console.Out.Write(exeProcess.StandardOutput.ReadToEnd());
            Console.Error.Write(exeProcess.StandardError.ReadToEnd());
            exeProcess.WaitForExit();
        }
    }
}