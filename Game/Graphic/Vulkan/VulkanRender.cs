using System;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Game.Graphic.Vulkan;

internal static unsafe partial class VulkanGraphics
{
    internal static bool TryBeginDraw(ref VkGraphics graphics, in DrawInfo drawInfo, out uint imageIndex)
    {
        imageIndex = uint.MaxValue;
        graphics.vk!.WaitForFences(graphics.device, 1, graphics.inFlightFences![graphics.currentFrame], true, ulong.MaxValue);
        Result result = graphics.khrSwapChain!.AcquireNextImage(graphics.device, graphics.swapChain, ulong.MaxValue,
                                                                graphics.imageAvailableSemaphores![graphics.currentFrame], default,
                                                                ref imageIndex);

        if (result == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapChain(ref graphics);
            return false;
        }
        if (result != Result.Success && result != Result.SuboptimalKhr)
            throw new Exception("Failed to acquire swap chain image!");

        UpdateUniformBuffer(ref graphics, drawInfo, imageIndex);
        return true;
    }

    internal static void EndDraw(ref VkGraphics graphics, uint imageIndex)
    {
        if (graphics.imagesInFlight![imageIndex].Handle != default)
            graphics.vk!.WaitForFences(graphics.device, 1, graphics.imagesInFlight[imageIndex], true, ulong.MaxValue);
        graphics.imagesInFlight[imageIndex] = graphics.inFlightFences![graphics.currentFrame];

        SubmitInfo submitInfo = new() { SType = StructureType.SubmitInfo };

        Semaphore* waitSemaphores = stackalloc[] { graphics.imageAvailableSemaphores![graphics.currentFrame] };
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

        graphics.khrSwapChain!.QueuePresent(graphics.presentQueue, presentInfo);

        graphics.currentFrame = (graphics.currentFrame + 1) % MaxFramesInFlight;

        graphics.vk!.DeviceWaitIdle(graphics.device);
    }

    internal static void Draw(ref VkGraphics graphics, in Mesh mesh, ref VkMesh vkMesh)
    {
        SyncMeshBuffers(ref graphics, mesh, ref vkMesh);
        
        ulong* offsetsPtr = stackalloc ulong[] { 0 };
        Buffer* vertexBuffersPtr = stackalloc Buffer[] { vkMesh.vertexBuffer };

        Vk vk = graphics.vk!;

        ClearValue* clearColors = stackalloc ClearValue[]
        {
            new() { Color = new ClearColorValue { Float32_0 = 0, Float32_1 = 0, Float32_2 = 0, Float32_3 = 1 } },
            new() { DepthStencil = new ClearDepthStencilValue { Depth = 1, Stencil = 0 } }
        };

        for (var i = 0; i < graphics.commandBuffers!.Length; i++)
        {
            ref CommandBuffer cmdBuf = ref graphics.commandBuffers[i];

            CommandBufferBeginInfo beginInfo = new(StructureType.CommandBufferBeginInfo);
            Check(vk.BeginCommandBuffer(cmdBuf, beginInfo), "Failed to begin recording command buffer!");

            RenderPassBeginInfo renderPassInfo = new()
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = graphics.renderPass,
                Framebuffer = graphics.swapChainFramebuffers![i],
                RenderArea =
                {
                    Offset = { X = 0, Y = 0 },
                    Extent = graphics.swapChainExtent
                },
                ClearValueCount = 2,
                PClearValues = clearColors
            };

            vk.CmdBeginRenderPass(cmdBuf, &renderPassInfo, SubpassContents.Inline);
            vk.CmdBindPipeline(cmdBuf, PipelineBindPoint.Graphics, graphics.graphicsPipeline);

            if (mesh.indices.Count > 0)
            {
                vk.CmdBindVertexBuffers(cmdBuf, 0, 1, vertexBuffersPtr, offsetsPtr);
                vk.CmdBindIndexBuffer(cmdBuf, vkMesh.indexBuffer, 0, IndexType.Uint32);
                vk.CmdBindDescriptorSets(cmdBuf, PipelineBindPoint.Graphics, graphics.pipelineLayout, 0, 1, graphics.descriptorSets![i], 0, null);
                vk.CmdDrawIndexed(cmdBuf, vkMesh.indexBufferSize, 1, 0, 0, 0);   
            }

            vk.CmdEndRenderPass(graphics.commandBuffers![i]);
            Check(vk.EndCommandBuffer(cmdBuf), "Failed to record command buffer!");
        }
    }

    private static void SyncMeshBuffers(ref VkGraphics graphics, in Mesh mesh, ref VkMesh vkMesh)
    {
        if (vkMesh.indexBufferSize < mesh.indices.Count)
        {
            if (vkMesh.indexBuffer.Handle != default)
                FreeMeshIndexBuffer(ref graphics, ref vkMesh);
            CreateIndexBuffer(ref graphics, ref vkMesh, mesh);
        }
        if (vkMesh.vertexBufferSize < mesh.vertices.Count)
        {
            if (vkMesh.vertexBuffer.Handle != default)
                FreeMeshVertexBuffer(ref graphics, ref vkMesh);
            CreateVertexBuffer(ref graphics, ref vkMesh, mesh);
        }
    }
}