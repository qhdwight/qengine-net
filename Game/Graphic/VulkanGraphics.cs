using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace Game.Graphic;

internal record struct QueueFamilyIndices
{
    public uint? GraphicsFamily { get; set; }
    public uint? PresentFamily { get; set; }

    public bool IsComplete() => GraphicsFamily.HasValue && PresentFamily.HasValue;
}

internal record struct SwapChainSupportDetails
{
    public SurfaceCapabilitiesKHR capabilities;
    public SurfaceFormatKHR[] formats;
    public PresentModeKHR[] presentModes;
}

internal static unsafe class VulkanGraphics
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

    // public void Run()
    // {
    //     InitWindow();
    //     InitVulkan();
    //     MainLoop();
    //     CleanUp();
    // }

    private static void InitWindow(ref Graphics graphics)
    {
        WindowOptions options = WindowOptions.DefaultVulkan with { Title = WindowName };

        graphics.window = Window.Create(options);
        graphics.window.Initialize();

        if (graphics.window.VkSurface is null)
            throw new Exception("Windowing platform doesn't support Vulkan.");
    }

    public static void InitVulkan(ref Graphics graphics)
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
        CreateGraphicsPipeline(ref graphics);
        CreateFramebuffers(ref graphics);
        CreateCommandPool(ref graphics);
        CreateCommandBuffers(ref graphics);
        CreateSyncObjects(ref graphics);
    }

    public static void CleanUp(ref Graphics graphics)
    {
        for (var i = 0; i < MaxFramesInFlight; i++)
        {
            graphics.vk!.DestroySemaphore(graphics.device, graphics.renderFinishedSemaphores![i], null);
            graphics.vk!.DestroySemaphore(graphics.device, graphics.imageAvailableSemaphores![i], null);
            graphics.vk!.DestroyFence(graphics.device, graphics.inFlightFences![i], null);
        }

        graphics.vk!.DestroyCommandPool(graphics.device, graphics.commandPool, null);

        foreach (Framebuffer framebuffer in graphics.swapChainFramebuffers!)
        {
            graphics.vk!.DestroyFramebuffer(graphics.device, framebuffer, null);
        }

        graphics.vk!.DestroyPipeline(graphics.device, graphics.graphicsPipeline, null);
        graphics.vk!.DestroyPipelineLayout(graphics.device, graphics.pipelineLayout, null);
        graphics.vk!.DestroyRenderPass(graphics.device, graphics.renderPass, null);

        foreach (ImageView imageView in graphics.swapChainImageViews!)
        {
            graphics.vk!.DestroyImageView(graphics.device, imageView, null);
        }

        graphics.khrSwapChain!.DestroySwapchain(graphics.device, graphics.swapChain, null);

        graphics.vk!.DestroyDevice(graphics.device, null);

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

    private static void CreateInstance(ref Graphics graphics)
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

    private static void SetupDebugMessenger(ref Graphics graphics)
    {
        if (!EnableValidationLayers) return;

        // TryGetInstanceExtension equivalent to method CreateDebugUtilsMessengerEXT from original tutorial.
        if (!graphics.vk!.TryGetInstanceExtension(graphics.instance, out graphics.debugUtils)) return;

        DebugUtilsMessengerCreateInfoEXT createInfo = new();
        PopulateDebugMessengerCreateInfo(ref createInfo);

        if (graphics.debugUtils!.CreateDebugUtilsMessenger(graphics.instance, in createInfo, null, out graphics.debugMessenger) != Result.Success)
            throw new Exception("Failed to set up debug messenger!");
    }

    private static void CreateSurface(ref Graphics graphics)
    {
        if (!graphics.vk!.TryGetInstanceExtension<KhrSurface>(graphics.instance, out graphics.khrSurface))
            throw new NotSupportedException("KHR_surface extension not found.");

        graphics.surface = graphics.window!.VkSurface!.Create<AllocationCallbacks>(graphics.instance.ToHandle(), null).ToSurface();
    }

    private static void PickPhysicalDevice(ref Graphics graphics)
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

    private static void CreateLogicalDevice(ref Graphics graphics)
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

    private static void CreateSwapChain(ref Graphics graphics)
    {
        SwapChainSupportDetails swapChainSupport = QuerySwapChainSupport(ref graphics, graphics.physicalDevice);

        SurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.formats);
        PresentModeKHR presentMode = ChoosePresentMode(swapChainSupport.presentModes);
        Extent2D extent = ChooseSwapExtent(ref graphics, swapChainSupport.capabilities);

        uint imageCount = swapChainSupport.capabilities.MinImageCount + 1;
        if (swapChainSupport.capabilities.MaxImageCount > 0 && imageCount > swapChainSupport.capabilities.MaxImageCount)
            imageCount = swapChainSupport.capabilities.MaxImageCount;

        SwapchainCreateInfoKHR createInfo = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = graphics.surface,

            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ImageUsageColorAttachmentBit
        };

        QueueFamilyIndices indices = FindQueueFamilies(ref graphics, graphics.physicalDevice);
        uint* queueFamilyIndices = stackalloc[] { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };

        if (indices.GraphicsFamily != indices.PresentFamily)
        {
            createInfo = createInfo with
            {
                ImageSharingMode = SharingMode.Concurrent,
                QueueFamilyIndexCount = 2,
                PQueueFamilyIndices = queueFamilyIndices
            };
        }
        else
        {
            createInfo.ImageSharingMode = SharingMode.Exclusive;
        }

        createInfo = createInfo with
        {
            PreTransform = swapChainSupport.capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.CompositeAlphaOpaqueBitKhr,
            PresentMode = presentMode,
            Clipped = true,

            OldSwapchain = default
        };

        if (!graphics.vk!.TryGetDeviceExtension(graphics.instance, graphics.device, out graphics.khrSwapChain))
            throw new NotSupportedException("VK_KHR_swapchain extension not found.");

        if (graphics.khrSwapChain!.CreateSwapchain(graphics.device, createInfo, null, out graphics.swapChain) != Result.Success)
            throw new Exception("Failed to create swap chain!");

        graphics.khrSwapChain.GetSwapchainImages(graphics.device, graphics.swapChain, ref imageCount, null);
        graphics.swapChainImages = new Image[imageCount];
        fixed (Image* swapChainImagesPtr = graphics.swapChainImages)
            graphics.khrSwapChain.GetSwapchainImages(graphics.device, graphics.swapChain, ref imageCount, swapChainImagesPtr);

        graphics.swapChainImageFormat = surfaceFormat.Format;
        graphics.swapChainExtent = extent;
    }

    private static void CreateImageViews(ref Graphics graphics)
    {
        graphics.swapChainImageViews = new ImageView[graphics.swapChainImages!.Length];

        for (var i = 0; i < graphics.swapChainImages.Length; i++)
        {
            ImageViewCreateInfo createInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = graphics.swapChainImages[i],
                ViewType = ImageViewType.ImageViewType2D,
                Format = graphics.swapChainImageFormat,
                Components =
                {
                    R = ComponentSwizzle.Identity,
                    G = ComponentSwizzle.Identity,
                    B = ComponentSwizzle.Identity,
                    A = ComponentSwizzle.Identity
                },
                SubresourceRange =
                {
                    AspectMask = ImageAspectFlags.ImageAspectColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            if (graphics.vk!.CreateImageView(graphics.device, createInfo, null, out graphics.swapChainImageViews[i]) != Result.Success)
                throw new Exception("Failed to create image views!");
        }
    }

    private static void CreateRenderPass(ref Graphics graphics)
    {
        AttachmentDescription colorAttachment = new()
        {
            Format = graphics.swapChainImageFormat,
            Samples = SampleCountFlags.SampleCount1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };

        AttachmentReference colorAttachmentRef = new()
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal
        };

        SubpassDescription subpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef
        };

        SubpassDependency dependency = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.PipelineStageColorAttachmentOutputBit,
            SrcAccessMask = 0,
            DstStageMask = PipelineStageFlags.PipelineStageColorAttachmentOutputBit,
            DstAccessMask = AccessFlags.AccessColorAttachmentWriteBit
        };

        RenderPassCreateInfo renderPassInfo = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            PAttachments = &colorAttachment,
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 1,
            PDependencies = &dependency
        };

        if (graphics.vk!.CreateRenderPass(graphics.device, renderPassInfo, null, out graphics.renderPass) != Result.Success)
            throw new Exception("Failed to create render pass!");
    }

    private static void CreateGraphicsPipeline(ref Graphics graphics)
    {
        byte[] vertShaderCode = Resources.TriangleVert;
        byte[] fragShaderCode = Resources.TriangleFrag;

        ShaderModule vertShaderModule = CreateShaderModule(ref graphics, vertShaderCode);
        ShaderModule fragShaderModule = CreateShaderModule(ref graphics, fragShaderCode);

        PipelineShaderStageCreateInfo vertShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.ShaderStageVertexBit,
            Module = vertShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };

        PipelineShaderStageCreateInfo fragShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.ShaderStageFragmentBit,
            Module = fragShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };

        PipelineShaderStageCreateInfo* shaderStages = stackalloc[]
        {
            vertShaderStageInfo,
            fragShaderStageInfo
        };

        PipelineVertexInputStateCreateInfo vertexInputInfo = new()
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = 0,
            VertexAttributeDescriptionCount = 0
        };

        PipelineInputAssemblyStateCreateInfo inputAssembly = new()
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = PrimitiveTopology.TriangleList,
            PrimitiveRestartEnable = false
        };

        Viewport viewport = new()
        {
            X = 0,
            Y = 0,
            Width = graphics.swapChainExtent.Width,
            Height = graphics.swapChainExtent.Height,
            MinDepth = 0,
            MaxDepth = 1
        };

        Rect2D scissor = new()
        {
            Offset = { X = 0, Y = 0 },
            Extent = graphics.swapChainExtent
        };

        PipelineViewportStateCreateInfo viewportState = new()
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            PViewports = &viewport,
            ScissorCount = 1,
            PScissors = &scissor
        };

        PipelineRasterizationStateCreateInfo rasterizer = new()
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            DepthClampEnable = false,
            RasterizerDiscardEnable = false,
            PolygonMode = PolygonMode.Fill,
            LineWidth = 1,
            CullMode = CullModeFlags.CullModeBackBit,
            FrontFace = FrontFace.Clockwise,
            DepthBiasEnable = false
        };

        PipelineMultisampleStateCreateInfo multisampling = new()
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            SampleShadingEnable = false,
            RasterizationSamples = SampleCountFlags.SampleCount1Bit
        };

        PipelineColorBlendAttachmentState colorBlendAttachment = new()
        {
            ColorWriteMask = ColorComponentFlags.ColorComponentRBit | ColorComponentFlags.ColorComponentGBit | ColorComponentFlags.ColorComponentBBit |
                             ColorComponentFlags.ColorComponentABit,
            BlendEnable = false
        };

        PipelineColorBlendStateCreateInfo colorBlending = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            LogicOpEnable = false,
            LogicOp = LogicOp.Copy,
            AttachmentCount = 1,
            PAttachments = &colorBlendAttachment
        };

        colorBlending.BlendConstants[0] = 0;
        colorBlending.BlendConstants[1] = 0;
        colorBlending.BlendConstants[2] = 0;
        colorBlending.BlendConstants[3] = 0;

        PipelineLayoutCreateInfo pipelineLayoutInfo = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 0,
            PushConstantRangeCount = 0
        };

        if (graphics.vk!.CreatePipelineLayout(graphics.device, pipelineLayoutInfo, null, out graphics.pipelineLayout) != Result.Success)
            throw new Exception("Failed to create pipeline layout!");

        GraphicsPipelineCreateInfo pipelineInfo = new()
        {
            SType = StructureType.GraphicsPipelineCreateInfo,
            StageCount = 2,
            PStages = shaderStages,
            PVertexInputState = &vertexInputInfo,
            PInputAssemblyState = &inputAssembly,
            PViewportState = &viewportState,
            PRasterizationState = &rasterizer,
            PMultisampleState = &multisampling,
            PColorBlendState = &colorBlending,
            Layout = graphics.pipelineLayout,
            RenderPass = graphics.renderPass,
            Subpass = 0,
            BasePipelineHandle = default
        };

        if (graphics.vk!.CreateGraphicsPipelines(graphics.device, default, 1, pipelineInfo, null, out graphics.graphicsPipeline) != Result.Success)
        {
            throw new Exception("Failed to create graphics pipeline!");
        }

        graphics.vk!.DestroyShaderModule(graphics.device, fragShaderModule, null);
        graphics.vk!.DestroyShaderModule(graphics.device, vertShaderModule, null);

        SilkMarshal.Free((nint)vertShaderStageInfo.PName);
        SilkMarshal.Free((nint)fragShaderStageInfo.PName);
    }

    private static void CreateFramebuffers(ref Graphics graphics)
    {
        graphics.swapChainFramebuffers = new Framebuffer[graphics.swapChainImageViews!.Length];

        for (var i = 0; i < graphics.swapChainImageViews.Length; i++)
        {
            ImageView attachment = graphics.swapChainImageViews[i];
            var framebufferInfo = new FramebufferCreateInfo
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = graphics.renderPass,
                AttachmentCount = 1,
                PAttachments = &attachment,
                Width = graphics.swapChainExtent.Width,
                Height = graphics.swapChainExtent.Height,
                Layers = 1
            };

            var framebuffer = new Framebuffer();
            if (graphics.vk!.CreateFramebuffer(graphics.device, &framebufferInfo, null, &framebuffer) != Result.Success)
                throw new Exception("failed to create framebuffer!");

            graphics.swapChainFramebuffers[i] = framebuffer;
        }
    }

    private static void CreateCommandPool(ref Graphics graphics)
    {
        QueueFamilyIndices queueFamilyIndices = FindQueueFamilies(ref graphics, graphics.physicalDevice);

        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = queueFamilyIndices.GraphicsFamily!.Value
        };

        if (graphics.vk!.CreateCommandPool(graphics.device, poolInfo, null, out graphics.commandPool) != Result.Success)
        {
            throw new Exception("Failed to create command pool!");
        }
    }

    private static void CreateCommandBuffers(ref Graphics graphics)
    {
        graphics.commandBuffers = new CommandBuffer[graphics.swapChainFramebuffers!.Length];

        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = graphics.commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = (uint)graphics.commandBuffers.Length
        };

        fixed (CommandBuffer* commandBuffersPtr = graphics.commandBuffers)
        {
            if (graphics.vk!.AllocateCommandBuffers(graphics.device, allocInfo, commandBuffersPtr) != Result.Success)
            {
                throw new Exception("Failed to allocate command buffers!");
            }
        }

        for (var i = 0; i < graphics.commandBuffers.Length; i++)
        {
            CommandBufferBeginInfo beginInfo = new()
            {
                SType = StructureType.CommandBufferBeginInfo
            };

            if (graphics.vk!.BeginCommandBuffer(graphics.commandBuffers[i], beginInfo) != Result.Success)
                throw new Exception("Failed to begin recording command buffer!");

            RenderPassBeginInfo renderPassInfo = new()
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = graphics.renderPass,
                Framebuffer = graphics.swapChainFramebuffers[i],
                RenderArea =
                {
                    Offset = { X = 0, Y = 0 },
                    Extent = graphics.swapChainExtent
                }
            };

            ClearValue clearColor = new()
            {
                Color = new ClearColorValue { Float32_0 = 0, Float32_1 = 0, Float32_2 = 0, Float32_3 = 1 }
            };

            renderPassInfo.ClearValueCount = 1;
            renderPassInfo.PClearValues = &clearColor;

            graphics.vk!.CmdBeginRenderPass(graphics.commandBuffers[i], &renderPassInfo, SubpassContents.Inline);

            graphics.vk!.CmdBindPipeline(graphics.commandBuffers[i], PipelineBindPoint.Graphics, graphics.graphicsPipeline);

            graphics.vk!.CmdDraw(graphics.commandBuffers[i], 3, 1, 0, 0);

            graphics.vk!.CmdEndRenderPass(graphics.commandBuffers[i]);

            if (graphics.vk!.EndCommandBuffer(graphics.commandBuffers[i]) != Result.Success)
            {
                throw new Exception("Failed to record command buffer!");
            }
        }
    }

    private static void CreateSyncObjects(ref Graphics graphics)
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

    public static void Render(ref Graphics graphics, double delta)
    {
        graphics.vk!.WaitForFences(graphics.device, 1, graphics.inFlightFences![graphics.currentFrame], true, ulong.MaxValue);

        uint imageIndex = 0;
        graphics.khrSwapChain!.AcquireNextImage(graphics.device, graphics.swapChain, ulong.MaxValue,
                                                graphics.imageAvailableSemaphores![graphics.currentFrame], default,
                                                ref imageIndex);

        if (graphics.imagesInFlight![imageIndex].Handle != default)
            graphics.vk!.WaitForFences(graphics.device, 1, graphics.imagesInFlight[imageIndex], true, ulong.MaxValue);
        graphics.imagesInFlight[imageIndex] = graphics.inFlightFences[graphics.currentFrame];

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo
        };

        Semaphore* waitSemaphores = stackalloc[] { graphics.imageAvailableSemaphores[graphics.currentFrame] };
        PipelineStageFlags* waitStages = stackalloc[] { PipelineStageFlags.PipelineStageColorAttachmentOutputBit };

        CommandBuffer buffer = graphics.commandBuffers![imageIndex];

        submitInfo = submitInfo with
        {
            WaitSemaphoreCount = 1,
            PWaitSemaphores = waitSemaphores,
            PWaitDstStageMask = waitStages,

            CommandBufferCount = 1,
            PCommandBuffers = &buffer
        };

        Semaphore* signalSemaphores = stackalloc[] { graphics.renderFinishedSemaphores![graphics.currentFrame] };
        submitInfo = submitInfo with
        {
            SignalSemaphoreCount = 1,
            PSignalSemaphores = signalSemaphores
        };

        graphics.vk!.ResetFences(graphics.device, 1, graphics.inFlightFences[graphics.currentFrame]);

        if (graphics.vk!.QueueSubmit(graphics.graphicsQueue, 1, submitInfo, graphics.inFlightFences[graphics.currentFrame]) != Result.Success)
            throw new Exception("Failed to submit draw command buffer!");

        SwapchainKHR* swapChains = stackalloc[] { graphics.swapChain };
        PresentInfoKHR presentInfo = new()
        {
            SType = StructureType.PresentInfoKhr,

            WaitSemaphoreCount = 1,
            PWaitSemaphores = signalSemaphores,

            SwapchainCount = 1,
            PSwapchains = swapChains,

            PImageIndices = &imageIndex
        };

        graphics.khrSwapChain.QueuePresent(graphics.presentQueue, presentInfo);

        graphics.currentFrame = (graphics.currentFrame + 1) % MaxFramesInFlight;

        graphics.vk!.DeviceWaitIdle(graphics.device);
    }

    private static ShaderModule CreateShaderModule(ref Graphics graphics, byte[] code)
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

    private static SurfaceFormatKHR ChooseSwapSurfaceFormat(IReadOnlyList<SurfaceFormatKHR> availableFormats)
    {
        foreach (SurfaceFormatKHR availableFormat in availableFormats)
            if (availableFormat.Format == Format.B8G8R8A8Unorm && availableFormat.ColorSpace == ColorSpaceKHR.ColorSpaceSrgbNonlinearKhr)
                return availableFormat;

        return availableFormats[0];
    }

    private static PresentModeKHR ChoosePresentMode(IEnumerable<PresentModeKHR> availablePresentModes)
    {
        foreach (PresentModeKHR availablePresentMode in availablePresentModes)
            if (availablePresentMode == PresentModeKHR.PresentModeMailboxKhr)
                return availablePresentMode;

        return PresentModeKHR.PresentModeFifoKhr;
    }

    private static Extent2D ChooseSwapExtent(ref Graphics graphics, SurfaceCapabilitiesKHR capabilities)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
            return capabilities.CurrentExtent;

        Vector2D<int> framebufferSize = graphics.window!.FramebufferSize;

        Extent2D actualExtent = new()
        {
            Width = (uint)framebufferSize.X,
            Height = (uint)framebufferSize.Y
        };

        actualExtent.Width = Math.Clamp(actualExtent.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
        actualExtent.Height = Math.Clamp(actualExtent.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);

        return actualExtent;
    }

    private static SwapChainSupportDetails QuerySwapChainSupport(ref Graphics graphics, PhysicalDevice physicalDevice)
    {
        var details = new SwapChainSupportDetails();

        graphics.khrSurface!.GetPhysicalDeviceSurfaceCapabilities(physicalDevice, graphics.surface, out details.capabilities);

        uint formatCount = 0;
        graphics.khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, graphics.surface, ref formatCount, null);

        if (formatCount != 0)
        {
            details.formats = new SurfaceFormatKHR[formatCount];
            fixed (SurfaceFormatKHR* formatsPtr = details.formats)
                graphics.khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, graphics.surface, ref formatCount, formatsPtr);
        }
        else
        {
            details.formats = Array.Empty<SurfaceFormatKHR>();
        }

        uint presentModeCount = 0;
        graphics.khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, graphics.surface, ref presentModeCount, null);

        if (presentModeCount != 0)
        {
            details.presentModes = new PresentModeKHR[presentModeCount];
            fixed (PresentModeKHR* formatsPtr = details.presentModes)
                graphics.khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, graphics.surface, ref presentModeCount, formatsPtr);
        }
        else
        {
            details.presentModes = Array.Empty<PresentModeKHR>();
        }

        return details;
    }

    private static bool IsDeviceSuitable(ref Graphics graphics, PhysicalDevice device)
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

    private static bool CheckDeviceExtensionsSupport(ref Graphics graphics, PhysicalDevice device)
    {
        uint extensionCount = 0;
        graphics.vk!.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionCount, null);

        var availableExtensions = new ExtensionProperties[extensionCount];
        fixed (ExtensionProperties* availableExtensionsPtr = availableExtensions)
            graphics.vk!.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionCount, availableExtensionsPtr);

        HashSet<string?> availableExtensionNames = availableExtensions.Select(extension => Marshal.PtrToStringAnsi((IntPtr)extension.ExtensionName)).ToHashSet();

        return DeviceExtensions.All(availableExtensionNames.Contains);
    }

    private static QueueFamilyIndices FindQueueFamilies(ref Graphics graphics, PhysicalDevice device)
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

    private static string[] GetRequiredExtensions(ref Graphics graphics)
    {
        byte** glfwExtensions = graphics.window!.VkSurface!.GetRequiredExtensions(out uint glfwExtensionCount);
        string[]? extensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)glfwExtensionCount);

        return EnableValidationLayers
            ? extensions.Append(ExtDebugUtils.ExtensionName).ToArray()
            : extensions;
    }

    private static bool CheckValidationLayerSupport(ref Graphics graphics)
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