using System;
using System.Collections.Generic;
using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace Game.Graphic;

internal static unsafe partial class VulkanGraphics
{
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
}