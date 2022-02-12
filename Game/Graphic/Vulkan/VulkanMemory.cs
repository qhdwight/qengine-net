using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Game.Graphic.Vulkan;

internal static unsafe partial class VulkanGraphics
{
    internal struct UniformBufferObject
    {
        public Matrix4X4<float> model;
        public Matrix4X4<float> view;
        public Matrix4X4<float> proj;
    }

    private static void CreateVertexBuffer(ref VkGraphics graphics, ref VkMesh vkMesh, in Mesh mesh)
    {
        var bufferSize = (ulong)(Unsafe.SizeOf<Vertex>() * mesh.vertices.Count);

        Buffer stagingBuffer = default;
        DeviceMemory stagingBufferMemory = default;
        CreateBuffer(ref graphics, bufferSize, BufferUsageFlags.BufferUsageTransferSrcBit,
                     MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit,
                     ref stagingBuffer, ref stagingBufferMemory);

        void* data;
        graphics.vk!.MapMemory(graphics.device, stagingBufferMemory, 0, bufferSize, 0, &data);
        CollectionsMarshal.AsSpan(mesh.vertices).CopyTo(new Span<Vertex>(data, mesh.vertices.Count));
        graphics.vk!.UnmapMemory(graphics.device, stagingBufferMemory);

        CreateBuffer(ref graphics, bufferSize,
                     BufferUsageFlags.BufferUsageTransferDstBit | BufferUsageFlags.BufferUsageVertexBufferBit,
                     MemoryPropertyFlags.MemoryPropertyDeviceLocalBit,
                     ref vkMesh.vertexBuffer, ref vkMesh.vertexBufferMemory);

        CopyBuffer(ref graphics, stagingBuffer, vkMesh.vertexBuffer, bufferSize);

        graphics.vk!.DestroyBuffer(graphics.device, stagingBuffer, null);
        graphics.vk!.FreeMemory(graphics.device, stagingBufferMemory, null);

        vkMesh.vertexBufferSize = (uint)mesh.vertices.Count;
    }

    private static void CreateIndexBuffer(ref VkGraphics graphics, ref VkMesh vkMesh, in Mesh mesh)
    {
        var bufferSize = (ulong)(Unsafe.SizeOf<uint>() * mesh.indices.Count);

        Buffer stagingBuffer = default;
        DeviceMemory stagingBufMem = default;
        CreateBuffer(ref graphics, bufferSize,
                     BufferUsageFlags.BufferUsageTransferSrcBit,
                     MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit,
                     ref stagingBuffer, ref stagingBufMem);

        void* data;
        graphics.vk!.MapMemory(graphics.device, stagingBufMem, 0, bufferSize, 0, &data);

        CollectionsMarshal.AsSpan(mesh.indices).CopyTo(new Span<uint>(data, mesh.indices.Count));
        graphics.vk!.UnmapMemory(graphics.device, stagingBufMem);

        CreateBuffer(ref graphics, bufferSize,
                     BufferUsageFlags.BufferUsageTransferDstBit | BufferUsageFlags.BufferUsageIndexBufferBit,
                     MemoryPropertyFlags.MemoryPropertyDeviceLocalBit,
                     ref vkMesh.indexBuffer, ref vkMesh.indexBufferMemory);

        CopyBuffer(ref graphics, stagingBuffer, vkMesh.indexBuffer, bufferSize);

        graphics.vk!.DestroyBuffer(graphics.device, stagingBuffer, null);
        graphics.vk!.FreeMemory(graphics.device, stagingBufMem, null);

        vkMesh.indexBufferSize = (uint)mesh.indices.Count;
    }

    private static void CreateBuffer(ref VkGraphics graphics, ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties, ref Buffer buffer, ref DeviceMemory bufferMemory)
    {
        BufferCreateInfo bufferInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
        };

        fixed (Buffer* bufferPtr = &buffer)
            if (graphics.vk!.CreateBuffer(graphics.device, bufferInfo, null, bufferPtr) != Result.Success)
                throw new Exception("Failed to create vertex buffer!");

        graphics.vk!.GetBufferMemoryRequirements(graphics.device, buffer, out MemoryRequirements memRequirements);

        MemoryAllocateInfo allocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(ref graphics, memRequirements.MemoryTypeBits, properties),
        };

        fixed (DeviceMemory* bufferMemoryPtr = &bufferMemory)
            if (graphics.vk!.AllocateMemory(graphics.device, allocateInfo, null, bufferMemoryPtr) != Result.Success)
                throw new Exception("Failed to allocate vertex buffer memory!");

        graphics.vk!.BindBufferMemory(graphics.device, buffer, bufferMemory, 0);
    }

    private static void CopyBuffer(ref VkGraphics graphics, Buffer srcBuffer, Buffer dstBuffer, ulong size)
    {
        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            Level = CommandBufferLevel.Primary,
            CommandPool = graphics.commandPool,
            CommandBufferCount = 1,
        };

        graphics.vk!.AllocateCommandBuffers(graphics.device, allocateInfo, out CommandBuffer commandBuffer);

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.CommandBufferUsageOneTimeSubmitBit,
        };

        graphics.vk!.BeginCommandBuffer(commandBuffer, beginInfo);

        BufferCopy copyRegion = new()
        {
            Size = size,
        };

        graphics.vk!.CmdCopyBuffer(commandBuffer, srcBuffer, dstBuffer, 1, copyRegion);

        graphics.vk!.EndCommandBuffer(commandBuffer);

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
        };

        graphics.vk!.QueueSubmit(graphics.graphicsQueue, 1, submitInfo, default);
        graphics.vk!.QueueWaitIdle(graphics.graphicsQueue);

        graphics.vk!.FreeCommandBuffers(graphics.device, graphics.commandPool, 1, commandBuffer);
    }

    private static void UpdateUniformBuffer(ref VkGraphics graphics, in DrawInfo drawInfo, uint currentImage)
    {
        float ratio = (float)graphics.swapChainExtent.Width / graphics.swapChainExtent.Height;
        UniformBufferObject ubo = new()
        {
            // model = Matrix4X4.CreateFromAxisAngle(new Vector3D<float>(0, 0, 1), time * Scalar.DegreesToRadians(90.0f)),
            model = Matrix4X4<float>.Identity,
            view = Matrix4X4.CreateLookAt((Vector3D<float>) drawInfo.Position,
                                          (Vector3D<float>)(drawInfo.Position + drawInfo.Forward),
                                          new Vector3D<float>{Z = 1.0f}),
            proj = Matrix4X4.CreatePerspectiveFieldOfView(Scalar.DegreesToRadians(45.0f), ratio, 0.1f, 256.0f),
        };
        ubo.proj.M22 *= -1;

        void* data;
        graphics.vk!.MapMemory(graphics.device, graphics.uniformBuffersMemory![currentImage], 0, (ulong)Unsafe.SizeOf<UniformBufferObject>(), 0, &data);
        new Span<UniformBufferObject>(data, 1)[0] = ubo;
        graphics.vk!.UnmapMemory(graphics.device, graphics.uniformBuffersMemory![currentImage]);
    }

    private static uint FindMemoryType(ref VkGraphics graphics, uint typeFilter, MemoryPropertyFlags properties)
    {
        graphics.vk!.GetPhysicalDeviceMemoryProperties(graphics.physicalDevice, out PhysicalDeviceMemoryProperties memProperties);

        for (var i = 0; i < memProperties.MemoryTypeCount; i++)
            if ((typeFilter & (1 << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
                return (uint)i;

        throw new Exception("Failed to find suitable memory type!");
    }
}