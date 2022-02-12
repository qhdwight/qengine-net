using System;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Game.Graphic.Vulkan;

internal static unsafe partial class VulkanGraphics
{
    public static void CreateCompute(ref VkGraphics graphics)
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

        fixed (DescriptorSetLayout* descSetLayoutPtr = &graphics.compDescSetLayout)
            if (graphics.vk!.CreateDescriptorSetLayout(graphics.device, layoutInfo, null, descSetLayoutPtr) != Result.Success)
                throw new Exception("Failed to create compute descriptor set layout!");

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
        {
            PipelineLayoutCreateInfo pipelineLayoutCreateInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                PushConstantRangeCount = 0,
                SetLayoutCount = 1,
                PSetLayouts = descSetLayoutPtr
            };

            if (graphics.vk!.CreatePipelineLayout(graphics.device, pipelineLayoutCreateInfo, null, out graphics.compPipelineLayout) != Result.Success)
                throw new Exception("Failed to create compute pipeline layout!");

            ComputePipelineCreateInfo computePipelineCreateInfo = new()
            {
                SType = StructureType.ComputePipelineCreateInfo,
                Stage = pipelineStageCreateInfo,
                Layout = graphics.compPipelineLayout
            };

            fixed (Pipeline* computePipeline = &graphics.compPipeline)
                graphics.vk!.CreateComputePipelines(graphics.device, default, 1, &computePipelineCreateInfo, null, computePipeline);
        }

        graphics.vk!.DestroyShaderModule(graphics.device, compShaderModule, null);
    }
}