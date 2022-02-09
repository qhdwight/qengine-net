using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Game.Graphic;

// ReSharper disable once UnusedType.Global
public static class CompileShaders
{
    // ReSharper disable once UnusedMember.Global
    public static void Compile()
    {
        string[] fileNames = Directory.GetFiles(".\\Resource").Where(fileName => !fileName.EndsWith("spv")).ToArray();
        foreach (string fileName in fileNames)
        {
            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = false,
                UseShellExecute = false,
                FileName = "C:\\VulkanSDK\\1.2.198.1\\Bin\\glslc.exe",
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = $"{fileName} -o {fileName}.spv"
            };
            Console.WriteLine($"Compiling {fileName}...");
            using Process exeProcess = Process.Start(startInfo)!;
            exeProcess.WaitForExit();
        }
    }
}