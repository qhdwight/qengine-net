using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace Game.Graphic.Vulkan;

internal record struct QueueFamilyIndices
{
    internal uint? GraphicsFamily { get; set; }
    internal uint? PresentFamily { get; set; }

    internal bool IsComplete() => GraphicsFamily.HasValue && PresentFamily.HasValue;
}

internal record struct SwapChainSupportDetails
{
    internal SurfaceCapabilitiesKHR capabilities;
    internal SurfaceFormatKHR[] formats;
    internal PresentModeKHR[] presentModes;
}

internal static unsafe partial class VulkanGraphics
{
    private const string WindowName = "Game";
    private const string EngineName = "QLib";

    private const int MaxFramesInFlight = 2;

#if DEBUG
    private static bool EnableValidationLayers = true;
#else
    private static bool EnableValidationLayers = false;
#endif

    private static readonly string[] ValidationLayers =
    {
        "VK_LAYER_KHRONOS_validation"
    };

    private static readonly string[] DeviceExtensions =
    {
        KhrSwapchain.ExtensionName
    };

    private static void InitWindow(ref VkGraphics graphics)
    {
        WindowOptions options = WindowOptions.DefaultVulkan with { Title = WindowName };

        graphics.window = Window.Create(options);
        graphics.window.Initialize();

        if (graphics.window.VkSurface is null)
            throw new Exception("Windowing platform doesn't support Vulkan.");
    }

    internal static void InitVulkan(ref VkGraphics graphics)
    {
        InitWindow(ref graphics);
        CreateInstance(ref graphics);
        SetupDebugMessenger(ref graphics);
        CreateSurface(ref graphics);
        PickPhysicalDevice(ref graphics);
        CreateLogicalDevice(ref graphics);
        CreateSwapChain(ref graphics);
        CreateImageViews(ref graphics);
        CreateRenderPass(ref graphics);
        CreateDescriptorSetLayout(ref graphics);
        CreateGraphicsPipeline(ref graphics);
        CreateDepthResources(ref graphics);
        CreateFramebuffers(ref graphics);
        CreateCommandPool(ref graphics);
        CreateUniformBuffers(ref graphics);
        CreateDescriptorPool(ref graphics);
        CreateDescriptorSets(ref graphics);
        CreateCommandBuffers(ref graphics);
        CreateSyncObjects(ref graphics);
    }

    internal static void CleanupMeshBuffers(ref VkGraphics graphics, ref VkMesh vkMesh)
    {
        FreeMeshVertexBuffer(ref graphics, ref vkMesh);
        FreeMeshIndexBuffer(ref graphics, ref vkMesh);
    }
    
    internal static void FreeMeshVertexBuffer(ref VkGraphics graphics, ref VkMesh vkMesh)
    {
        Debug.Assert(vkMesh.vertexBuffer.Handle != default);
        Debug.Assert(vkMesh.vertexBufferMemory.Handle != default);
        graphics.vk!.DestroyBuffer(graphics.device, vkMesh.vertexBuffer, null);
        graphics.vk!.FreeMemory(graphics.device, vkMesh.vertexBufferMemory, null);
        vkMesh.vertexBuffer = default;
        vkMesh.vertexBufferMemory = default;
    }
    
    internal static void FreeMeshIndexBuffer(ref VkGraphics graphics, ref VkMesh vkMesh)
    {
        Debug.Assert(vkMesh.indexBuffer.Handle != default);
        Debug.Assert(vkMesh.indexBufferMemory.Handle != default);
        graphics.vk!.DestroyBuffer(graphics.device, vkMesh.indexBuffer, null);
        graphics.vk!.FreeMemory(graphics.device, vkMesh.indexBufferMemory, null);
        vkMesh.indexBuffer = default;
        vkMesh.indexBufferMemory = default;
    }

    internal static void CleanUp(ref VkGraphics graphics)
    {
        CleanUpSwapChain(ref graphics);

        graphics.vk!.DestroyDescriptorSetLayout(graphics.device, graphics.descriptorSetLayout, null);

        for (var i = 0; i < MaxFramesInFlight; i++)
        {
            graphics.vk.DestroySemaphore(graphics.device, graphics.renderFinishedSemaphores![i], null);
            graphics.vk.DestroySemaphore(graphics.device, graphics.imageAvailableSemaphores![i], null);
            graphics.vk.DestroyFence(graphics.device, graphics.inFlightFences![i], null);
        }

        graphics.vk.DestroyCommandPool(graphics.device, graphics.commandPool, null);

        graphics.vk.DestroyDevice(graphics.device, null);

        if (EnableValidationLayers)
        {
            // DestroyDebugUtilsMessenger equivalent to method DestroyDebugUtilsMessengerEXT from original tutorial.
            graphics.debugUtils!.DestroyDebugUtilsMessenger(graphics.instance, graphics.debugMessenger, null);
        }

        graphics.khrSurface!.DestroySurface(graphics.instance, graphics.surface, null);
        graphics.vk!.DestroyInstance(graphics.instance, null);
        graphics.vk!.Dispose();

        graphics.window?.Dispose();
    }

    private static void CreateInstance(ref VkGraphics graphics)
    {
        graphics.vk = Vk.GetApi();

        if (EnableValidationLayers && !CheckValidationLayerSupport(ref graphics))
            throw new Exception("Validation layers requested, but not available!");

        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi(WindowName),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi(EngineName),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version12
        };

        InstanceCreateInfo createInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo
        };

        string[] extensions = GetRequiredExtensions(ref graphics);
        createInfo.EnabledExtensionCount = (uint)extensions.Length;
        createInfo.PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions);

        if (EnableValidationLayers)
        {
            createInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
            createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(ValidationLayers);

            DebugUtilsMessengerCreateInfoEXT debugCreateInfo = new();
            PopulateDebugMessengerCreateInfo(ref debugCreateInfo);
            createInfo.PNext = &debugCreateInfo;
        }
        else
        {
            createInfo.EnabledLayerCount = 0;
            createInfo.PNext = null;
        }

        if (graphics.vk!.CreateInstance(createInfo, null, out graphics.instance) != Result.Success)
            throw new Exception("Failed to create instance!");

        Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
        Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
        SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);

        if (EnableValidationLayers)
            SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
    }

    private static void PopulateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInfoEXT createInfo)
    {
        createInfo.SType = StructureType.DebugUtilsMessengerCreateInfoExt;
        createInfo.MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityVerboseBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityWarningBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityErrorBitExt;
        createInfo.MessageType = DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeGeneralBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypePerformanceBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeValidationBitExt;
        createInfo.PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)DebugCallback;
    }

    private static void SetupDebugMessenger(ref VkGraphics graphics)
    {
        if (!EnableValidationLayers) return;

        // TryGetInstanceExtension equivalent to method CreateDebugUtilsMessengerEXT from original tutorial.
        if (!graphics.vk!.TryGetInstanceExtension(graphics.instance, out graphics.debugUtils)) return;

        DebugUtilsMessengerCreateInfoEXT createInfo = new();
        PopulateDebugMessengerCreateInfo(ref createInfo);

        if (graphics.debugUtils!.CreateDebugUtilsMessenger(graphics.instance, in createInfo, null, out graphics.debugMessenger) != Result.Success)
            throw new Exception("Failed to set up debug messenger!");
    }

    private static void CreateSurface(ref VkGraphics graphics)
    {
        if (!graphics.vk!.TryGetInstanceExtension<KhrSurface>(graphics.instance, out graphics.khrSurface))
            throw new NotSupportedException("KHR_surface extension not found.");

        graphics.surface = graphics.window!.VkSurface!.Create<AllocationCallbacks>(graphics.instance.ToHandle(), null).ToSurface();
    }

    private static void PickPhysicalDevice(ref VkGraphics graphics)
    {
        uint deviceCount = 0;
        graphics.vk!.EnumeratePhysicalDevices(graphics.instance, ref deviceCount, null);

        if (deviceCount == 0)
            throw new Exception("Failed to find GPUs with Vulkan support!");

        var devices = new PhysicalDevice[deviceCount];
        fixed (PhysicalDevice* devicesPtr = devices)
            graphics.vk!.EnumeratePhysicalDevices(graphics.instance, ref deviceCount, devicesPtr);

        foreach (PhysicalDevice dev in devices)
        {
            if (IsDeviceSuitable(ref graphics, dev))
            {
                graphics.physicalDevice = dev;
                break;
            }
        }

        if (graphics.physicalDevice.Handle == 0)
            throw new Exception("Failed to find a suitable GPU!");
    }

    private static void CreateLogicalDevice(ref VkGraphics graphics)
    {
        QueueFamilyIndices indices = FindQueueFamilies(ref graphics, graphics.physicalDevice);

        uint[] uniqueQueueFamilies = { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };
        uniqueQueueFamilies = uniqueQueueFamilies.Distinct().ToArray();

        using GlobalMemory? mem = GlobalMemory.Allocate(uniqueQueueFamilies.Length * sizeof(DeviceQueueCreateInfo));
        var queueCreateInfos = (DeviceQueueCreateInfo*)Unsafe.AsPointer(ref mem.GetPinnableReference());

        var queuePriority = 1.0f;
        for (var i = 0; i < uniqueQueueFamilies.Length; i++)
        {
            queueCreateInfos[i] = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = uniqueQueueFamilies[i],
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };
        }

        PhysicalDeviceFeatures deviceFeatures = new();

        DeviceCreateInfo createInfo = new()
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = (uint)uniqueQueueFamilies.Length,
            PQueueCreateInfos = queueCreateInfos,

            PEnabledFeatures = &deviceFeatures,

            EnabledExtensionCount = (uint)DeviceExtensions.Length,
            PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(DeviceExtensions)
        };

        if (EnableValidationLayers)
        {
            createInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
            createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(ValidationLayers);
        }
        else
        {
            createInfo.EnabledLayerCount = 0;
        }

        if (graphics.vk!.CreateDevice(graphics.physicalDevice, in createInfo, null, out graphics.device) != Result.Success)
            throw new Exception("Failed to create logical device!");

        graphics.vk!.GetDeviceQueue(graphics.device, indices.GraphicsFamily!.Value, 0, out graphics.graphicsQueue);
        graphics.vk!.GetDeviceQueue(graphics.device, indices.PresentFamily!.Value, 0, out graphics.presentQueue);

        if (EnableValidationLayers)
        {
            SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
        }

        SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);
    }

    private static void CreateSyncObjects(ref VkGraphics graphics)
    {
        graphics.imageAvailableSemaphores = new Semaphore[MaxFramesInFlight];
        graphics.renderFinishedSemaphores = new Semaphore[MaxFramesInFlight];
        graphics.inFlightFences = new Fence[MaxFramesInFlight];
        graphics.imagesInFlight = new Fence[graphics.swapChainImages!.Length];

        SemaphoreCreateInfo semaphoreInfo = new()
        {
            SType = StructureType.SemaphoreCreateInfo
        };

        FenceCreateInfo fenceInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.FenceCreateSignaledBit
        };

        for (var i = 0; i < MaxFramesInFlight; i++)
        {
            if (graphics.vk!.CreateSemaphore(graphics.device, semaphoreInfo, null, out graphics.imageAvailableSemaphores[i]) != Result.Success ||
                graphics.vk!.CreateSemaphore(graphics.device, semaphoreInfo, null, out graphics.renderFinishedSemaphores[i]) != Result.Success ||
                graphics.vk!.CreateFence(graphics.device, fenceInfo, null, out graphics.inFlightFences[i]) != Result.Success)
                throw new Exception("Failed to create synchronization objects for a frame!");
        }
    }

    private static ShaderModule CreateShaderModule(ref VkGraphics graphics, byte[] code)
    {
        ShaderModuleCreateInfo createInfo = new()
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (nuint)code.Length
        };

        ShaderModule shaderModule;

        fixed (byte* codePtr = code)
        {
            createInfo.PCode = (uint*)codePtr;

            if (graphics.vk!.CreateShaderModule(graphics.device, createInfo, null, out shaderModule) != Result.Success)
                throw new Exception("Failed to create shader module");
        }

        return shaderModule;
    }

    private static bool IsDeviceSuitable(ref VkGraphics graphics, PhysicalDevice device)
    {
        QueueFamilyIndices indices = FindQueueFamilies(ref graphics, device);

        bool extensionsSupported = CheckDeviceExtensionsSupport(ref graphics, device);

        var swapChainAdequate = false;
        if (extensionsSupported)
        {
            SwapChainSupportDetails swapChainSupport = QuerySwapChainSupport(ref graphics, device);
            swapChainAdequate = swapChainSupport.formats.Any() && swapChainSupport.presentModes.Any();
        }

        return indices.IsComplete() && extensionsSupported && swapChainAdequate;
    }

    private static bool CheckDeviceExtensionsSupport(ref VkGraphics graphics, PhysicalDevice device)
    {
        uint extensionCount = 0;
        graphics.vk!.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionCount, null);

        var availableExtensions = new ExtensionProperties[extensionCount];
        fixed (ExtensionProperties* availableExtensionsPtr = availableExtensions)
            graphics.vk!.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionCount, availableExtensionsPtr);

        HashSet<string?> availableExtensionNames = availableExtensions.Select(extension => Marshal.PtrToStringAnsi((IntPtr)extension.ExtensionName)).ToHashSet();

        return DeviceExtensions.All(availableExtensionNames.Contains);
    }

    private static QueueFamilyIndices FindQueueFamilies(ref VkGraphics graphics, PhysicalDevice device)
    {
        var indices = new QueueFamilyIndices();

        uint queueFamilyCount = 0;
        graphics.vk!.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);

        var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
            graphics.vk!.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, queueFamiliesPtr);

        uint i = 0;
        foreach (QueueFamilyProperties queueFamily in queueFamilies)
        {
            if (queueFamily.QueueFlags.HasFlag(QueueFlags.QueueGraphicsBit))
                indices.GraphicsFamily = i;

            graphics.khrSurface!.GetPhysicalDeviceSurfaceSupport(device, i, graphics.surface, out Bool32 presentSupport);

            if (presentSupport)
                indices.PresentFamily = i;

            if (indices.IsComplete())
                break;

            i++;
        }

        return indices;
    }

    private static string[] GetRequiredExtensions(ref VkGraphics graphics)
    {
        byte** glfwExtensions = graphics.window!.VkSurface!.GetRequiredExtensions(out uint glfwExtensionCount);
        string[]? extensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)glfwExtensionCount);

        return EnableValidationLayers
            ? extensions.Append(ExtDebugUtils.ExtensionName).ToArray()
            : extensions;
    }

    private static bool CheckValidationLayerSupport(ref VkGraphics graphics)
    {
        uint layerCount = 0;
        graphics.vk!.EnumerateInstanceLayerProperties(ref layerCount, null);
        var availableLayers = new LayerProperties[layerCount];
        fixed (LayerProperties* availableLayersPtr = availableLayers)
            graphics.vk!.EnumerateInstanceLayerProperties(ref layerCount, availableLayersPtr);

        HashSet<string?> availableLayerNames = availableLayers.Select(layer => Marshal.PtrToStringAnsi((IntPtr)layer.LayerName)).ToHashSet();

        return ValidationLayers.All(availableLayerNames.Contains);
    }

    private static uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity,
                                      DebugUtilsMessageTypeFlagsEXT messageTypes,
                                      DebugUtilsMessengerCallbackDataEXT* pCallbackData,
                                      void* pUserData)
    {
        if (messageSeverity.HasFlag(DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityVerboseBitExt))
            return Vk.False;
        var message = $"Validation layer: {Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage)}";
        if (messageSeverity.HasFlag(DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityErrorBitExt))
            Console.Error.WriteLine(message);
        else
            Console.Out.WriteLine(message);
        return Vk.False;
    }
}