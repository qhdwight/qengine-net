using System;
using System.Collections.Generic;
using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace Game.Graphic.Vulkan;

internal static unsafe partial class VulkanGraphics
{
    private static void CreateSwapChain(ref VkGraphics graphics)
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

    private static void CreateImageViews(ref VkGraphics graphics)
    {
        graphics.swapChainImageViews = new ImageView[graphics.swapChainImages!.Length];

        for (var i = 0; i < graphics.swapChainImages!.Length; i++)
            graphics.swapChainImageViews![i] = CreateImageView(ref graphics, graphics.swapChainImages[i], graphics.swapChainImageFormat, ImageAspectFlags.ImageAspectColorBit);
    }

    private static ImageView CreateImageView(ref VkGraphics graphics, Image image, Format format, ImageAspectFlags aspectFlags)
    {
        ImageViewCreateInfo createInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.ImageViewType2D,
            Format = format,
            SubresourceRange =
            {
                AspectMask = aspectFlags,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        if (graphics.vk!.CreateImageView(graphics.device, createInfo, null, out ImageView imageView) != Result.Success)
            throw new Exception("Failed to create image views!");

        return imageView;
    }

    private static void CreateImage(ref VkGraphics graphics, uint width, uint height, Format format,
                                    ImageTiling tiling, ImageUsageFlags usage, MemoryPropertyFlags properties,
                                    ref Image image, ref DeviceMemory imageMemory)
    {
        ImageCreateInfo imageInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.ImageType2D,
            Extent =
            {
                Width = width,
                Height = height,
                Depth = 1
            },
            MipLevels = 1,
            ArrayLayers = 1,
            Format = format,
            Tiling = tiling,
            InitialLayout = ImageLayout.Undefined,
            Usage = usage,
            Samples = SampleCountFlags.SampleCount1Bit,
            SharingMode = SharingMode.Exclusive
        };

        fixed (Image* imagePtr = &image)
            if (graphics.vk!.CreateImage(graphics.device, imageInfo, null, imagePtr) != Result.Success)
                throw new Exception("failed to create image!");

        graphics.vk!.GetImageMemoryRequirements(graphics.device, image, out MemoryRequirements memRequirements);

        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(ref graphics, memRequirements.MemoryTypeBits, properties)
        };

        fixed (DeviceMemory* imageMemoryPtr = &imageMemory)
            if (graphics.vk!.AllocateMemory(graphics.device, allocInfo, null, imageMemoryPtr) != Result.Success)
                throw new Exception("failed to allocate image memory!");

        graphics.vk!.BindImageMemory(graphics.device, image, imageMemory, 0);
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

    private static Extent2D ChooseSwapExtent(ref VkGraphics graphics, SurfaceCapabilitiesKHR capabilities)
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

    private static SwapChainSupportDetails QuerySwapChainSupport(ref VkGraphics graphics, PhysicalDevice physicalDevice)
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

    private static void RecreateSwapChain(ref VkGraphics graphics)
    {
        Vector2D<int> framebufferSize = graphics.window!.FramebufferSize;

        // TODO: fix, should still be going game loop
        while (framebufferSize.X == 0 || framebufferSize.Y == 0)
        {
            framebufferSize = graphics.window.FramebufferSize;
            graphics.window.DoEvents();
        }

        graphics.vk!.DeviceWaitIdle(graphics.device);

        CleanUpSwapChain(ref graphics);

        CreateSwapChain(ref graphics);
        CreateImageViews(ref graphics);
        CreateRenderPass(ref graphics);
        CreateGraphicsPipeline(ref graphics);
        CreateDepthResources(ref graphics);
        CreateFramebuffers(ref graphics);
        CreateUniformBuffers(ref graphics);
        CreateDescriptorSets(ref graphics);
        CreateCommandBuffers(ref graphics);

        graphics.imagesInFlight = new Fence[graphics.swapChainImages!.Length];
    }

    private static void CleanUpSwapChain(ref VkGraphics graphics)
    {
        Vk vk = graphics.vk!;

        vk.DestroyImageView(graphics.device, graphics.depthImageView, null);
        vk.DestroyImage(graphics.device, graphics.depthImage, null);
        vk.FreeMemory(graphics.device, graphics.depthImageMemory, null);

        foreach (Framebuffer framebuffer in graphics.swapChainFramebuffers!)
            vk.DestroyFramebuffer(graphics.device, framebuffer, null);

        fixed (CommandBuffer* commandBuffersPtr = graphics.commandBuffers)
            vk.FreeCommandBuffers(graphics.device, graphics.cmdPool, (uint)graphics.commandBuffers!.Length, commandBuffersPtr);

        vk.DestroyPipeline(graphics.device, graphics.graphicsPipeline, null);
        vk.DestroyPipelineLayout(graphics.device, graphics.pipelineLayout, null);
        vk.DestroyRenderPass(graphics.device, graphics.renderPass, null);

        foreach (ImageView imageView in graphics.swapChainImageViews!)
            vk.DestroyImageView(graphics.device, imageView, null);

        graphics.khrSwapChain!.DestroySwapchain(graphics.device, graphics.swapChain, null);

        for (var i = 0; i < graphics.swapChainImages!.Length; i++)
        {
            vk.DestroyBuffer(graphics.device, graphics.uniformBuffers![i], null);
            vk.FreeMemory(graphics.device, graphics.uniformBuffersMemory![i], null);
        }
    }
}