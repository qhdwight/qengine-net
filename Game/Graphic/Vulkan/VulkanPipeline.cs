using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Game.Graphic.Vulkan;

internal static unsafe partial class VulkanGraphics
{
    private static void CreateRenderPass(ref VkGraphics graphics)
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

        AttachmentDescription depthAttachment = new()
        {
            Format = FindDepthFormat(ref graphics),
            Samples = SampleCountFlags.SampleCount1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
        };

        AttachmentReference colorAttachmentRef = new()
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal
        };

        AttachmentReference depthAttachmentRef = new()
        {
            Attachment = 1,
            Layout = ImageLayout.DepthStencilAttachmentOptimal
        };

        SubpassDescription subpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef,
            PDepthStencilAttachment = &depthAttachmentRef
        };

        SubpassDependency dependency = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.PipelineStageColorAttachmentOutputBit | PipelineStageFlags.PipelineStageEarlyFragmentTestsBit,
            SrcAccessMask = 0,
            DstStageMask = PipelineStageFlags.PipelineStageColorAttachmentOutputBit | PipelineStageFlags.PipelineStageEarlyFragmentTestsBit,
            DstAccessMask = AccessFlags.AccessColorAttachmentWriteBit | AccessFlags.AccessDepthStencilAttachmentWriteBit
        };

        AttachmentDescription* attachments = stackalloc AttachmentDescription[] { colorAttachment, depthAttachment };
        RenderPassCreateInfo renderPassInfo = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 2,
            PAttachments = attachments,
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 1,
            PDependencies = &dependency
        };

        if (graphics.vk!.CreateRenderPass(graphics.device, renderPassInfo, null, out graphics.renderPass) != Result.Success)
            throw new Exception("Failed to create render pass!");
    }

    private static void CreateGraphicsPipeline(ref VkGraphics graphics)
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

        VertexInputBindingDescription bindingDescription = Vertex.GetBindingDescription();
        VertexInputAttributeDescription[] attributeDescriptions = Vertex.GetAttributeDescriptions();

        fixed (VertexInputAttributeDescription* attributeDescriptionsPtr = attributeDescriptions)
        fixed (DescriptorSetLayout* descriptorSetLayoutPtr = &graphics.descriptorSetLayout)
        {
            PipelineVertexInputStateCreateInfo vertexInputInfo = new()
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                VertexAttributeDescriptionCount = (uint)attributeDescriptions.Length,
                PVertexBindingDescriptions = &bindingDescription,
                PVertexAttributeDescriptions = attributeDescriptionsPtr
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
                PolygonMode = PolygonMode.Line,
                LineWidth = 1,
                CullMode = CullModeFlags.CullModeBackBit,
                FrontFace = FrontFace.CounterClockwise,
                DepthBiasEnable = false
            };

            PipelineMultisampleStateCreateInfo multisampling = new()
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                SampleShadingEnable = false,
                RasterizationSamples = SampleCountFlags.SampleCount1Bit
            };

            PipelineDepthStencilStateCreateInfo depthStencil = new()
            {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = true,
                DepthWriteEnable = true,
                DepthCompareOp = CompareOp.Less,
                DepthBoundsTestEnable = false,
                StencilTestEnable = false
            };

            PipelineColorBlendAttachmentState colorBlendAttachment = new()
            {
                ColorWriteMask = ColorComponentFlags.ColorComponentRBit
                               | ColorComponentFlags.ColorComponentGBit
                               | ColorComponentFlags.ColorComponentBBit
                               | ColorComponentFlags.ColorComponentABit,
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
                PushConstantRangeCount = 0,
                SetLayoutCount = 1,
                PSetLayouts = descriptorSetLayoutPtr
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
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlending,
                Layout = graphics.pipelineLayout,
                RenderPass = graphics.renderPass,
                Subpass = 0,
                BasePipelineHandle = default
            };

            if (graphics.vk!.CreateGraphicsPipelines(graphics.device, default, 1, pipelineInfo, null, out graphics.graphicsPipeline) != Result.Success)
                throw new Exception("Failed to create graphics pipeline!");
        }

        graphics.vk!.DestroyShaderModule(graphics.device, fragShaderModule, null);
        graphics.vk!.DestroyShaderModule(graphics.device, vertShaderModule, null);

        SilkMarshal.Free((nint)vertShaderStageInfo.PName);
        SilkMarshal.Free((nint)fragShaderStageInfo.PName);
    }

    private static void CreateFramebuffers(ref VkGraphics graphics)
    {
        graphics.swapChainFramebuffers = new Framebuffer[graphics.swapChainImageViews!.Length];

        ImageView* attachments = stackalloc ImageView[] { default, graphics.depthImageView };
        for (var i = 0; i < graphics.swapChainImageViews.Length; i++)
        {
            attachments[0] = graphics.swapChainImageViews[i];
            var framebufferInfo = new FramebufferCreateInfo
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = graphics.renderPass,
                AttachmentCount = 2,
                PAttachments = attachments,
                Width = graphics.swapChainExtent.Width,
                Height = graphics.swapChainExtent.Height,
                Layers = 1
            };

            var framebuffer = new Framebuffer();
            if (graphics.vk!.CreateFramebuffer(graphics.device, &framebufferInfo, null, &framebuffer) != Result.Success)
                throw new Exception("Failed to create framebuffer!");

            graphics.swapChainFramebuffers[i] = framebuffer;
        }
    }

    private static void CreateCommandPool(ref VkGraphics graphics)
    {
        QueueFamilyIndices queueFamilyIndices = FindQueueFamilies(ref graphics, graphics.physicalDevice);

        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.CommandPoolCreateResetCommandBufferBit,
            QueueFamilyIndex = queueFamilyIndices.GraphicsFamily!.Value
        };

        if (graphics.vk!.CreateCommandPool(graphics.device, poolInfo, null, out graphics.cmdPool) != Result.Success)
            throw new Exception("Failed to create command pool!");
    }

    private static Format FindDepthFormat(ref VkGraphics graphics)
        => FindSupportedFormat(ref graphics, new[] { Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint }, ImageTiling.Optimal,
                               FormatFeatureFlags.FormatFeatureDepthStencilAttachmentBit);

    private static Format FindSupportedFormat(ref VkGraphics graphics, IEnumerable<Format> candidates, ImageTiling tiling, FormatFeatureFlags features)
    {
        foreach (Format format in candidates)
        {
            graphics.vk!.GetPhysicalDeviceFormatProperties(graphics.physicalDevice, format, out FormatProperties props);
            switch (tiling)
            {
                case ImageTiling.Linear when (props.LinearTilingFeatures & features) == features:
                    return format;
                case ImageTiling.Optimal when (props.OptimalTilingFeatures & features) == features:
                    return format;
            }
        }
        throw new Exception("Failed to find supported format!");
    }

    private static void CreateDepthResources(ref VkGraphics graphics)
    {
        Format depthFormat = FindDepthFormat(ref graphics);

        CreateImage(ref graphics, graphics.swapChainExtent.Width, graphics.swapChainExtent.Height, depthFormat, ImageTiling.Optimal,
                    ImageUsageFlags.ImageUsageDepthStencilAttachmentBit, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit, ref graphics.depthImage, ref graphics.depthImageMemory);
        graphics.depthImageView = CreateImageView(ref graphics, graphics.depthImage, depthFormat, ImageAspectFlags.ImageAspectDepthBit);
    }

    private static void CreateCommandBuffers(ref VkGraphics graphics)
    {
        graphics.commandBuffers = new CommandBuffer[graphics.swapChainFramebuffers!.Length];

        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = graphics.cmdPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = (uint)graphics.commandBuffers.Length
        };

        fixed (CommandBuffer* commandBuffersPtr = graphics.commandBuffers)
            if (graphics.vk!.AllocateCommandBuffers(graphics.device, allocInfo, commandBuffersPtr) != Result.Success)
                throw new Exception("Failed to allocate command buffers!");
    }

    private static void CreateUniformBuffers(ref VkGraphics graphics)
    {
        var bufferSize = (ulong)Unsafe.SizeOf<UniformBufferObject>();

        graphics.uniformBuffers = new Buffer[graphics.swapChainImages!.Length];
        graphics.uniformBuffersMemory = new DeviceMemory[graphics.swapChainImages!.Length];

        for (var i = 0; i < graphics.swapChainImages!.Length; i++)
            CreateBuffer(ref graphics, bufferSize, BufferUsageFlags.BufferUsageUniformBufferBit,
                         MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit,
                         ref graphics.uniformBuffers![i], ref graphics.uniformBuffersMemory![i]);
    }

    private static void CreateDescriptorSetLayout(ref VkGraphics graphics)
    {
        DescriptorSetLayoutBinding uboLayoutBinding = new()
        {
            Binding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.UniformBuffer,
            PImmutableSamplers = null,
            StageFlags = ShaderStageFlags.ShaderStageVertexBit
        };

        DescriptorSetLayoutCreateInfo layoutInfo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &uboLayoutBinding
        };

        fixed (DescriptorSetLayout* descriptorSetLayoutPtr = &graphics.descriptorSetLayout)
            if (graphics.vk!.CreateDescriptorSetLayout(graphics.device, layoutInfo, null, descriptorSetLayoutPtr) != Result.Success)
                throw new Exception("Failed to create descriptor set layout!");
    }

    private static void CreateDescriptorPool(ref VkGraphics graphics)
    {
        DescriptorPoolSize uniformPoolSize = new()
        {
            Type = DescriptorType.UniformBuffer,
            DescriptorCount = (uint)graphics.swapChainImages!.Length
        };

        DescriptorPoolSize storagePoolSize = new()
        {
            Type = DescriptorType.StorageBuffer,
            DescriptorCount = 2
        };

        DescriptorPoolSize* poolSizes = stackalloc DescriptorPoolSize[] { uniformPoolSize, storagePoolSize };

        DescriptorPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = 2,
            PPoolSizes = poolSizes,
            MaxSets = (uint)graphics.swapChainImages!.Length + 1
        };

        fixed (DescriptorPool* descriptorPoolPtr = &graphics.descPool)
            if (graphics.vk!.CreateDescriptorPool(graphics.device, poolInfo, null, descriptorPoolPtr) != Result.Success)
                throw new Exception("Failed to create descriptor pool!");
    }

    private static void CreateDescriptorSets(ref VkGraphics graphics)
    {
        var layouts = new DescriptorSetLayout[graphics.swapChainImages!.Length];
        Array.Fill(layouts, graphics.descriptorSetLayout);

        fixed (DescriptorSetLayout* layoutsPtr = layouts)
        {
            DescriptorSetAllocateInfo allocateInfo = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = graphics.descPool,
                DescriptorSetCount = (uint)graphics.swapChainImages!.Length,
                PSetLayouts = layoutsPtr
            };

            graphics.descriptorSets = new DescriptorSet[graphics.swapChainImages.Length];
            fixed (DescriptorSet* descriptorSetsPtr = graphics.descriptorSets)
                if (graphics.vk!.AllocateDescriptorSets(graphics.device, allocateInfo, descriptorSetsPtr) != Result.Success)
                    throw new Exception("Failed to allocate descriptor sets!");
        }

        for (var i = 0; i < graphics.swapChainImages.Length; i++)
        {
            DescriptorBufferInfo bufferInfo = new()
            {
                Buffer = graphics.uniformBuffers![i],
                Offset = 0,
                Range = (ulong)Unsafe.SizeOf<UniformBufferObject>()
            };

            WriteDescriptorSet descriptorWrite = new(dstSet: graphics.descriptorSets[i],
                                                     dstBinding: 0,
                                                     dstArrayElement: 0,
                                                     descriptorType: DescriptorType.UniformBuffer,
                                                     descriptorCount: 1,
                                                     pBufferInfo: &bufferInfo);

            graphics.vk!.UpdateDescriptorSets(graphics.device, 1, descriptorWrite, 0, null);
        }
    }
}