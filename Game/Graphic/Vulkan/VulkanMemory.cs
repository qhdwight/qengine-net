using System;
using System.Runtime.CompilerServices;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Game.Graphic.Vulkan;

internal static unsafe partial class VulkanGraphics
{
    private static readonly Vertex[] Vertices =
    {
        new() { pos = new Vector2D<float>(-0.5f, -0.5f), color = new Vector3D<float>(1.0f, 0.0f, 0.0f) },
        new() { pos = new Vector2D<float>(0.5f, -0.5f), color = new Vector3D<float>(0.0f, 1.0f, 0.0f) },
        new() { pos = new Vector2D<float>(0.5f, 0.5f), color = new Vector3D<float>(0.0f, 0.0f, 1.0f) },
        new() { pos = new Vector2D<float>(-0.5f, 0.5f), color = new Vector3D<float>(1.0f, 1.0f, 1.0f) },
    };

    private static readonly ushort[] Indices =
    {
        0, 1, 2, 2, 3, 0
    };

    private static void CreateVertexBuffer(ref Graphics graphics)
    {
        var bufferSize = (ulong)(Unsafe.SizeOf<Vertex>() * Vertices.Length);

        Buffer stagingBuffer = default;
        DeviceMemory stagingBufferMemory = default;
        CreateBuffer(ref graphics, bufferSize, BufferUsageFlags.BufferUsageTransferSrcBit,
                     MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit,
                     ref stagingBuffer, ref stagingBufferMemory);

        void* data;
        graphics.vk!.MapMemory(graphics.device, stagingBufferMemory, 0, bufferSize, 0, &data);
        Vertices.AsSpan().CopyTo(new Span<Vertex>(data, Vertices.Length));
        graphics.vk!.UnmapMemory(graphics.device, stagingBufferMemory);

        CreateBuffer(ref graphics, bufferSize, BufferUsageFlags.BufferUsageTransferDstBit | BufferUsageFlags.BufferUsageVertexBufferBit,
                     MemoryPropertyFlags.MemoryPropertyDeviceLocalBit,
                     ref graphics.vertexBuffer, ref graphics.vertexBufferMemory);

        CopyBuffer(ref graphics, stagingBuffer, graphics.vertexBuffer, bufferSize);

        graphics.vk!.DestroyBuffer(graphics.device, stagingBuffer, null);
        graphics.vk!.FreeMemory(graphics.device, stagingBufferMemory, null);
    }

    private static void CreateIndexBuffer(ref Graphics graphics)
    {
        var bufferSize = (ulong)(Unsafe.SizeOf<ushort>() * Indices.Length);

        Buffer stagingBuffer = default;
        DeviceMemory stagingBufferMemory = default;
        CreateBuffer(ref graphics, bufferSize, BufferUsageFlags.BufferUsageTransferSrcBit,
                     MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit,
                     ref stagingBuffer, ref stagingBufferMemory);

        void* data;
        graphics.vk!.MapMemory(graphics.device, stagingBufferMemory, 0, bufferSize, 0, &data);
        Indices.AsSpan().CopyTo(new Span<ushort>(data, Indices.Length));
        graphics.vk!.UnmapMemory(graphics.device, stagingBufferMemory);

        CreateBuffer(ref graphics, bufferSize, BufferUsageFlags.BufferUsageTransferDstBit | BufferUsageFlags.BufferUsageIndexBufferBit,
                     MemoryPropertyFlags.MemoryPropertyDeviceLocalBit,
                     ref graphics.indexBuffer, ref graphics.indexBufferMemory);

        CopyBuffer(ref graphics, stagingBuffer, graphics.indexBuffer, bufferSize);

        graphics.vk!.DestroyBuffer(graphics.device, stagingBuffer, null);
        graphics.vk!.FreeMemory(graphics.device, stagingBufferMemory, null);
    }

    private static void CreateBuffer(ref Graphics graphics, ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties, ref Buffer buffer, ref DeviceMemory bufferMemory)
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

    private static void CopyBuffer(ref Graphics graphics, Buffer srcBuffer, Buffer dstBuffer, ulong size)
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

    private static void UpdateUniformBuffer(ref Graphics graphics, uint currentImage)
    {
        var time = (float)graphics.window!.Time;

        float ratio = (float)graphics.swapChainExtent.Width / graphics.swapChainExtent.Height;
        UniformBufferObject ubo = new()
        {
            model = Matrix4X4<float>.Identity * Matrix4X4.CreateFromAxisAngle(new Vector3D<float>(0, 0, 1), time * Scalar.DegreesToRadians(90.0f)),
            view = Matrix4X4.CreateLookAt(new Vector3D<float>(2, 2, 2),
                                          new Vector3D<float>(0, 0, 0),
                                          new Vector3D<float>(0, 0, 1)),
            proj = Matrix4X4.CreatePerspectiveFieldOfView(Scalar.DegreesToRadians(45.0f), ratio, 0.1f, 10.0f),
        };
        ubo.proj.M22 *= -1;

        void* data;
        graphics.vk!.MapMemory(graphics.device, graphics.uniformBuffersMemory![currentImage], 0, (ulong)Unsafe.SizeOf<UniformBufferObject>(), 0, &data);
        new Span<UniformBufferObject>(data, 1)[0] = ubo;
        graphics.vk!.UnmapMemory(graphics.device, graphics.uniformBuffersMemory![currentImage]);
    }

    private static uint FindMemoryType(ref Graphics graphics, uint typeFilter, MemoryPropertyFlags properties)
    {
        graphics.vk!.GetPhysicalDeviceMemoryProperties(graphics.physicalDevice, out PhysicalDeviceMemoryProperties memProperties);

        for (var i = 0; i < memProperties.MemoryTypeCount; i++)
            if ((typeFilter & (1 << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
                return (uint)i;

        throw new Exception("failed to find suitable memory type!");
    }
}