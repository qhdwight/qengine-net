using System;
using System.Runtime.CompilerServices;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Game.Graphic.Vulkan;

internal static unsafe partial class VulkanGraphics
{
    public static float[] Compute(ref VkGraphics graphics)
    {
        Vk vk = graphics.vk!;
        fixed (CommandBuffer* cmdBufPtr = &graphics.compCmdBuf)
        fixed (DescriptorSet* descSetLayout = &graphics.compDescSet)
        {
            CommandBufferBeginInfo beginInfo = new(StructureType.CommandBufferBeginInfo);
            vk.BeginCommandBuffer(graphics.compCmdBuf, beginInfo);
            vk.CmdBindPipeline(graphics.compCmdBuf, PipelineBindPoint.Compute, graphics.compPipeline);
            vk.CmdBindDescriptorSets(graphics.compCmdBuf, PipelineBindPoint.Compute,
                                     graphics.compPipelineLayout, 0, 1, descSetLayout,
                                     0, null);
            const uint groupCountX = 32 * 32 / 256 + 1;
            vk.CmdDispatch(graphics.compCmdBuf, groupCountX, 1, 1);
            vk.EndCommandBuffer(graphics.compCmdBuf);
            SubmitInfo submitInfo = new(commandBufferCount: 1, pCommandBuffers: cmdBufPtr);
            vk.QueueSubmit(graphics.graphicsQueue, 1, submitInfo, default);
            vk.QueueWaitIdle(graphics.graphicsQueue);

            var bufSize = (ulong)(Unsafe.SizeOf<float>() * 32 * 32);
            var points = new float[32 * 32];
            void* data;
            graphics.vk!.MapMemory(graphics.device, graphics.compBufMemOut, 0, bufSize, 0, &data);
            new Span<float>(data, points.Length).CopyTo(points.AsSpan());
            graphics.vk!.UnmapMemory(graphics.device, graphics.compBufMemOut);
            return points;
        }
    }

    private static void CreateCompBuffers(ref VkGraphics graphics)
    {
        var outBufSize = (ulong)(Unsafe.SizeOf<float>() * 32 * 32);
        CreateBuffer(ref graphics, outBufSize,
                     BufferUsageFlags.BufferUsageTransferDstBit | BufferUsageFlags.BufferUsageStorageBufferBit,
                     MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyDeviceLocalBit,
                     ref graphics.compBufOut, ref graphics.compBufMemOut);

        var inBufSize = (ulong)(Unsafe.SizeOf<Vector2D<float>>() * 32 * 32);
        Buffer stagingBuf = default;
        DeviceMemory stagingBufMem = default;
        CreateBuffer(ref graphics, inBufSize,
                     BufferUsageFlags.BufferUsageTransferSrcBit,
                     MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit,
                     ref stagingBuf, ref stagingBufMem);
        void* data;
        graphics.vk!.MapMemory(graphics.device, stagingBufMem, 0, inBufSize, 0, &data);
        var points = new Vector2D<float>[32 * 32];
        for (var x = 0; x < 32; x++)
        for (var y = 0; y < 32; y++)
            points[x + y * 32] = new Vector2D<float>(x / 16.0f, y / 16.0f);
        points.AsSpan().CopyTo(new Span<Vector2D<float>>(data, points.Length));
        graphics.vk!.UnmapMemory(graphics.device, stagingBufMem);
        CreateBuffer(ref graphics, inBufSize,
                     BufferUsageFlags.BufferUsageTransferDstBit | BufferUsageFlags.BufferUsageStorageBufferBit,
                     MemoryPropertyFlags.MemoryPropertyDeviceLocalBit,
                     ref graphics.compBufIn, ref graphics.compBufMemIn);
        CopyBuffer(ref graphics, stagingBuf, graphics.compBufIn, inBufSize);
        graphics.vk!.DestroyBuffer(graphics.device, stagingBuf, null);
        graphics.vk!.FreeMemory(graphics.device, stagingBufMem, null);
    }

    private static void CreateCompCmdBuffers(ref VkGraphics graphics)
    {
        QueueFamilyIndices queueFamilyIndices = FindQueueFamilies(ref graphics, graphics.physicalDevice);
        CommandPoolCreateInfo poolInfo = new(flags: CommandPoolCreateFlags.CommandPoolCreateResetCommandBufferBit,
                                             queueFamilyIndex: queueFamilyIndices.ComputeFamily!.Value);
        Check(graphics.vk!.CreateCommandPool(graphics.device, poolInfo, null, out graphics.compCmdPool),
              "Failed to create compute command pool!");
        CommandBufferAllocateInfo allocInfo = new(commandPool: graphics.compCmdPool, level: CommandBufferLevel.Primary, commandBufferCount: 1);
        fixed (CommandBuffer* cmdBufPtr = &graphics.compCmdBuf)
            Check(graphics.vk!.AllocateCommandBuffers(graphics.device, allocInfo, cmdBufPtr),
                  "Failed to allocate compute command buffers!");
    }

    private static void CreateCompDescSet(ref VkGraphics graphics)
    {
        DescriptorSetLayoutBinding* layoutBindings = stackalloc DescriptorSetLayoutBinding[]
        {
            new(0, DescriptorType.StorageBuffer, 1, ShaderStageFlags.ShaderStageComputeBit),
            new(1, DescriptorType.StorageBuffer, 1, ShaderStageFlags.ShaderStageComputeBit)
        };

        DescriptorSetLayoutCreateInfo layoutInfo = new(bindingCount: 2, pBindings: layoutBindings);

        fixed (DescriptorSet* descriptorSetsPtr = &graphics.compDescSet)
        fixed (DescriptorSetLayout* descSetLayoutPtr = &graphics.compDescSetLayout)
        {
            Check(graphics.vk!.CreateDescriptorSetLayout(graphics.device, layoutInfo, null, descSetLayoutPtr),
                  "Failed to create compute descriptor set layout!");

            DescriptorSetAllocateInfo allocInfo = new(descriptorPool: graphics.descPool, descriptorSetCount: 1, pSetLayouts: descSetLayoutPtr);
            Check(graphics.vk!.AllocateDescriptorSets(graphics.device, allocInfo, descriptorSetsPtr),
                  "Failed to allocate compute descriptor sets!");

            DescriptorBufferInfo inBufInfo = new(graphics.compBufIn, 0, 32 * 32 * (uint)Unsafe.SizeOf<Vector2D<float>>());
            WriteDescriptorSet inWriteDescSet = new(dstSet: graphics.compDescSet,
                                                    dstBinding: 0,
                                                    descriptorType: DescriptorType.StorageBuffer,
                                                    descriptorCount: 1,
                                                    pBufferInfo: &inBufInfo);
            DescriptorBufferInfo outBufInfo = new(graphics.compBufOut, 0, 32 * 32 * (uint)Unsafe.SizeOf<float>());
            WriteDescriptorSet outWriteDescSet = new(dstSet: graphics.compDescSet,
                                                     dstBinding: 1,
                                                     descriptorType: DescriptorType.StorageBuffer,
                                                     descriptorCount: 1,
                                                     pBufferInfo: &outBufInfo);
            WriteDescriptorSet* writeDescSets = stackalloc WriteDescriptorSet[] { inWriteDescSet, outWriteDescSet };
            graphics.vk!.UpdateDescriptorSets(graphics.device, 2, writeDescSets, 0, null);
        }
    }

    public static void CreateCompute(ref VkGraphics graphics)
    {
        CreateCompCmdBuffers(ref graphics);
        CreateCompBuffers(ref graphics);
        CreateCompDescSet(ref graphics);

        byte[] simplexCode = Resources.SimplexComp;
        ShaderModule compShaderModule = CreateShaderModule(ref graphics, simplexCode);
        PipelineShaderStageCreateInfo pipelineStageCreateInfo = new(stage: ShaderStageFlags.ShaderStageComputeBit,
                                                                    module: compShaderModule,
                                                                    pName: (byte*)SilkMarshal.StringToPtr("main"));

        fixed (DescriptorSetLayout* descSetLayoutPtr = &graphics.compDescSetLayout)
        fixed (Pipeline* computePipeline = &graphics.compPipeline)
        {
            PipelineLayoutCreateInfo pipelineLayoutCreateInfo = new(setLayoutCount: 1, pSetLayouts: descSetLayoutPtr);
            Check(graphics.vk!.CreatePipelineLayout(graphics.device, pipelineLayoutCreateInfo, null, out graphics.compPipelineLayout),
                  "Failed to create compute pipeline layout!");
            ComputePipelineCreateInfo computePipelineCreateInfo = new(stage: pipelineStageCreateInfo, layout: graphics.compPipelineLayout);
            graphics.vk!.CreateComputePipelines(graphics.device, default, 1, &computePipelineCreateInfo, null, computePipeline);
        }

        graphics.vk!.DestroyShaderModule(graphics.device, compShaderModule, null);
    }
}