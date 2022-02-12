using System;
using System.Runtime.CompilerServices;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace Game.Graphic.Vulkan;

internal static unsafe partial class VulkanGraphics
{
    private static void CreateCompCmdBuffers(ref VkGraphics graphics)
    {
        QueueFamilyIndices queueFamilyIndices = FindQueueFamilies(ref graphics, graphics.physicalDevice);

        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.CommandPoolCreateResetCommandBufferBit,
            QueueFamilyIndex = queueFamilyIndices.ComputeFamily!.Value
        };

        Check(graphics.vk!.CreateCommandPool(graphics.device, poolInfo, null, out graphics.compCmdPool),
              "Failed to create compute command pool!");

        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = graphics.compCmdPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };

        fixed (CommandBuffer* cmdBufPtr = &graphics.compCmdBuf)
            Check(graphics.vk!.AllocateCommandBuffers(graphics.device, allocInfo, cmdBufPtr),
                  "Failed to allocate compute command buffers!");
    }

    private static void CreateCompDescSet(ref VkGraphics graphics)
    {
        DescriptorSetLayoutBinding* layoutBindings = stackalloc DescriptorSetLayoutBinding[]
        {
            new()
            {
                Binding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                StageFlags = ShaderStageFlags.ShaderStageComputeBit
            },
            new()
            {
                Binding = 1,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                StageFlags = ShaderStageFlags.ShaderStageComputeBit
            }
        };

        DescriptorSetLayoutCreateInfo layoutInfo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 2,
            PBindings = layoutBindings
        };

        fixed (DescriptorSet* descriptorSetsPtr = &graphics.compDescSet)
        fixed (DescriptorSetLayout* descSetLayoutPtr = &graphics.compDescSetLayout)
        {
            Check(graphics.vk!.CreateDescriptorSetLayout(graphics.device, layoutInfo, null, descSetLayoutPtr),
                  "Failed to create compute descriptor set layout!");

            DescriptorSetAllocateInfo allocInfo = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = graphics.descriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = descSetLayoutPtr
            };

            Check(graphics.vk!.AllocateDescriptorSets(graphics.device, allocInfo, descriptorSetsPtr),
                  "Failed to allocate compute descriptor sets!");

            DescriptorBufferInfo bufferInfo = new()
            {
                Buffer = graphics.compBuf,
                Offset = 0,
                Range = (ulong)Unsafe.SizeOf<UniformBufferObject>()
            };

            WriteDescriptorSet descriptorWrite = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = graphics.compDescSet,
                DstBinding = 0,
                DstArrayElement = 0,
                DescriptorType = DescriptorType.StorageBuffer,
                DescriptorCount = 1,
                PBufferInfo = &bufferInfo
            };

            graphics.vk!.UpdateDescriptorSets(graphics.device, 1, descriptorWrite, 0, null);
        }
    }

    public static void Compute(ref VkGraphics graphics)
    {
        fixed (DescriptorSet* descSetLayout = &graphics.compDescSet)
        {
            graphics.vk!.CmdBindPipeline(graphics.compCmdBuf, PipelineBindPoint.Compute, graphics.compPipeline);
            graphics.vk.CmdBindDescriptorSets(graphics.compCmdBuf, PipelineBindPoint.Compute,
                                              graphics.pipelineLayout, 0, 1, descSetLayout,
                                              0, null);
            graphics.vk.CmdDispatch(graphics.compCmdBuf, 0, 0, 0);
        }
    }

    public static void CreateCompute(ref VkGraphics graphics)
    {
        CreateCompCmdBuffers(ref graphics);
        CreateCompDescSet(ref graphics);

        uint size = (uint)Unsafe.SizeOf<Vector2D<float>>() * 32 * 32;
        CreateBuffer(ref graphics, size, BufferUsageFlags.BufferUsageStorageBufferBit,
                     MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit,
                     ref graphics.compBuf, ref graphics.compBufMem);

        byte[] simplexCode = Resources.SimplexComp;

        ShaderModule compShaderModule = CreateShaderModule(ref graphics, simplexCode);

        PipelineShaderStageCreateInfo pipelineStageCreateInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.ShaderStageComputeBit,
            Module = compShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };

        fixed (DescriptorSetLayout* descSetLayoutPtr = &graphics.compDescSetLayout)
        fixed (Pipeline* computePipeline = &graphics.compPipeline)
        {
            PipelineLayoutCreateInfo pipelineLayoutCreateInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                PushConstantRangeCount = 0,
                SetLayoutCount = 1,
                PSetLayouts = descSetLayoutPtr
            };

            Check(graphics.vk!.CreatePipelineLayout(graphics.device, pipelineLayoutCreateInfo, null, out graphics.compPipelineLayout),
                  "Failed to create compute pipeline layout!");

            ComputePipelineCreateInfo computePipelineCreateInfo = new()
            {
                SType = StructureType.ComputePipelineCreateInfo,
                Stage = pipelineStageCreateInfo,
                Layout = graphics.compPipelineLayout
            };

            graphics.vk!.CreateComputePipelines(graphics.device, default, 1, &computePipelineCreateInfo, null, computePipeline);
        }

        graphics.vk!.DestroyShaderModule(graphics.device, compShaderModule, null);
    }
}