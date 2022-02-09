using System;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Game.Graphic;

internal static unsafe partial class VulkanGraphics
{
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
            if (graphics.vk!.AllocateCommandBuffers(graphics.device, allocInfo, commandBuffersPtr) != Result.Success)
                throw new Exception("Failed to allocate command buffers!");

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
                throw new Exception("Failed to record command buffer!");
        }
    }
}